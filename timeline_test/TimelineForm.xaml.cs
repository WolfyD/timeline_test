using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace timeline_test
{
    /// <summary>
    /// Interaction logic for TimelineForm.xaml
    /// </summary>
    public partial class TimelineForm : Window
    {
        // Timeline configuration variables
        private double tickGap = 50.0; // Pixels between ticks
        private int startYear = 0; // Initial year to center
        private double currentOffset = 0.0; // Current horizontal offset in pixels
        private int yearSubdivision = 0; // Marks per year (0 = years only)
        private double centerY = 150.0; // Vertical center (half of 300px height)
        private double centerX = 400.0; // Horizontal center (will be updated on resize)
        private int currentYear = 0; // The year at the center (for the vertical line)
        
        // Database and timeline data
        private long timelineId;
        private DatabaseHelper databaseHelper;
        private Dictionary<string, object> timelineData;
        private Dictionary<string, object> settingsData;
        
        // Mouse drag state
        private bool isDragging = false;
        private Point dragStartPosition;
        private double dragStartOffset = 0.0;
        
        // Drawing constants
        private const double TIMELINE_HEIGHT = 300.0;
        private const int YEAR_LABEL_INTERVAL = 5; // Label every 5 years
        private const double CENTER_LINE_HEIGHT = 2.0;
        private const double YEAR_TICK_HEIGHT = 20.0;
        private const double SUB_TICK_HEIGHT = 10.0;
        private const double CURRENT_YEAR_LINE_WIDTH = 2.0;
        
        /// <summary>
        /// Constructor for opening a specific timeline
        /// </summary>
        /// <param name="timelineId">The ID of the timeline to open</param>
        /// <param name="dbHelper">Database helper instance (can be null to create new)</param>
        public TimelineForm(long timelineId, DatabaseHelper dbHelper = null)
        {
            this.timelineId = timelineId;
            if (dbHelper == null)
            {
                this.databaseHelper = new DatabaseHelper();
                dbHelperWasCreated = true;
            }
            else
            {
                this.databaseHelper = dbHelper;
                dbHelperWasCreated = false;
            }
            
            InitializeComponent();
            this.Loaded += TimelineForm_Loaded;
            this.SizeChanged += TimelineForm_SizeChanged;
            this.Closed += TimelineForm_Closed;
        }
        
        /// <summary>
        /// Legacy constructor for backward compatibility
        /// </summary>
        public TimelineForm() : this(0, null)
        {
        }
        
        private void TimelineForm_Closed(object sender, EventArgs e)
        {
            // Only dispose if we created the database helper ourselves
            if (databaseHelper != null && dbHelperWasCreated)
            {
                databaseHelper.Dispose();
            }
        }
        
        private bool dbHelperWasCreated = false;
        
        // Configurable constraint variables
        private const double MIN_HEIGHT_PERCENT = 0.10; // 10% of window height
        private const double CANVAS_MIN_HEIGHT_PERCENT = 0.05; // 10% of window height
        private const double MIN_WIDTH_PERCENT = 0.10;  // 10% of window width
        
        // Store proportions when rows are manually resized
        private double? topRowProportion = null;
        private double? middleRowProportion = null;
        private double? bottomRowProportion = null;
        private bool rowsManuallyResized = false;

        private void TimelineForm_Loaded(object sender, RoutedEventArgs e)
        {
            LoadTimelineData();
            ApplySizeConstraints();
            
            // Subscribe to TimelineCanvas size changes to re-render when resized
            TimelineCanvas.SizeChanged += TimelineCanvas_SizeChanged;
            
            // Wait for layout to complete before calculating centers
            this.Dispatcher.BeginInvoke(new Action(() =>
            {
                centerX = TimelineCanvas.ActualWidth / 2.0;
                centerY = TimelineCanvas.ActualHeight / 2.0;
                currentYear = startYear;
                RedrawTimeline();
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        /// <summary>
        /// Handles TimelineCanvas size changes - re-renders timeline with new dimensions
        /// </summary>
        private void TimelineCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            centerX = TimelineCanvas.ActualWidth / 2.0;
            centerY = TimelineCanvas.ActualHeight / 2.0;
            RedrawTimeline();
        }

        /// <summary>
        /// Applies minimum size constraints to resizable rows and columns
        /// </summary>
        private void ApplySizeConstraints()
        {
            // Only apply constraints if window is loaded and has actual size
            if (this.ActualHeight <= 0 || this.ActualWidth <= 0)
                return;
                
            double windowHeight = this.ActualHeight;
            double windowWidth = this.ActualWidth;
            
            // Set minimum heights for resizable rows in MiddleGrid (10% of window height)
            double minHeight = windowHeight * MIN_HEIGHT_PERCENT;
            double canvasMinHeight = windowHeight * CANVAS_MIN_HEIGHT_PERCENT;
            if (ThreeColumnsRow != null) ThreeColumnsRow.MinHeight = minHeight;
            if (TimelineRow != null) TimelineRow.MinHeight = minHeight;
            if (CanvasRenderingRow != null) CanvasRenderingRow.MinHeight = canvasMinHeight;
            
            // Set minimum widths for resizable columns in ThreeColumnsGrid (10% of window width)
            double minWidth = windowWidth * MIN_WIDTH_PERCENT;
            if (PicturesColumn != null) PicturesColumn.MinWidth = minWidth;
            if (MiddleColumn != null) MiddleColumn.MinWidth = minWidth;
            if (StoryBlocksColumn != null) StoryBlocksColumn.MinWidth = minWidth;
        }
        
        /// <summary>
        /// Loads timeline data and settings from the database
        /// </summary>
        private void LoadTimelineData()
        {
            try
            {
                if (timelineId > 0)
                {
                    // Get all timelines to find the one we need
                    var allTimelines = databaseHelper.GetAllTimelines();
                    timelineData = allTimelines.FirstOrDefault(t => (long)t["id"] == timelineId);
                    
                    if (timelineData != null)
                    {
                        // Load timeline properties
                        startYear = (int)timelineData["start_year"];
                        int granularity = (int)timelineData["granularity"];
                        yearSubdivision = granularity - 1; // Convert granularity to subdivision count
                        
                        // Set window title
                        string title = (string)timelineData["title"];
                        string author = (string)timelineData["author"];
                        this.Title = $"{title} by {author}";
                        
                        // Load settings for this timeline
                        settingsData = databaseHelper.GetTimelineSettings(timelineId);
                        if (settingsData != null)
                        {
                            // Apply settings
                            tickGap = (int)settingsData["pixels_per_subtick"];
                            // TODO: Apply other settings (window size, position, etc.) as needed
                        }
                        else
                        {
                            // Use defaults if no settings found
                            tickGap = 20.0; // Default from schema
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error loading timeline data:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        
        private void TimelineForm_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ApplySizeConstraints();
            
            // If rows were manually resized, maintain proportions when window resizes
            if (rowsManuallyResized && e.NewSize.Height > 0 && e.PreviousSize.Height > 0)
            {
                // Wait for layout to update before recalculating
                this.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (topRowProportion.HasValue && middleRowProportion.HasValue && bottomRowProportion.HasValue)
                    {
                        double newTotalHeight = MiddleSectionRow.ActualHeight;
                        
                        if (newTotalHeight > 0)
                        {
                            double newTopHeight = newTotalHeight * topRowProportion.Value;
                            double newMiddleHeight = newTotalHeight * middleRowProportion.Value;
                            double newBottomHeight = newTotalHeight * bottomRowProportion.Value;
                            
                            // Apply minimum constraints
                            double minHeight = this.ActualHeight * MIN_HEIGHT_PERCENT;
                            double canvasMinHeight = this.ActualHeight * CANVAS_MIN_HEIGHT_PERCENT;
                            if (newTopHeight < minHeight) newTopHeight = minHeight;
                            if (newMiddleHeight < minHeight) newMiddleHeight = minHeight;
                            if (newBottomHeight < minHeight) newBottomHeight = canvasMinHeight;
                            
                            // Ensure total doesn't exceed available space
                            double total = newTopHeight + newMiddleHeight + newBottomHeight;
                            if (total > newTotalHeight)
                            {
                                double scale = newTotalHeight / total;
                                newTopHeight *= scale;
                                newMiddleHeight *= scale;
                                newBottomHeight *= scale;
                            }
                            
                            ThreeColumnsRow.Height = new GridLength(newTopHeight, GridUnitType.Pixel);
                            TimelineRow.Height = new GridLength(newMiddleHeight, GridUnitType.Pixel);
                            CanvasRenderingRow.Height = new GridLength(newBottomHeight, GridUnitType.Pixel);
                        }
                    }
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
            
            // TimelineCanvas_SizeChanged will handle the redraw when canvas size changes
        }
        
        /// <summary>
        /// Converts a pixel X coordinate to a year value
        /// </summary>
        private double GetYearFromPixel(double pixelX)
        {
            double pixelOffsetFromCenter = pixelX - centerX;
            double totalPixelsFromStart = pixelOffsetFromCenter + currentOffset;
            double year = startYear + (totalPixelsFromStart / tickGap);
            return year;
        }
        
        /// <summary>
        /// Converts a year value to a pixel X coordinate
        /// </summary>
        private double GetPixelFromYear(double year)
        {
            double yearOffset = year - startYear;
            double pixelOffset = yearOffset * tickGap;
            double pixelX = centerX - currentOffset + pixelOffset;
            return pixelX;
        }
        
        /// <summary>
        /// Gets the visible year range based on current viewport
        /// </summary>
        private void GetVisibleYearRange(out double minYear, out double maxYear)
        {
            double leftPixel = 0;
            double rightPixel = TimelineCanvas.ActualWidth;
            
            minYear = GetYearFromPixel(leftPixel);
            maxYear = GetYearFromPixel(rightPixel);
            
            // Add padding to ensure we draw slightly outside viewport
            double padding = 2.0;
            minYear -= padding;
            maxYear += padding;
        }
        
        /// <summary>
        /// Snaps the current offset to the nearest tick
        /// </summary>
        private void SnapToNearestTick()
        {
            double currentYearAtCenter = GetYearFromPixel(centerX);
            
            if (yearSubdivision > 0)
            {
                // Snap to nearest sub-tick
                double tickInterval = 1.0 / (yearSubdivision + 1);
                double snappedYear = Math.Round(currentYearAtCenter / tickInterval) * tickInterval;
                // Calculate offset so that snappedYear appears at centerX
                double yearOffset = snappedYear - startYear;
                currentOffset = yearOffset * tickGap;
            }
            else
            {
                // Snap to nearest year
                int snappedYear = (int)Math.Round(currentYearAtCenter);
                // Calculate offset so that snappedYear appears at centerX
                double yearOffset = snappedYear - startYear;
                currentOffset = yearOffset * tickGap;
            }
            
            currentYear = (int)Math.Round(GetYearFromPixel(centerX));
        }
        
        /// <summary>
        /// Draws the horizontal center line
        /// </summary>
        private void DrawCenterLine()
        {
            Line centerLine = new Line
            {
                X1 = 0,
                Y1 = centerY,
                X2 = TimelineCanvas.ActualWidth,
                Y2 = centerY,
                Stroke = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                StrokeThickness = CENTER_LINE_HEIGHT
            };
            TimelineCanvas.Children.Add(centerLine);
        }
        
        /// <summary>
        /// Draws the vertical line at the current year (center)
        /// </summary>
        private void DrawCurrentYearLine()
        {
            double timelineHeight = TimelineCanvas.ActualHeight > 0 ? TimelineCanvas.ActualHeight : TIMELINE_HEIGHT;
            Line currentYearLine = new Line
            {
                X1 = centerX,
                Y1 = 0,
                X2 = centerX,
                Y2 = timelineHeight,
                Stroke = new SolidColorBrush(Color.FromRgb(100, 150, 200)),
                StrokeThickness = CURRENT_YEAR_LINE_WIDTH
            };
            TimelineCanvas.Children.Add(currentYearLine);
        }
        
        /// <summary>
        /// Draws year ticks (vertical marks for each year)
        /// </summary>
        private void DrawYearTicks()
        {
            GetVisibleYearRange(out double minYear, out double maxYear);
            
            int startYearInt = (int)Math.Floor(minYear);
            int endYearInt = (int)Math.Ceiling(maxYear);
            
            for (int year = startYearInt; year <= endYearInt; year++)
            {
                double pixelX = GetPixelFromYear(year);
                
                if (pixelX >= -10 && pixelX <= TimelineCanvas.ActualWidth + 10)
                {
                    Line tick = new Line
                    {
                        X1 = pixelX,
                        Y1 = centerY - (YEAR_TICK_HEIGHT / 2),
                        X2 = pixelX,
                        Y2 = centerY + (YEAR_TICK_HEIGHT / 2),
                        Stroke = new SolidColorBrush(Color.FromRgb(150, 150, 150)),
                        StrokeThickness = 1.0
                    };
                    TimelineCanvas.Children.Add(tick);
                }
            }
        }
        
        /// <summary>
        /// Draws sub-ticks if yearSubdivision > 0
        /// </summary>
        private void DrawSubTicks()
        {
            if (yearSubdivision <= 0) return;
            
            GetVisibleYearRange(out double minYear, out double maxYear);
            
            int startYearInt = (int)Math.Floor(minYear);
            int endYearInt = (int)Math.Ceiling(maxYear);
            
            for (int year = startYearInt; year <= endYearInt; year++)
            {
                for (int sub = 1; sub <= yearSubdivision; sub++)
                {
                    double subYear = year + (sub / (double)(yearSubdivision + 1));
                    double pixelX = GetPixelFromYear(subYear);
                    
                    if (pixelX >= -10 && pixelX <= TimelineCanvas.ActualWidth + 10)
                    {
                        Line subTick = new Line
                        {
                            X1 = pixelX,
                            Y1 = centerY - (SUB_TICK_HEIGHT / 2),
                            X2 = pixelX,
                            Y2 = centerY + (SUB_TICK_HEIGHT / 2),
                            Stroke = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                            StrokeThickness = 0.5
                        };
                        TimelineCanvas.Children.Add(subTick);
                    }
                }
            }
        }
        
        /// <summary>
        /// Draws year labels every 5 years
        /// </summary>
        private void DrawYearLabels()
        {
            GetVisibleYearRange(out double minYear, out double maxYear);
            
            int startYearInt = (int)Math.Floor(minYear);
            int endYearInt = (int)Math.Ceiling(maxYear);
            
            // Round to nearest multiple of YEAR_LABEL_INTERVAL
            int labelStart = (startYearInt / YEAR_LABEL_INTERVAL) * YEAR_LABEL_INTERVAL;
            if (labelStart > startYearInt) labelStart -= YEAR_LABEL_INTERVAL;
            
            for (int year = labelStart; year <= endYearInt; year += YEAR_LABEL_INTERVAL)
            {
                double pixelX = GetPixelFromYear(year);
                
                if (pixelX >= -50 && pixelX <= TimelineCanvas.ActualWidth + 50)
                {
                    TextBlock label = new TextBlock
                    {
                        Text = year.ToString(),
                        FontSize = 12,
                        Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100))
                    };
                    
                    Canvas.SetLeft(label, pixelX - (label.ActualWidth / 2));
                    Canvas.SetTop(label, centerY + (YEAR_TICK_HEIGHT / 2) + 5);
                    
                    // Measure text to center it properly
                    label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    label.Arrange(new Rect(label.DesiredSize));
                    
                    Canvas.SetLeft(label, pixelX - (label.DesiredSize.Width / 2));
                    
                    TimelineCanvas.Children.Add(label);
                }
            }
        }
        
        /// <summary>
        /// Main drawing function - redraws all timeline elements
        /// </summary>
        private void RedrawTimeline()
        {
            TimelineCanvas.Children.Clear();
            
            // Draw in order: baseline graphics first, then other items on top
            DrawCenterLine();
            DrawSubTicks();
            DrawYearTicks();
            DrawYearLabels();
            DrawCurrentYearLine();
        }
        
        private void TimelineCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                isDragging = true;
                dragStartPosition = e.GetPosition(TimelineCanvas);
                dragStartOffset = currentOffset;
                TimelineCanvas.CaptureMouse();
            }
        }
        
        private void TimelineCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDragging)
            {
                Point currentPosition = e.GetPosition(TimelineCanvas);
                double deltaX = currentPosition.X - dragStartPosition.X;
                
                // Reverse direction: dragging left moves forward in time, right moves backward
                currentOffset = dragStartOffset - deltaX;
                RedrawTimeline();
            }
        }
        
        private void TimelineCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (isDragging)
            {
                isDragging = false;
                TimelineCanvas.ReleaseMouseCapture();
                
                // Snap to nearest tick
                SnapToNearestTick();
                RedrawTimeline();
            }
        }
        
        private void TimelineCanvas_MouseLeave(object sender, MouseEventArgs e)
        {
            if (isDragging)
            {
                isDragging = false;
                TimelineCanvas.ReleaseMouseCapture();
                
                // Snap to nearest tick
                SnapToNearestTick();
                RedrawTimeline();
            }
        }

        /// <summary>
        /// Handles Timeline splitter drag - resizes TOP (three columns) and MIDDLE (Timeline),
        /// keeping BOTTOM (Canvas) height constant
        /// Dragging UP = MIDDLE grows, TOP shrinks
        /// Dragging DOWN = MIDDLE shrinks, TOP grows
        /// </summary>
        private void TimelineSplitter_DragDelta(object sender, DragDeltaEventArgs e)
        {
            // Get current row heights
            double topHeight = ThreeColumnsRow.ActualHeight;
            double middleHeight = TimelineRow.ActualHeight;
            double bottomHeight = CanvasRenderingRow.ActualHeight;
            
            // deltaY: positive = dragging down, negative = dragging up
            double deltaY = e.VerticalChange;
            
            // When dragging UP (negative deltaY): MIDDLE grows, TOP shrinks
            // When dragging DOWN (positive deltaY): MIDDLE shrinks, TOP grows
            double newMiddleHeight = middleHeight - deltaY;
            double newTopHeight = topHeight + deltaY;
            
            // Apply minimum constraints
            double minHeight = this.ActualHeight * MIN_HEIGHT_PERCENT;
            if (newMiddleHeight < minHeight)
            {
                double adjustment = minHeight - newMiddleHeight;
                newMiddleHeight = minHeight;
                newTopHeight -= adjustment;
            }
            if (newTopHeight < minHeight)
            {
                double adjustment = minHeight - newTopHeight;
                newTopHeight = minHeight;
                newMiddleHeight -= adjustment;
            }
            // Note: BOTTOM (Canvas) height is not changed in this handler, so no canvas min check needed
            
            // Update row heights (BOTTOM stays the same)
            ThreeColumnsRow.Height = new GridLength(newTopHeight, GridUnitType.Pixel);
            TimelineRow.Height = new GridLength(newMiddleHeight, GridUnitType.Pixel);
            
            // Store proportions for window resize handling
            UpdateRowProportions();
        }

        /// <summary>
        /// Handles Canvas splitter drag - resizes TOP (three columns) and BOTTOM (Canvas),
        /// keeping MIDDLE (Timeline) height constant but moving it
        /// Dragging UP = BOTTOM grows, MIDDLE moves down, TOP shrinks
        /// Dragging DOWN = BOTTOM shrinks, MIDDLE moves up, TOP grows
        /// </summary>
        private void CanvasSplitter_DragDelta(object sender, DragDeltaEventArgs e)
        {
            // Get current row heights
            double topHeight = ThreeColumnsRow.ActualHeight;
            double middleHeight = TimelineRow.ActualHeight;
            double bottomHeight = CanvasRenderingRow.ActualHeight;
            
            // deltaY: positive = dragging down, negative = dragging up
            double deltaY = e.VerticalChange;
            
            // When dragging UP (negative deltaY): BOTTOM grows, MIDDLE stays same height but moves down, TOP shrinks
            // When dragging DOWN (positive deltaY): BOTTOM shrinks, MIDDLE stays same height but moves up, TOP grows
            double newBottomHeight = bottomHeight - deltaY;
            double newTopHeight = topHeight + deltaY;
            // middleHeight stays the same
            
            // Apply minimum constraints
            double minHeight = this.ActualHeight * MIN_HEIGHT_PERCENT;
            double canvasMinHeight = this.ActualHeight * CANVAS_MIN_HEIGHT_PERCENT;
            if (newBottomHeight < canvasMinHeight)
            {
                double adjustment = canvasMinHeight - newBottomHeight;
                newBottomHeight = canvasMinHeight;
                newTopHeight -= adjustment;
            }
            if (newTopHeight < minHeight)
            {
                double adjustment = minHeight - newTopHeight;
                newTopHeight = minHeight;
                newBottomHeight -= adjustment;
            }
            
            // Update row heights (MIDDLE height stays the same, but position changes via TOP adjustment)
            ThreeColumnsRow.Height = new GridLength(newTopHeight, GridUnitType.Pixel);
            CanvasRenderingRow.Height = new GridLength(newBottomHeight, GridUnitType.Pixel);
            // TimelineRow height remains unchanged (stays as *)
            
            // Store proportions for window resize handling
            UpdateRowProportions();
        }

        /// <summary>
        /// Updates stored proportions of the three resizable rows for maintaining proportions on window resize
        /// </summary>
        private void UpdateRowProportions()
        {
            double totalHeight = ThreeColumnsRow.ActualHeight + TimelineRow.ActualHeight + CanvasRenderingRow.ActualHeight;
            
            if (totalHeight > 0)
            {
                topRowProportion = ThreeColumnsRow.ActualHeight / totalHeight;
                middleRowProportion = TimelineRow.ActualHeight / totalHeight;
                bottomRowProportion = CanvasRenderingRow.ActualHeight / totalHeight;
                rowsManuallyResized = true;
            }
        }

    }
}

