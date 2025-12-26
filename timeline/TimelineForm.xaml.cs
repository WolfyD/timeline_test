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
using timeline.classes;

namespace timeline
{
    /// <summary>
    /// Interaction logic for TimelineForm.xaml
    /// </summary>
    public partial class TimelineForm : Window
    {

        public TimelineRenderer Renderer;

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
        private Timeline timeLine;

        // Mouse drag state
        private bool isDragging = false;
        private Point dragStartPosition;
        private double dragStartOffset = 0.0;
        
        // Momentum/inertia state
        private bool useDragMomentum = false; // Enable/disable drag momentum
        private double momentumVelocity = 0.0; // Pixels per second
        private System.Windows.Threading.DispatcherTimer momentumTimer;
        private const double FRICTION = 0.95; // Friction coefficient (0.92 = 8% reduction per frame)
        private const double MIN_VELOCITY = 10; // Stop when velocity is below this threshold
        private const double MAX_VELOCITY_AGE = 0.1; // Maximum age of velocity sample in seconds (100ms)
        private Point lastMousePosition;
        private DateTime lastMouseMoveTime;
        private List<(double velocity, DateTime time)> velocityHistory = new List<(double, DateTime)>();
        
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
            // Renderer will be created after LoadTimelineData() in TimelineForm_Loaded
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

            this.timeLine = databaseHelper.GetCompleteTimeline(this.timelineId);

            // Create renderer after timeline data is loaded (so it has correct startYear, tickGap, etc.)
            Renderer = new TimelineRenderer(TimelineCanvas, centerX, centerY, CENTER_LINE_HEIGHT, CURRENT_YEAR_LINE_WIDTH, TIMELINE_HEIGHT, YEAR_LABEL_INTERVAL, YEAR_TICK_HEIGHT,
                SUB_TICK_HEIGHT, yearSubdivision, startYear, tickGap, currentOffset, this.timeLine);
            
            // Subscribe to TimelineCanvas size changes to re-render when resized
            TimelineCanvas.SizeChanged += TimelineCanvas_SizeChanged;
            
            // Wait for layout to complete before calculating centers
            this.Dispatcher.BeginInvoke(new Action(() =>
            {
                centerX = TimelineCanvas.ActualWidth / 2.0;
                centerY = TimelineCanvas.ActualHeight / 2.0;
                
                // Ensure renderer has the latest values
                Renderer.UpdateCenters(centerX, centerY);
                Renderer.UpdateStartYear(startYear);
                Renderer.UpdateTickGap(tickGap);
                Renderer.UpdateYearSubdivision(yearSubdivision);
                
                // Calculate initial offset so that startYear appears at the center
                // If we want startYear at centerX, then: startYear = GetYearFromPixel(centerX)
                // startYear = startYear + ((centerX - centerX + currentOffset) / tickGap)
                // startYear = startYear + (currentOffset / tickGap)
                // So currentOffset should be 0 to show startYear at center
                currentOffset = 0.0;
                currentYear = startYear;
                
                Renderer.RedrawTimeline(currentOffset);
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        /// <summary>
        /// Handles TimelineCanvas size changes - re-renders timeline with new dimensions
        /// </summary>
        private void TimelineCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            centerX = TimelineCanvas.ActualWidth / 2.0;
            centerY = TimelineCanvas.ActualHeight / 2.0;
            Renderer.UpdateCenters(centerX, centerY);
            Renderer.RedrawTimeline(currentOffset);
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
                        
                        // Set window title and header labels
                        string title = (string)timelineData["title"];
                        string author = (string)timelineData["author"];
                        this.Title = $"{title} by {author}";
                        
                        // Update header TextBlocks with loaded data
                        if (TitleTextBlock != null)
                        {
                            TitleTextBlock.Text = title;
                        }
                        if (AuthorTextBlock != null)
                        {
                            AuthorTextBlock.Text = author;
                        }
                        
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
        /// Snaps the current offset to the nearest tick
        /// </summary>
        private void SnapToNearestTick()
        {
            // Ensure renderer has the latest values
            Renderer.UpdateCenters(centerX, centerY);
            Renderer.UpdateStartYear(startYear);
            Renderer.UpdateTickGap(tickGap);
            Renderer.UpdateYearSubdivision(yearSubdivision);
            
            // Get the current year at the center using the renderer
            double currentYearAtCenter = Renderer.GetYearFromPixel(centerX);
            
            int snappedYear;
            if (yearSubdivision > 0)
            {
                // Snap to nearest sub-tick
                double tickInterval = 1.0 / (yearSubdivision + 1);
                double snappedYearDouble = Math.Round(currentYearAtCenter / tickInterval) * tickInterval;
                snappedYear = (int)Math.Round(snappedYearDouble);
            }
            else
            {
                // Snap to nearest year
                snappedYear = (int)Math.Round(currentYearAtCenter);
            }
            
            // Calculate offset so that snappedYear appears at centerX
            // Formula: year = StartYear + ((pixelX - CenterX + CurrentOffset) / TickGap)
            // At centerX: snappedYear = StartYear + ((centerX - CenterX + currentOffset) / TickGap)
            // snappedYear = StartYear + (currentOffset / TickGap)
            // currentOffset = (snappedYear - StartYear) * TickGap
            double yearOffset = snappedYear - startYear;
            currentOffset = yearOffset * tickGap;
            
            // Update the renderer with the new offset
            Renderer.UpdateCurrentOffset(currentOffset);
            
            // The current year is the snapped year
            currentYear = snappedYear;
        }
        
        
        
        private void TimelineCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                // Stop any existing momentum
                StopMomentum();
                
                isDragging = true;
                dragStartPosition = e.GetPosition(TimelineCanvas);
                lastMousePosition = dragStartPosition;
                lastMouseMoveTime = DateTime.Now;
                dragStartOffset = currentOffset;
                velocityHistory.Clear();
                TimelineCanvas.CaptureMouse();
            }
        }
        
        private void TimelineCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDragging)
            {
                Point currentPosition = e.GetPosition(TimelineCanvas);
                DateTime currentTime = DateTime.Now;
                
                // Calculate delta from start position
                double deltaX = currentPosition.X - dragStartPosition.X;
                
                // Reverse direction: dragging left moves forward in time, right moves backward
                currentOffset = dragStartOffset - deltaX;
                
                // Ensure renderer has the latest center values before redrawing
                Renderer.UpdateCenters(centerX, centerY);
                Renderer.RedrawTimeline(currentOffset);
                
                // Calculate velocity for momentum (only if momentum is enabled)
                if (useDragMomentum)
                {
                    double timeDelta = (currentTime - lastMouseMoveTime).TotalSeconds;
                    if (timeDelta > 0 && timeDelta < 0.5) // Only use recent samples (within 500ms)
                    {
                        double positionDelta = currentPosition.X - lastMousePosition.X;
                        // Reverse direction for timeline (left = positive velocity)
                        double velocity = -positionDelta / timeDelta;
                        
                        // Store velocity sample with timestamp
                        velocityHistory.Add((velocity, currentTime));
                        
                        // Remove old samples (older than MAX_VELOCITY_AGE)
                        velocityHistory.RemoveAll(v => (currentTime - v.time).TotalSeconds > MAX_VELOCITY_AGE);
                    }
                }
                
                lastMousePosition = currentPosition;
                lastMouseMoveTime = currentTime;
            }
        }
        
        private void TimelineCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (isDragging)
            {
                isDragging = false;
                TimelineCanvas.ReleaseMouseCapture();
                
                if (useDragMomentum)
                {
                    // Calculate average velocity from recent samples (within MAX_VELOCITY_AGE)
                    DateTime now = DateTime.Now;
                    var recentVelocities = velocityHistory
                        .Where(v => (now - v.time).TotalSeconds <= MAX_VELOCITY_AGE)
                        .Select(v => v.velocity)
                        .ToList();
                    
                    if (recentVelocities.Count > 0)
                    {
                        // Use average of recent velocities, weighted towards more recent samples
                        momentumVelocity = recentVelocities.Average();
                    }
                    else
                    {
                        momentumVelocity = 0.0;
                    }
                    
                    // Start momentum if velocity is significant
                    if (Math.Abs(momentumVelocity) > MIN_VELOCITY)
                    {
                        StartMomentum();
                    }
                    else
                    {
                        // Snap to nearest tick if no momentum
                        SnapToNearestTick();
                        Renderer.RedrawTimeline(currentOffset);
                    }
                    
                    // Clear velocity history
                    velocityHistory.Clear();
                }
                else
                {
                    // If momentum is disabled, just snap to nearest tick
                    SnapToNearestTick();
                    Renderer.RedrawTimeline(currentOffset);
                    velocityHistory.Clear();
                }
            }
        }
        
        private void TimelineCanvas_MouseLeave(object sender, MouseEventArgs e)
        {
            if (isDragging)
            {
                isDragging = false;
                TimelineCanvas.ReleaseMouseCapture();
                
                if (useDragMomentum)
                {
                    // Calculate average velocity from recent samples (within MAX_VELOCITY_AGE)
                    DateTime now = DateTime.Now;
                    var recentVelocities = velocityHistory
                        .Where(v => (now - v.time).TotalSeconds <= MAX_VELOCITY_AGE)
                        .Select(v => v.velocity)
                        .ToList();
                    
                    if (recentVelocities.Count > 0)
                    {
                        // Use average of recent velocities
                        momentumVelocity = recentVelocities.Average();
                    }
                    else
                    {
                        momentumVelocity = 0.0;
                    }
                    
                    // Start momentum if velocity is significant
                    if (Math.Abs(momentumVelocity) > MIN_VELOCITY)
                    {
                        StartMomentum();
                    }
                    else
                    {
                        // Snap to nearest tick if no momentum
                        SnapToNearestTick();
                        Renderer.RedrawTimeline(currentOffset);
                    }
                    
                    // Clear velocity history
                    velocityHistory.Clear();
                }
                else
                {
                    // If momentum is disabled, just snap to nearest tick
                    SnapToNearestTick();
                    Renderer.RedrawTimeline(currentOffset);
                    velocityHistory.Clear();
                }
            }
        }

        #region Momentum

        /// <summary>
        /// Starts momentum scrolling based on the drag velocity
        /// </summary>
        private void StartMomentum()
        {
            // Use CompositionTarget.Rendering for smoother animation (runs at display refresh rate)
            if (momentumTimer == null)
            {
                momentumTimer = new System.Windows.Threading.DispatcherTimer();
                momentumTimer.Interval = TimeSpan.FromMilliseconds(8); // ~120 FPS for smoother animation
                momentumTimer.Tick += MomentumTimer_Tick;
            }
            
            momentumTimer.Start();
        }

        /// <summary>
        /// Stops momentum scrolling
        /// </summary>
        private void StopMomentum()
        {
            if (momentumTimer != null)
            {
                momentumTimer.Stop();
            }
            momentumVelocity = 0.0;
        }

        /// <summary>
        /// Timer tick handler for momentum scrolling - applies velocity with friction
        /// </summary>
        private void MomentumTimer_Tick(object sender, EventArgs e)
        {
            if (Math.Abs(momentumVelocity) < MIN_VELOCITY)
            {
                // Velocity is too low, stop momentum and snap to nearest tick
                StopMomentum();
                SnapToNearestTick();
                Renderer.RedrawTimeline(currentOffset);
                return;
            }
            
            // Apply velocity (convert from pixels per second to pixels per frame)
            // Timer runs at ~120 FPS (8ms intervals), so divide by 120
            double frameDelta = momentumVelocity / 120.0;
            // Add frameDelta to continue in the same direction as the drag
            currentOffset += frameDelta;
            
            // Apply friction
            momentumVelocity *= FRICTION;

            Renderer.RedrawTimeline(currentOffset);
        }

        #endregion

        #region Resizing

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

        #endregion

        /// <summary>
        /// Handles hamburger menu button click - opens the menu popup
        /// </summary>
        private void HamburgerMenuButton_Click(object sender, RoutedEventArgs e)
        {
            if (HamburgerMenuPopup != null)
            {
                HamburgerMenuPopup.IsOpen = !HamburgerMenuPopup.IsOpen;
            }
        }

        public void JumpToYear(int year)
        {

        }

        private void TimelineCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta > 0)
            {
                
            }
            else
            {

            }
        }
    }
}

