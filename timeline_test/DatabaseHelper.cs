using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using timeline_test.Properties;

namespace timeline_test
{
    /// <summary>
    /// Database helper class for SQLite operations
    /// </summary>
    public class DatabaseHelper : IDisposable
    {
        private string databasePath;
        private SqliteConnection connection;
        private bool disposed = false;

        /// <summary>
        /// Gets the database file path
        /// </summary>
        public string DatabasePath => databasePath;

        /// <summary>
        /// Gets the SQLite connection
        /// </summary>
        public SqliteConnection Connection => connection;

        /// <summary>
        /// Initializes a new instance of DatabaseHelper
        /// </summary>
        /// <param name="dbPath">Path to the SQLite database file</param>
        public DatabaseHelper(string dbPath = null)
        {
            if (string.IsNullOrEmpty(dbPath))
            {
                // Default to application directory (where executable is)
                string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
                databasePath = Path.Combine(appDirectory, "timeline.db");
            }
            else
            {
                databasePath = dbPath;
            }

            InitializeDatabase();
        }

        /// <summary>
        /// Initializes the database connection and creates tables if needed
        /// </summary>
        private void InitializeDatabase()
        {
            bool databaseExists = File.Exists(databasePath);

            string connectionString = $"Data Source={databasePath}";
            connection = new SqliteConnection(connectionString);
            connection.Open();

            // Enable foreign keys
            using (SqliteCommand command = new SqliteCommand("PRAGMA foreign_keys = ON;", connection))
            {
                command.ExecuteNonQuery();
            }

            if (!databaseExists || !IsSchemaComplete())
            {
                CreateDatabaseSchema();
            }
        }

        /// <summary>
        /// Checks if the database schema is complete by verifying key tables exist
        /// </summary>
        private bool IsSchemaComplete()
        {
            try
            {
                // Check for essential tables
                string[] essentialTables = { "timelines", "stories", "item_types", "items", "tags", 
                    "item_tags", "pictures", "item_pictures", "notes", "item_story_refs", 
                    "settings", "characters", "character_relationships", "item_characters", "db_version" };

                foreach (string tableName in essentialTables)
                {
                    string checkTableSql = "SELECT name FROM sqlite_master WHERE type='table' AND name=@tableName;";
                    using (SqliteCommand command = new SqliteCommand(checkTableSql, connection))
                    {
                        command.Parameters.AddWithValue("@tableName", tableName);
                        object result = command.ExecuteScalar();
                        if (result == null)
                        {
                            return false; // Table is missing
                        }
                    }
                }
                return true;
            }
            catch
            {
                return false; // If we can't check, assume incomplete
            }
        }

        /// <summary>
        /// Gets the current database version from the db_version table
        /// </summary>
        public string GetDatabaseVersion()
        {
            try
            {
                string sql = "SELECT version FROM db_version WHERE id = 1;";
                object result = ExecuteScalar(sql);
                return result?.ToString() ?? "0.0";
            }
            catch
            {
                return "0.0"; // If table doesn't exist or error, return 0.0
            }
        }

        /// <summary>
        /// Checks if migrations are needed by comparing Settings version with database version
        /// </summary>
        public bool NeedsMigration()
        {
            string dbVersion = GetDatabaseVersion();
            string settingsVersion = Settings.Default.DBVersion ?? "2.0";
            
            // Simple version comparison (assumes semantic versioning like "2.0", "2.1", etc.)
            if (Version.TryParse(dbVersion, out Version dbVer) && Version.TryParse(settingsVersion, out Version settingsVer))
            {
                return settingsVer > dbVer;
            }
            
            // Fallback: string comparison if parsing fails
            return string.Compare(settingsVersion, dbVersion, StringComparison.OrdinalIgnoreCase) > 0;
        }

        /// <summary>
        /// Creates the complete database schema for a new database
        /// </summary>
        private void CreateDatabaseSchema()
        {
            using (SqliteTransaction transaction = connection.BeginTransaction())
            {
                try
                {
                    // Create all tables in the correct order (respecting foreign key dependencies)
                    CreateTables(transaction);
                    
                    // Create indexes
                    CreateIndexes(transaction);
                    
                    // Insert default data
                    InsertDefaultData(transaction);
                    
                    // Create db_version table and insert initial version
                    CreateVersionTable(transaction);
                    
                    transaction.Commit();
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }

        /// <summary>
        /// Creates all database tables in the correct order
        /// </summary>
        private void CreateTables(SqliteTransaction transaction)
        {
            // 1. timelines
            ExecuteCommand(transaction, @"
                CREATE TABLE IF NOT EXISTS timelines (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    title TEXT NOT NULL,
                    author TEXT NOT NULL,
                    description TEXT,
                    start_year INTEGER DEFAULT 0,
                    granularity INTEGER DEFAULT 4,
                    picture TEXT,
                    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                    UNIQUE(title, author)
                );
            ");

            // 2. stories
            ExecuteCommand(transaction, @"
                CREATE TABLE IF NOT EXISTS stories (
                    id TEXT PRIMARY KEY,
                    title TEXT NOT NULL,
                    description TEXT,
                    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP
                );
            ");

            // 3. item_types
            ExecuteCommand(transaction, @"
                CREATE TABLE IF NOT EXISTS item_types (
                    id INTEGER PRIMARY KEY,
                    name TEXT UNIQUE,
                    description TEXT,
                    created_at DATETIME DEFAULT CURRENT_TIMESTAMP
                );
            ");

            // 4. items (includes importance column for fresh databases)
            ExecuteCommand(transaction, @"
                CREATE TABLE IF NOT EXISTS items (
                    id TEXT PRIMARY KEY,
                    title TEXT,
                    description TEXT,
                    content TEXT,
                    story_id TEXT,
                    type_id INTEGER DEFAULT 1,
                    year INTEGER,
                    subtick INTEGER,
                    original_subtick INTEGER,
                    end_year INTEGER,
                    end_subtick INTEGER,
                    original_end_subtick INTEGER,
                    book_title TEXT,
                    chapter TEXT,
                    page TEXT,
                    color TEXT,
                    creation_granularity INTEGER,
                    timeline_id INTEGER,
                    item_index INTEGER DEFAULT 0,
                    show_in_notes INTEGER DEFAULT 1,
                    importance INTEGER DEFAULT 5,
                    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (story_id) REFERENCES stories(id),
                    FOREIGN KEY (type_id) REFERENCES item_types(id),
                    FOREIGN KEY (timeline_id) REFERENCES timelines(id) ON DELETE CASCADE
                );
            ");

            // 5. tags
            ExecuteCommand(transaction, @"
                CREATE TABLE IF NOT EXISTS tags (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    name TEXT UNIQUE NOT NULL,
                    created_at DATETIME DEFAULT CURRENT_TIMESTAMP
                );
            ");

            // 6. item_tags
            ExecuteCommand(transaction, @"
                CREATE TABLE IF NOT EXISTS item_tags (
                    item_id TEXT,
                    tag_id INTEGER,
                    FOREIGN KEY (item_id) REFERENCES items(id) ON DELETE CASCADE,
                    FOREIGN KEY (tag_id) REFERENCES tags(id) ON DELETE CASCADE,
                    PRIMARY KEY (item_id, tag_id)
                );
            ");

            // 7. pictures
            ExecuteCommand(transaction, @"
                CREATE TABLE IF NOT EXISTS pictures (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    item_id TEXT,
                    file_path TEXT,
                    file_name TEXT,
                    file_size INTEGER,
                    file_type TEXT,
                    width INTEGER,
                    height INTEGER,
                    title TEXT,
                    description TEXT,
                    picture TEXT,
                    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (item_id) REFERENCES items(id) ON DELETE CASCADE
                );
            ");

            // 8. item_pictures
            ExecuteCommand(transaction, @"
                CREATE TABLE IF NOT EXISTS item_pictures (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    item_id TEXT NOT NULL,
                    picture_id INTEGER NOT NULL,
                    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (item_id) REFERENCES items(id) ON DELETE CASCADE,
                    FOREIGN KEY (picture_id) REFERENCES pictures(id) ON DELETE CASCADE,
                    UNIQUE(item_id, picture_id)
                );
            ");

            // 9. notes
            ExecuteCommand(transaction, @"
                CREATE TABLE IF NOT EXISTS notes (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    year INTEGER,
                    subtick INTEGER,
                    content TEXT,
                    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP
                );
            ");

            // 10. item_story_refs
            ExecuteCommand(transaction, @"
                CREATE TABLE IF NOT EXISTS item_story_refs (
                    item_id TEXT,
                    story_id TEXT,
                    FOREIGN KEY (item_id) REFERENCES items(id) ON DELETE CASCADE,
                    FOREIGN KEY (story_id) REFERENCES stories(id) ON DELETE CASCADE,
                    PRIMARY KEY (item_id, story_id)
                );
            ");

            // 11. settings
            ExecuteCommand(transaction, @"
                CREATE TABLE IF NOT EXISTS settings (
                    id INTEGER PRIMARY KEY,
                    timeline_id INTEGER,
                    font TEXT DEFAULT 'Arial',
                    font_size_scale REAL DEFAULT 1.0,
                    pixels_per_subtick INTEGER DEFAULT 20,
                    custom_css TEXT,
                    use_custom_css INTEGER DEFAULT 0,
                    is_fullscreen INTEGER DEFAULT 0,
                    show_guides INTEGER DEFAULT 1,
                    window_size_x INTEGER DEFAULT 1000,
                    window_size_y INTEGER DEFAULT 700,
                    window_position_x INTEGER DEFAULT 300,
                    window_position_y INTEGER DEFAULT 100,
                    use_custom_scaling INTEGER DEFAULT 0,
                    custom_scale REAL DEFAULT 1.0,
                    display_radius INTEGER DEFAULT 10,
                    canvas_settings TEXT,
                    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (timeline_id) REFERENCES timelines(id) ON DELETE CASCADE
                );
            ");

            // 12. characters
            ExecuteCommand(transaction, @"
                CREATE TABLE IF NOT EXISTS characters (
                    id TEXT PRIMARY KEY,
                    name TEXT NOT NULL,
                    nicknames TEXT,
                    aliases TEXT,
                    race TEXT,
                    description TEXT,
                    notes TEXT,
                    birth_year INTEGER,
                    birth_subtick INTEGER,
                    birth_date TEXT,
                    birth_alternative_year TEXT,
                    death_year INTEGER,
                    death_subtick INTEGER,
                    death_date TEXT,
                    death_alternative_year TEXT,
                    importance INTEGER DEFAULT 5,
                    color TEXT,
                    timeline_id INTEGER NOT NULL,
                    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (timeline_id) REFERENCES timelines(id) ON DELETE CASCADE
                );
            ");

            // 13. character_relationships
            ExecuteCommand(transaction, @"
                CREATE TABLE IF NOT EXISTS character_relationships (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    character_1_id TEXT NOT NULL,
                    character_2_id TEXT NOT NULL,
                    relationship_type TEXT NOT NULL CHECK (relationship_type IN (
                        'parent', 'child', 'sibling', 'spouse', 'partner', 'ex-spouse', 'ex-partner',
                        'grandparent', 'grandchild', 'great-grandparent', 'great-grandchild',
                        'great-great-grandparent', 'great-great-grandchild',
                        'aunt', 'uncle', 'niece', 'nephew', 'cousin', 'great-aunt', 'great-uncle',
                        'great-niece', 'great-nephew',
                        'step-parent', 'step-child', 'step-sibling', 'half-sibling',
                        'step-grandparent', 'step-grandchild',
                        'parent-in-law', 'child-in-law', 'sibling-in-law', 'grandparent-in-law', 'grandchild-in-law',
                        'adoptive-parent', 'adoptive-child', 'adoptive-sibling',
                        'foster-parent', 'foster-child', 'foster-sibling',
                        'biological-parent', 'biological-child',
                        'godparent', 'godchild', 'mentor', 'apprentice', 'guardian', 'ward',
                        'best-friend', 'friend', 'ally', 'enemy', 'rival', 'acquaintance', 'colleague', 'neighbor',
                        'familiar', 'bonded', 'master', 'servant', 'liege', 'vassal', 'clan-member', 'pack-member',
                        'custom', 'other'
                    )),
                    custom_relationship_type TEXT,
                    relationship_degree TEXT,
                    relationship_modifier TEXT,
                    relationship_strength INTEGER DEFAULT 50,
                    is_bidirectional INTEGER DEFAULT 0,
                    notes TEXT,
                    timeline_id INTEGER NOT NULL,
                    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (character_1_id) REFERENCES characters(id) ON DELETE CASCADE,
                    FOREIGN KEY (character_2_id) REFERENCES characters(id) ON DELETE CASCADE,
                    FOREIGN KEY (timeline_id) REFERENCES timelines(id) ON DELETE CASCADE
                );
            ");

            // 14. item_characters
            ExecuteCommand(transaction, @"
                CREATE TABLE IF NOT EXISTS item_characters (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    item_id TEXT NOT NULL,
                    character_id TEXT NOT NULL,
                    relationship_type TEXT DEFAULT 'appears',
                    timeline_id INTEGER NOT NULL,
                    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (item_id) REFERENCES items(id) ON DELETE CASCADE,
                    FOREIGN KEY (character_id) REFERENCES characters(id) ON DELETE CASCADE,
                    FOREIGN KEY (timeline_id) REFERENCES timelines(id) ON DELETE CASCADE,
                    UNIQUE(item_id, character_id)
                );
            ");
        }

        /// <summary>
        /// Creates all database indexes
        /// </summary>
        private void CreateIndexes(SqliteTransaction transaction)
        {
            // Items table indexes
            ExecuteCommand(transaction, "CREATE INDEX IF NOT EXISTS idx_items_timeline_id ON items(timeline_id);");
            ExecuteCommand(transaction, "CREATE INDEX IF NOT EXISTS idx_items_year_subtick ON items(year, subtick);");
            ExecuteCommand(transaction, "CREATE INDEX IF NOT EXISTS idx_items_type_id ON items(type_id);");
            ExecuteCommand(transaction, "CREATE INDEX IF NOT EXISTS idx_items_item_index ON items(item_index);");
            ExecuteCommand(transaction, "CREATE INDEX IF NOT EXISTS idx_items_story_id ON items(story_id);");

            // Item-Tags junction table indexes
            ExecuteCommand(transaction, "CREATE INDEX IF NOT EXISTS idx_item_tags_item_id ON item_tags(item_id);");
            ExecuteCommand(transaction, "CREATE INDEX IF NOT EXISTS idx_item_tags_tag_id ON item_tags(tag_id);");

            // Item-Pictures junction table indexes
            ExecuteCommand(transaction, "CREATE INDEX IF NOT EXISTS idx_item_pictures_item_id ON item_pictures(item_id);");
            ExecuteCommand(transaction, "CREATE INDEX IF NOT EXISTS idx_item_pictures_picture_id ON item_pictures(picture_id);");
            ExecuteCommand(transaction, "CREATE INDEX IF NOT EXISTS idx_item_pictures_combined ON item_pictures(item_id, picture_id);");

            // Item-Story References table indexes
            ExecuteCommand(transaction, "CREATE INDEX IF NOT EXISTS idx_item_story_refs_item_id ON item_story_refs(item_id);");
            ExecuteCommand(transaction, "CREATE INDEX IF NOT EXISTS idx_item_story_refs_story_id ON item_story_refs(story_id);");

            // Tags table indexes
            ExecuteCommand(transaction, "CREATE INDEX IF NOT EXISTS idx_tags_name ON tags(name);");

            // Notes table indexes
            ExecuteCommand(transaction, "CREATE INDEX IF NOT EXISTS idx_notes_year_subtick ON notes(year, subtick);");
        }

        /// <summary>
        /// Inserts default data into the database
        /// </summary>
        private void InsertDefaultData(SqliteTransaction transaction)
        {
            // Insert default item_types
            ExecuteCommand(transaction, @"
                INSERT OR IGNORE INTO item_types (id, name, description) VALUES
                    (1, 'Event', 'A specific point in time'),
                    (2, 'Period', 'A span of time'),
                    (3, 'Age', 'A significant era or period'),
                    (4, 'Picture', 'An image or visual record'),
                    (5, 'Note', 'A text note or annotation'),
                    (6, 'Bookmark', 'A marked point of interest'),
                    (7, 'Character', 'A person or entity'),
                    (8, 'Timeline_start', 'The start point of the timeline'),
                    (9, 'Timeline_end', 'The end point of the timeline');
            ");
        }

        /// <summary>
        /// Creates the db_version table and inserts the initial version
        /// </summary>
        private void CreateVersionTable(SqliteTransaction transaction)
        {
            // Create db_version table
            ExecuteCommand(transaction, @"
                CREATE TABLE IF NOT EXISTS db_version (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    version TEXT NOT NULL
                );
            ");

            // Check if version already exists
            string checkSql = "SELECT COUNT(*) FROM db_version WHERE id = 1;";
            using (SqliteCommand checkCmd = new SqliteCommand(checkSql, connection, transaction))
            {
                long count = (long)checkCmd.ExecuteScalar();
                if (count == 0)
                {
                    // Insert initial version from settings
                    string dbVersion = Settings.Default.DBVersion ?? "2.0";
                    ExecuteCommand(transaction, @"
                        INSERT INTO db_version (id, version) VALUES (1, @version);
                    ", new SqliteParameter("@version", dbVersion));
                }
            }
        }

        /// <summary>
        /// Executes a SQL command within a transaction
        /// </summary>
        private void ExecuteCommand(SqliteTransaction transaction, string sql, params SqliteParameter[] parameters)
        {
            using (SqliteCommand command = new SqliteCommand(sql, connection, transaction))
            {
                if (parameters != null)
                {
                    command.Parameters.AddRange(parameters);
                }
                command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Executes a non-query SQL command
        /// </summary>
        public int ExecuteNonQuery(string sql, params SqliteParameter[] parameters)
        {
            using (SqliteCommand command = new SqliteCommand(sql, connection))
            {
                if (parameters != null)
                {
                    command.Parameters.AddRange(parameters);
                }
                return command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Executes a query and returns a DataReader
        /// </summary>
        public SqliteDataReader ExecuteReader(string sql, params SqliteParameter[] parameters)
        {
            SqliteCommand command = new SqliteCommand(sql, connection);
            if (parameters != null)
            {
                command.Parameters.AddRange(parameters);
            }
            return command.ExecuteReader();
        }

        /// <summary>
        /// Executes a scalar query
        /// </summary>
        public object ExecuteScalar(string sql, params SqliteParameter[] parameters)
        {
            using (SqliteCommand command = new SqliteCommand(sql, connection))
            {
                if (parameters != null)
                {
                    command.Parameters.AddRange(parameters);
                }
                return command.ExecuteScalar();
            }
        }

        /// <summary>
        /// Begins a transaction
        /// </summary>
        public SqliteTransaction BeginTransaction()
        {
            return connection.BeginTransaction();
        }

        /// <summary>
        /// Closes the database connection
        /// </summary>
        public void Close()
        {
            if (connection != null && connection.State != ConnectionState.Closed)
            {
                connection.Close();
            }
        }

        /// <summary>
        /// Disposes the database helper
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected dispose method
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    Close();
                    connection?.Dispose();
                }
                disposed = true;
            }
        }

        /// <summary>
        /// Imports all data from an old database into the current database
        /// </summary>
        /// <param name="oldDbPath">Path to the old database file</param>
        /// <returns>Number of records imported</returns>
        public int ImportFromOldDatabase(string oldDbPath)
        {
            if (!File.Exists(oldDbPath))
            {
                throw new FileNotFoundException($"Old database not found: {oldDbPath}");
            }

            int totalRecords = 0;

            using (var oldConnection = new SqliteConnection($"Data Source={oldDbPath}"))
            {
                oldConnection.Open();

                // Enable foreign keys on old database too
                using (SqliteCommand command = new SqliteCommand("PRAGMA foreign_keys = ON;", oldConnection))
                {
                    command.ExecuteNonQuery();
                }

                using (SqliteTransaction transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // Import tables in order (respecting foreign key dependencies)
                        totalRecords += ImportTable(oldConnection, transaction, "timelines");
                        totalRecords += ImportTable(oldConnection, transaction, "stories");
                        totalRecords += ImportTable(oldConnection, transaction, "item_types", skipIfExists: true); // Skip if default data exists
                        totalRecords += ImportTable(oldConnection, transaction, "items");
                        totalRecords += ImportTable(oldConnection, transaction, "tags");
                        totalRecords += ImportTable(oldConnection, transaction, "item_tags");
                        totalRecords += ImportTable(oldConnection, transaction, "pictures");
                        totalRecords += ImportTable(oldConnection, transaction, "item_pictures");
                        totalRecords += ImportTable(oldConnection, transaction, "notes");
                        totalRecords += ImportTable(oldConnection, transaction, "item_story_refs");
                        totalRecords += ImportTable(oldConnection, transaction, "settings");
                        totalRecords += ImportTable(oldConnection, transaction, "characters");
                        totalRecords += ImportTable(oldConnection, transaction, "character_relationships");
                        totalRecords += ImportTable(oldConnection, transaction, "item_characters");

                        // Create db_version table and set version to 2.0
                        CreateVersionTable(transaction);

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }

            return totalRecords;
        }

        /// <summary>
        /// Imports data from a specific table in the old database
        /// </summary>
        private int ImportTable(SqliteConnection oldConnection, SqliteTransaction transaction, string tableName, bool skipIfExists = false)
        {
            // Check if table exists in old database
            string checkTableSql = "SELECT name FROM sqlite_master WHERE type='table' AND name=@tableName;";
            using (var checkCmd = new SqliteCommand(checkTableSql, oldConnection))
            {
                checkCmd.Parameters.AddWithValue("@tableName", tableName);
                if (checkCmd.ExecuteScalar() == null)
                {
                    return 0; // Table doesn't exist in old database
                }
            }

            // For item_types, check if we should skip (if default data already exists)
            if (skipIfExists && tableName == "item_types")
            {
                string checkDataSql = "SELECT COUNT(*) FROM item_types;";
                using (var checkCmd = new SqliteCommand(checkDataSql, connection, transaction))
                {
                    long count = (long)checkCmd.ExecuteScalar();
                    if (count > 0)
                    {
                        return 0; // Skip if data already exists
                    }
                }
            }

            // Get column names from old table
            var oldColumns = new List<string>();
            string pragmaSql = $"PRAGMA table_info({tableName});";
            using (var pragmaCmd = new SqliteCommand(pragmaSql, oldConnection))
            using (var reader = pragmaCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    oldColumns.Add(reader.GetString(1)); // Column name is at index 1
                }
            }

            if (oldColumns.Count == 0)
            {
                return 0; // No columns found
            }

            // Check if picture column exists in old DB
            bool pictureExistsInOld = oldColumns.Contains("picture");

            // For timelines table, if picture column doesn't exist in old DB, we'll add it as NULL
            if (tableName == "timelines" && !pictureExistsInOld)
            {
                oldColumns.Add("picture");
            }

            // Build SELECT statement - only select columns that exist in old table (excluding added picture)
            var selectColumns = oldColumns.Where(c => c != "picture" || pictureExistsInOld).ToList();
            string selectSql = $"SELECT {string.Join(", ", selectColumns)} FROM {tableName};";

            // Build INSERT statement with all columns (including picture if needed)
            string columnList = string.Join(", ", oldColumns);
            string parameterList = string.Join(", ", oldColumns.Select(c => $"@{c}"));
            string insertSql = $"INSERT OR REPLACE INTO {tableName} ({columnList}) VALUES ({parameterList});";

            int recordCount = 0;

            // Read all data from old table
            using (var selectCmd = new SqliteCommand(selectSql, oldConnection))
            using (var reader = selectCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    using (var insertCmd = new SqliteCommand(insertSql, connection, transaction))
                    {
                        // Add parameters with values from old database
                        int readerIndex = 0;
                        for (int i = 0; i < oldColumns.Count; i++)
                        {
                            string colName = oldColumns[i];
                            
                            // Handle picture column that might not exist in old DB
                            if (colName == "picture" && !pictureExistsInOld && tableName == "timelines")
                            {
                                // Set picture to NULL if it didn't exist in old DB
                                insertCmd.Parameters.AddWithValue($"@{colName}", DBNull.Value);
                            }
                            else
                            {
                                if (reader.IsDBNull(readerIndex))
                                {
                                    insertCmd.Parameters.AddWithValue($"@{colName}", DBNull.Value);
                                }
                                else
                                {
                                    // Get the value based on column type
                                    object value = reader.GetValue(readerIndex);
                                    insertCmd.Parameters.AddWithValue($"@{colName}", value);
                                }
                                readerIndex++;
                            }
                        }

                        insertCmd.ExecuteNonQuery();
                        recordCount++;
                    }
                }
            }

            return recordCount;
        }

        /// <summary>
        /// Gets all timelines from the database
        /// </summary>
        /// <returns>List of dictionaries containing timeline data (id, title, author, description, start_year, granularity, picture)</returns>
        public List<Dictionary<string, object>> GetAllTimelines()
        {
            var timelines = new List<Dictionary<string, object>>();
            
            string sql = "SELECT id, title, author, description, start_year, granularity, picture, created_at, updated_at FROM timelines ORDER BY title, author;";
            using (var reader = ExecuteReader(sql))
            {
                while (reader.Read())
                {
                    var timeline = new Dictionary<string, object>
                    {
                        ["id"] = reader.GetInt64(0),
                        ["title"] = reader.GetString(1),
                        ["author"] = reader.GetString(2),
                        ["description"] = reader.IsDBNull(3) ? null : reader.GetString(3),
                        ["start_year"] = reader.GetInt32(4),
                        ["granularity"] = reader.GetInt32(5),
                        ["picture"] = reader.IsDBNull(6) ? null : reader.GetString(6),
                        ["created_at"] = reader.IsDBNull(7) ? null : reader.GetString(7),
                        ["updated_at"] = reader.IsDBNull(8) ? null : reader.GetString(8)
                    };
                    timelines.Add(timeline);
                }
            }
            
            return timelines;
        }

        /// <summary>
        /// Gets settings for a specific timeline
        /// </summary>
        /// <param name="timelineId">The timeline ID</param>
        /// <returns>Dictionary containing settings data, or null if not found</returns>
        public Dictionary<string, object> GetTimelineSettings(long timelineId)
        {
            string sql = @"SELECT id, timeline_id, font, font_size_scale, pixels_per_subtick, custom_css, use_custom_css,
                          is_fullscreen, show_guides, window_size_x, window_size_y, window_position_x, window_position_y,
                          use_custom_scaling, custom_scale, display_radius, canvas_settings, updated_at
                          FROM settings WHERE timeline_id = @timelineId;";
            
            using (var command = new SqliteCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@timelineId", timelineId);
                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return new Dictionary<string, object>
                        {
                            ["id"] = reader.GetInt64(0),
                            ["timeline_id"] = reader.IsDBNull(1) ? null : (object)reader.GetInt64(1),
                            ["font"] = reader.IsDBNull(2) ? "Arial" : reader.GetString(2),
                            ["font_size_scale"] = reader.IsDBNull(3) ? 1.0 : reader.GetDouble(3),
                            ["pixels_per_subtick"] = reader.IsDBNull(4) ? 20 : reader.GetInt32(4),
                            ["custom_css"] = reader.IsDBNull(5) ? null : reader.GetString(5),
                            ["use_custom_css"] = reader.IsDBNull(6) ? 0 : reader.GetInt32(6),
                            ["is_fullscreen"] = reader.IsDBNull(7) ? 0 : reader.GetInt32(7),
                            ["show_guides"] = reader.IsDBNull(8) ? 1 : reader.GetInt32(8),
                            ["window_size_x"] = reader.IsDBNull(9) ? 1000 : reader.GetInt32(9),
                            ["window_size_y"] = reader.IsDBNull(10) ? 700 : reader.GetInt32(10),
                            ["window_position_x"] = reader.IsDBNull(11) ? 300 : reader.GetInt32(11),
                            ["window_position_y"] = reader.IsDBNull(12) ? 100 : reader.GetInt32(12),
                            ["use_custom_scaling"] = reader.IsDBNull(13) ? 0 : reader.GetInt32(13),
                            ["custom_scale"] = reader.IsDBNull(14) ? 1.0 : reader.GetDouble(14),
                            ["display_radius"] = reader.IsDBNull(15) ? 10 : reader.GetInt32(15),
                            ["canvas_settings"] = reader.IsDBNull(16) ? null : reader.GetString(16),
                            ["updated_at"] = reader.IsDBNull(17) ? null : reader.GetString(17)
                        };
                    }
                }
            }
            
            return null; // No settings found
        }

        /// <summary>
        /// TEMPORARY: Inspects the schema of a database file (for analyzing old database structure)
        /// </summary>
        public static void InspectDatabaseSchema(string dbPath)
        {
            string outputPath = Path.Combine(Path.GetDirectoryName(dbPath), "old_db_schema.txt");
            using (var writer = new StreamWriter(outputPath))
            using (var connection = new SqliteConnection($"Data Source={dbPath}"))
            {
                connection.Open();
                
                writer.WriteLine("=== TABLES ===");
                var tablesQuery = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name;";
                using (var command = new SqliteCommand(tablesQuery, connection))
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string tableName = reader.GetString(0);
                        writer.WriteLine($"\nTable: {tableName}");
                        
                        // Get schema for this table
                        using (var schemaCmd = new SqliteCommand($"PRAGMA table_info({tableName});", connection))
                        using (var schemaReader = schemaCmd.ExecuteReader())
                        {
                            writer.WriteLine("  Columns:");
                            while (schemaReader.Read())
                            {
                                string colName = schemaReader.GetString(1);
                                string colType = schemaReader.GetString(2);
                                int notNull = schemaReader.GetInt32(3);
                                string defaultValue = schemaReader.IsDBNull(4) ? "NULL" : schemaReader.GetString(4);
                                int pk = schemaReader.GetInt32(5);
                                writer.WriteLine($"    {colName} ({colType}) {(notNull == 1 ? "NOT NULL" : "")} {(pk > 0 ? "PRIMARY KEY" : "")} DEFAULT: {defaultValue}");
                            }
                        }
                    }
                }
                
                writer.WriteLine("\n\n=== INDEXES ===");
                var indexesQuery = "SELECT name, tbl_name, sql FROM sqlite_master WHERE type='index' AND name NOT LIKE 'sqlite_%' ORDER BY name;";
                using (var command = new SqliteCommand(indexesQuery, connection))
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        writer.WriteLine($"{reader.GetString(0)} on {reader.GetString(1)}");
                    }
                }
            }
        }
    }
}



