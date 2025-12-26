using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Win32;
using timeline.classes;

namespace timeline
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private DatabaseHelper databaseHelper;
        private List<Dictionary<string, object>> timelines;
        private Dictionary<long, TimelineBase> timelineBases;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadTimelines();
        }

        /// <summary>
        /// Loads all timelines from the database and displays them in the list
        /// </summary>
        private void LoadTimelines()
        {
            try
            {
                databaseHelper = new DatabaseHelper();
                timelines = databaseHelper.GetAllTimelines();

                // Create TimelineBase objects for each timeline
                timelineBases = new Dictionary<long, TimelineBase>();
                foreach (var timeline in timelines)
                {
                    long timelineId = (long)timeline["id"];
                    var timelineBase = new TimelineBase
                    {
                        Title = timeline["title"]?.ToString() ?? "Untitled",
                        Author = timeline["author"]?.ToString() ?? "Unknown",
                        Description = timeline["description"]?.ToString() ?? "",
                        ImagePath = timeline["picture"]?.ToString() ?? ""
                    };
                    timelineBases[timelineId] = timelineBase;
                }

                // Create a list of timeline display items with separate title and author
                var timelineDisplayItems = timelines.Select(t => new
                {
                    Id = t["id"],
                    Title = t["title"]?.ToString() ?? "Untitled",
                    Author = $"by {t["author"]?.ToString() ?? "Unknown"}"
                }).ToList();

                TimelineListBox.ItemsSource = timelineDisplayItems;

                // Also read settings (for future use)
                if (timelines.Count > 0)
                {
                    // For now, just ensure we can read settings - we'll use this later
                    foreach (var timeline in timelines)
                    {
                        long timelineId = (long)timeline["id"];
                        var settings = databaseHelper.GetTimelineSettings(timelineId);
                        // Settings loaded but not used yet - will be used when opening timeline
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error loading timelines:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void TimelineListBox_MouseMove(object sender, MouseEventArgs e)
        {
            // Find the ListBoxItem under the mouse
            var hitTestResult = VisualTreeHelper.HitTest(TimelineListBox, e.GetPosition(TimelineListBox));
            if (hitTestResult != null)
            {
                DependencyObject current = hitTestResult.VisualHit;
                while (current != null && current != TimelineListBox)
                {
                    if (current is ListBoxItem listBoxItem)
                    {
                        // Get the data item
                        var item = listBoxItem.DataContext;
                        if (item != null)
                        {
                            dynamic timelineItem = item;
                            long timelineId = (long)timelineItem.Id;

                            // Update preview with timeline data
                            if (timelineBases != null && timelineBases.ContainsKey(timelineId))
                            {
                                UpdatePreview(timelineBases[timelineId]);
                            }
                            return;
                        }
                    }
                    current = VisualTreeHelper.GetParent(current);
                }
            }
            
            // Clear preview when mouse is not over an item
            ClearPreview();
        }

        /// <summary>
        /// Updates the preview panel with timeline data
        /// </summary>
        private void UpdatePreview(TimelineBase timeline)
        {
            PreviewTitleLabel.Content = timeline.Title;
            PreviewAuthorLabel.Content = timeline.Author;
            PreviewDescriptionTextBlock.Text = string.IsNullOrEmpty(timeline.Description) ? "No description available." : timeline.Description;

            // Load and display image
            if (!string.IsNullOrEmpty(timeline.ImagePath) && File.Exists(timeline.ImagePath))
            {
                try
                {
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(timeline.ImagePath, UriKind.Absolute);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    PreviewImage.Source = bitmap;
                    PreviewImage.Visibility = Visibility.Visible;
                }
                catch
                {
                    PreviewImage.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                PreviewImage.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Clears the preview panel
        /// </summary>
        private void ClearPreview()
        {
            PreviewTitleLabel.Content = "";
            PreviewAuthorLabel.Content = "";
            PreviewDescriptionTextBlock.Text = "";
            PreviewImage.Source = null;
            PreviewImage.Visibility = Visibility.Collapsed;
        }

        private void TimelineListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TimelineListBox.SelectedItem != null && timelines != null)
            {
                // Get the selected timeline ID
                dynamic selectedItem = TimelineListBox.SelectedItem;
                long timelineId = (long)selectedItem.Id;

                // Find the timeline data
                var selectedTimeline = timelines.FirstOrDefault(t => (long)t["id"] == timelineId);
                
                if (selectedTimeline != null)
                {
                    // Open timeline form with the selected timeline ID
                    TimelineForm timelineForm = new TimelineForm(timelineId, databaseHelper);
                    timelineForm.Show();
                    
                    // Clear selection so user can click again
                    TimelineListBox.SelectedItem = null;
                }
            }
        }

        private void ImportDatabaseButton_Click(object sender, RoutedEventArgs e)
        {
            if(MessageBox.Show(
                "Importing an old database will merge its data into the current database. " +
                "Make sure to back up your current database before proceeding.\n\n" +
                "Do you want to continue?",
                "Import Old Database",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }

            // Open file dialog to select old database
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "SQLite Database (*.db)|*.db|All files (*.*)|*.*",
                Title = "Select Old Database to Import"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    // Disable button during import
                    ImportDatabaseButton.IsEnabled = false;
                    ImportDatabaseButton.Content = "Importing...";

                    // Import the database
                    using (var db = new DatabaseHelper())
                    {
                        int recordsImported = db.ImportFromOldDatabase(openFileDialog.FileName);
                        
                        MessageBox.Show(
                            $"Import completed successfully!\n\n{recordsImported} records imported.",
                            "Import Complete",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                        
                        // Reload timelines after import
                        LoadTimelines();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Error importing database:\n\n{ex.Message}",
                        "Import Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
                finally
                {
                    // Re-enable button
                    ImportDatabaseButton.IsEnabled = true;
                    ImportDatabaseButton.Content = "Import Old Database";
                }
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            databaseHelper?.Dispose();
            base.OnClosed(e);
        }
    }
}
