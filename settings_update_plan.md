# Settings Table Refactor Plan

## Overview
Refactor the `settings` table from multiple dedicated columns to a single JSON column (`settings_json`) for better flexibility and maintainability. Each form (MainWindow, TimelineForm, future forms) will have independent scaling factors and settings stored in JSON format.

---

## Current Settings Table Structure

### Current Columns (to be migrated):
- `id` (INTEGER PRIMARY KEY)
- `timeline_id` (INTEGER, FOREIGN KEY to timelines table)
- `font` (TEXT DEFAULT 'Arial')
- `font_size_scale` (REAL DEFAULT 1.0)
- `pixels_per_subtick` (INTEGER DEFAULT 20)
- `custom_css` (TEXT)
- `use_custom_css` (INTEGER DEFAULT 0)
- `is_fullscreen` (INTEGER DEFAULT 0)
- `show_guides` (INTEGER DEFAULT 1)
- `window_size_x` (INTEGER DEFAULT 1000)
- `window_size_y` (INTEGER DEFAULT 700)
- `window_position_x` (INTEGER DEFAULT 300)
- `window_position_y` (INTEGER DEFAULT 100)
- `use_custom_scaling` (INTEGER DEFAULT 0)
- `custom_scale` (REAL DEFAULT 1.0)
- `display_radius` (INTEGER DEFAULT 10)
- `canvas_settings` (TEXT)
- `updated_at` (DATETIME DEFAULT CURRENT_TIMESTAMP)

---

## New Settings Table Structure

### New Columns:
1. **`id`** (INTEGER PRIMARY KEY)
2. **`timeline_id`** (INTEGER, UNIQUE, NULLABLE, NO FOREIGN KEY CONSTRAINT)
   - For timeline forms: stores the actual timeline ID
   - For MainWindow: stores `NULL` (or could use special value like -1/999999, but NULL is preferred)
   - Must be UNIQUE to ensure one settings row per timeline/form
3. **`settings_json`** (TEXT)
   - Stores all settings as a JSON object
   - Simple key-value pairs where keys = setting names, values = setting values
   - Example structure:
     ```json
     {
       "scaling_factor": 1.5,
       "font": "Arial",
       "font_size_scale": 1.0,
       "pixels_per_subtick": 20,
       "window_size_x": 1000,
       "window_size_y": 700,
       ...
     }
     ```
4. **`last_updated`** (DATETIME DEFAULT CURRENT_TIMESTAMP)
   - Renamed from `updated_at` for consistency

---

## TimelineSettings Class

**Location:** `timeline_test/classes/TimelineSettings.cs`

### Current State:
- Placeholder/template class with example properties
- Contains two methods that need implementation:
  - `getDefaultTimelineSettings()` - Returns default settings for timeline forms
  - `getDefaultMainFormSettings()` - Returns default settings for MainWindow
- **DO NOT MODIFY YET** - User will finalize structure and default values later

### Future Requirements:
- Will contain actual settings properties (including `scaling_factor` for per-form scaling)
- Will be used for JSON serialization/deserialization
- Methods should likely be static (since they create new instances)
- Will need to support JSON serialization (System.Text.Json or Newtonsoft.Json)

---

## Migration Strategy

### Detection:
- Check if `settings_json` column exists in the settings table
- If column does NOT exist → old format database, migration needed
- If column exists → new format database, no migration needed

### Migration Process:
1. Read all existing settings rows with old column structure
2. For each row:
   - Create a JSON object mapping old column names to values
   - Map old columns to new JSON structure:
     - `custom_scale` → `scaling_factor` (or appropriate key)
     - `font` → `font`
     - `font_size_scale` → `font_size_scale`
     - `pixels_per_subtick` → `pixels_per_subtick`
     - `window_size_x` → `window_size_x`
     - `window_size_y` → `window_size_y`
     - `window_position_x` → `window_position_x`
     - `window_position_y` → `window_position_y`
     - etc. (map all relevant columns)
   - Use default settings object to fill in any missing values
   - Serialize to JSON string
   - Insert/update row with new structure
3. **Error Handling:** If migration fails at any point, use default settings object instead of failing
4. Drop old columns (or keep for reference during transition, then remove later)

### Migration Safety:
- No backwards compatibility required (app is in dev, not production)
- If migration fails, use default settings - don't crash
- Migration is one-way (old format → new format)

---

## Import Logic for Old Databases

### When importing from old database format:

1. **Detection:**
   - Check if settings table has `settings_json` column
   - If NO → old format database

2. **Conversion Process:**
   - Read all settings rows from old database
   - For each settings row:
     - Create JSON object from old column values
     - Map old columns to new JSON keys
     - Use default settings object to fill missing values
     - Serialize to JSON
   - Insert/update settings in new database with JSON format

3. **Handling Missing Settings:**
   - If a timeline has no settings row, create one with default settings
   - Use `GetDefaultTimelineSettings()` for timeline forms
   - Use `GetDefaultMainFormSettings()` for MainWindow (timeline_id = NULL)

---

## MainWindow Settings

### Storage:
- MainWindow settings stored with `timeline_id = NULL` (preferred approach)
- Alternative: Could use special value like -1 or 999999, but NULL is cleaner
- UNIQUE constraint on `timeline_id` allows NULL (SQLite allows multiple NULLs in UNIQUE, but we want only one NULL row for MainWindow)

### Implementation:
- When loading MainWindow, query: `SELECT settings_json FROM settings WHERE timeline_id IS NULL`
- If no row exists, use `GetDefaultMainFormSettings()` and create row
- When saving MainWindow settings, update or insert row with `timeline_id = NULL`

---

## Settings Access Pattern

### Reading Settings:
- **When:** On program/form startup
- **How:** 
  - Query database for settings_json by timeline_id (or NULL for MainWindow)
  - Deserialize JSON to TimelineSettings object
  - Use helper class/methods for typed access
  - If no settings found, use default settings object

### Writing Settings:
- **When:** Before closing the app/form
- **How:**
  - Serialize TimelineSettings object to JSON
  - Update database row (or insert if doesn't exist)
  - Use UNIQUE constraint to ensure one row per timeline/form

### During Runtime:
- Settings handled locally in memory (TimelineSettings object)
- No database reads/writes during normal operation
- Only persist on close

---

## Helper Class Requirements

### Purpose:
Create a helper class for settings management to:
- Parse JSON from database → TimelineSettings object
- Serialize TimelineSettings object → JSON for database
- Handle default values when keys are missing in JSON
- Provide typed access to settings
- Handle JSON parsing errors gracefully

### Suggested Structure:
```csharp
public static class SettingsHelper
{
    // Deserialize settings_json from database to TimelineSettings
    public static TimelineSettings LoadSettings(long? timelineId);
    
    // Serialize TimelineSettings to JSON and save to database
    public static void SaveSettings(long? timelineId, TimelineSettings settings);
    
    // Get default settings (delegates to TimelineSettings methods)
    public static TimelineSettings GetDefaultTimelineSettings();
    public static TimelineSettings GetDefaultMainFormSettings();
    
    // Merge settings with defaults (fill missing keys)
    public static TimelineSettings MergeWithDefaults(TimelineSettings settings, bool isMainForm);
}
```

---

## Per-Form Scaling Implementation

### Requirement:
Each form (MainWindow, TimelineForm, future forms) must have independent scaling factors:
- MainWindow might be 1.5
- TimelineForm might be 1.2
- Another form might be 2.0
- Each adjustable independently

### Implementation:
1. `scaling_factor` property in TimelineSettings class
2. Each form loads its settings on startup
3. Apply scaling dynamically in code-behind based on loaded value
4. Scaling applied using `LayoutTransform` (current approach is correct)
5. Scaling value stored in settings_json for each form

### Current State:
- MainWindow.xaml currently has hardcoded `ScaleX="1.5" ScaleY="1.5"` in XAML
- This needs to be changed to dynamic application based on loaded settings
- TimelineForm.xaml currently has no scaling applied
- Both need to load scaling_factor from settings and apply programmatically

---

## Database Schema Changes

### SQL Migration Script:
```sql
-- Step 1: Add new columns
ALTER TABLE settings ADD COLUMN settings_json TEXT;
ALTER TABLE settings ADD COLUMN last_updated DATETIME DEFAULT CURRENT_TIMESTAMP;

-- Step 2: Migrate existing data (done in code, not SQL)
-- (Read old columns, create JSON, update settings_json)

-- Step 3: Remove foreign key constraint (if exists)
-- Note: SQLite doesn't support DROP CONSTRAINT directly
-- May need to recreate table without FK

-- Step 4: Add UNIQUE constraint on timeline_id
CREATE UNIQUE INDEX IF NOT EXISTS idx_settings_timeline_id ON settings(timeline_id);

-- Step 5: Make timeline_id nullable (if not already)
-- SQLite columns are nullable by default, but ensure no NOT NULL constraint

-- Step 6: (Later) Drop old columns after migration verified
-- ALTER TABLE settings DROP COLUMN font;
-- ALTER TABLE settings DROP COLUMN font_size_scale;
-- etc.
```

**Note:** SQLite limitations:
- Cannot drop columns directly (need table recreation)
- Cannot drop foreign key constraints directly (need table recreation)
- May need to use table recreation approach for clean migration

---

## Implementation Checklist

### Phase 1: Database Schema
- [ ] Add `settings_json` column to settings table
- [ ] Add `last_updated` column (or rename `updated_at`)
- [ ] Remove foreign key constraint on `timeline_id`
- [ ] Add UNIQUE constraint/index on `timeline_id`
- [ ] Ensure `timeline_id` is nullable

### Phase 2: TimelineSettings Class
- [ ] Wait for user to finalize structure and properties
- [ ] Add `scaling_factor` property
- [ ] Implement `GetDefaultTimelineSettings()` method
- [ ] Implement `GetDefaultMainFormSettings()` method
- [ ] Add JSON serialization attributes/support

### Phase 3: Migration Logic
- [ ] Create migration detection (check for `settings_json` column)
- [ ] Implement old column → JSON conversion
- [ ] Map old column names to new JSON keys
- [ ] Use default settings to fill missing values
- [ ] Handle migration errors gracefully (use defaults)
- [ ] Test migration with sample old-format database

### Phase 4: Import Logic
- [ ] Update import function to detect old format
- [ ] Convert old settings columns to JSON during import
- [ ] Handle timelines without settings (use defaults)
- [ ] Test import with old database files

### Phase 5: Helper Class
- [ ] Create SettingsHelper class
- [ ] Implement JSON deserialization (DB → TimelineSettings)
- [ ] Implement JSON serialization (TimelineSettings → DB)
- [ ] Implement default value merging
- [ ] Add error handling for malformed JSON

### Phase 6: Form Updates
- [ ] Update MainWindow to load settings on startup
- [ ] Apply scaling_factor dynamically to MainWindow
- [ ] Update TimelineForm to load settings on startup
- [ ] Apply scaling_factor dynamically to TimelineForm
- [ ] Save settings on form close
- [ ] Remove hardcoded scaling from XAML

### Phase 7: Testing
- [ ] Test migration from old format
- [ ] Test import from old database
- [ ] Test settings persistence (save/load)
- [ ] Test independent scaling per form
- [ ] Test default settings when no settings exist
- [ ] Test error handling (malformed JSON, missing keys, etc.)

---

## Key Decisions Made

1. **Foreign Key:** Remove FK constraint, make `timeline_id` nullable (NULL for MainWindow)
2. **Uniqueness:** `timeline_id` must be UNIQUE (one settings row per timeline/form)
3. **MainWindow ID:** Use `NULL` for MainWindow's `timeline_id` (cleaner than magic numbers)
4. **Migration Safety:** If migration fails, use default settings (don't crash)
5. **Backwards Compatibility:** Not required (dev phase only)
6. **Settings Access:** Read on startup, write on close, handle in memory otherwise
7. **Scaling:** Each form has independent `scaling_factor` in its settings JSON
8. **Default Values:** Use default settings object to fill missing values during migration/import

---

## Notes

- TimelineSettings.cs is currently a placeholder - DO NOT modify until user finalizes structure
- Current scaling in MainWindow.xaml is hardcoded (1.5) - needs to be dynamic
- TimelineForm currently has no scaling applied - needs to be added
- All settings will be stored as JSON, making it easy to add new settings without schema changes
- JSON structure is simple key-value pairs (not nested complex objects)
- Default settings objects will be provided by TimelineSettings class methods

---

## Questions Resolved

1. ✅ Foreign key: Remove FK, make nullable
2. ✅ JSON structure: Simple key-value pairs
3. ✅ Default values: Provided by TimelineSettings methods, used for missing values
4. ✅ Migration safety: Use defaults if migration fails
5. ✅ Settings access: Read on start, write on close
6. ✅ Import: Convert old columns to JSON
7. ✅ Backwards compatibility: Not needed
8. ✅ Helper class: Yes, for JSON parsing/serialization

---

## Files to Modify (When Implementation Begins)

1. `DatabaseHelper.cs` - Migration logic, schema changes, import updates
2. `TimelineSettings.cs` - Finalize structure, implement default methods (after user input)
3. `MainWindow.xaml.cs` - Load/save settings, apply scaling dynamically
4. `TimelineForm.xaml.cs` - Load/save settings, apply scaling dynamically
5. `MainWindow.xaml` - Remove hardcoded scaling, apply programmatically
6. `TimelineForm.xaml` - Add scaling support (programmatically)
7. New file: `SettingsHelper.cs` - JSON serialization/deserialization helper
8. Potentially: `App.xaml` - Remove global scaling resource if no longer needed

---

*Document created: [Date will be set when file is created]*
*Last updated: [Will track updates]*

