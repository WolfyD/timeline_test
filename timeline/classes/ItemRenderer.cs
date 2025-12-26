using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace timeline.classes
{
    public class ItemRenderer(Canvas timeline, TimelineRenderer renderer)
    {
        private Canvas Timeline = timeline;
        private TimelineRenderer Renderer = renderer;

        // Configurable rendering variables
        public double AgeLineThickness = 20.0; // Thickness of age lines
        public double PeriodLineThickness = 10.0; // Thickness of period lines
        public double PeriodVerticalSpacing = 4.0; // Vertical spacing between period levels
        public double EventBubbleVerticalOffset = 0.0; // Vertical offset of event bubble from line
        public double EventCascadeSpacing = 4.0; // Vertical spacing between cascaded events
        public double EventVerticalOffset = 10.0; // Vertical offset to move bubble away from edge (for connector line)
        private Dictionary<string, int> PeriodLevelCache = new Dictionary<string, int>(); // Stable cascade levels for periods
        private Dictionary<string, int> EventLevelCache = new Dictionary<string, int>(); // Stable cascade levels keyed by item Id
        public double EventBubblePaddingVertical = 2.0; // Vertical padding inside event bubble
        public double EventBubblePaddingHorizontal = 5.0; // Horizontal padding inside event bubble
        public double EventBubbleBorderRadius = 2.0; // Border radius of event bubble
        public double EventBubbleBorderThickness = .5; // Border thickness of event bubble

        /// <summary>
        /// Renders all items on the timeline
        /// </summary>
        public void RenderItems()
        {
            if (Renderer.TimeLine == null || Renderer.TimeLine.Items == null)
                return;

            // Render items in order: Ages first (on center line), then Periods, then Events
            RenderAges();
            RenderPeriods();
            RenderEvents();
        }

        /// <summary>
        /// Converts an ItemDate to a year value (accounting for sub-year granularity)
        /// </summary>
        private double GetYearFromItemDate(ItemDate date)
        {
            if (date == null) return 0;
            
            double year = date.Year;
            if (Renderer.YearSubdivision > 0 && date.SubYear > 0)
            {
                // Add fractional year based on subtick
                year += date.SubYear / (double)(Renderer.YearSubdivision + 1);
            }
            return year;
        }

        /// <summary>
        /// Renders all Age items
        /// </summary>
        private void RenderAges()
        {
            if (Renderer.TimeLine == null || Renderer.TimeLine.Items == null)
                return;

            // Get visible year range for optimization
            Renderer.GetVisibleYearRange(out double minYear, out double maxYear);

            foreach (var item in Renderer.TimeLine.Items)
            {
                if (item.ItemType != ItemTypes.Age || item.StartDate == null || item.EndDate == null)
                    continue;

                double startYear = GetYearFromItemDate(item.StartDate);
                double endYear = GetYearFromItemDate(item.EndDate);

                // Skip if completely outside visible range
                if (endYear < minYear || startYear > maxYear)
                    continue;

                // Get pixel positions
                double startX = Renderer.GetPixelFromYear(startYear);
                double endX = Renderer.GetPixelFromYear(endYear);

                // Only render if within canvas bounds (with padding)
                if (endX >= -20 && startX <= Timeline.ActualWidth + 20)
                {
                    DrawAgeLine(startX, endX, Renderer.CenterY, item.Color);
                }
            }
        }

        /// <summary>
        /// Draws an Age line (thick line on center line)
        /// </summary>
        private void DrawAgeLine(double startX, double endX, double centerY, System.Drawing.Color color)
        {
            double halfThickness = AgeLineThickness / 2.0;

            var c = Color.FromArgb(color.A, color.R, color.G, color.B);

            Rectangle ageRect = new Rectangle
            {
                Width = Math.Abs(endX - startX),
                Height = AgeLineThickness,
                Fill = new SolidColorBrush(c), // Gray color for ages
                Stroke = new SolidColorBrush(Colors.Black),
                StrokeThickness = 1.0
            };

            Canvas.SetLeft(ageRect, Math.Min(startX, endX));
            Canvas.SetTop(ageRect, centerY - halfThickness);
            
            Timeline.Children.Add(ageRect);
        }

        /// <summary>
        /// Renders all Period items with layout algorithm
        /// </summary>
        private void RenderPeriods()
        {
            if (Renderer.TimeLine == null || Renderer.TimeLine.Items == null)
                return;

            // Get visible year range for optimization (rendering only)
            Renderer.GetVisibleYearRange(out double minYear, out double maxYear);

            // Collect all periods (full list for stable stacking)
            var allPeriods = new List<(IItem item, double startYear, double endYear, double duration)>();
            foreach (var item in Renderer.TimeLine.Items)
            {
                if (item.ItemType != ItemTypes.Period || item.StartDate == null || item.EndDate == null)
                    continue;

                double startYear = GetYearFromItemDate(item.StartDate);
                double endYear = GetYearFromItemDate(item.EndDate);
                double duration = Math.Abs(endYear - startYear);

                allPeriods.Add((item, startYear, endYear, duration));
            }

            // Sort by ItemIndex first (for stability), then by duration (shorter first)
            allPeriods = allPeriods.OrderBy(p => p.item.ItemIndex).ThenBy(p => p.duration).ToList();

            // Compute stable levels using all periods
            PeriodLevelCache = ComputePeriodLevels(allPeriods);

            // Filter visible periods for rendering
            var visiblePeriods = allPeriods.Where(p => p.endYear >= minYear && p.startYear <= maxYear).ToList();

            // Render each period at its calculated position
            foreach (var (item, startYear, endYear, duration) in visiblePeriods)
            {
                double startX = Renderer.GetPixelFromYear(startYear);
                double endX = Renderer.GetPixelFromYear(endYear);

                // Only render if within canvas bounds
                if (endX >= -20 && startX <= Timeline.ActualWidth + 20)
                {
                    int level = PeriodLevelCache.TryGetValue(item.Id, out var lvl) ? lvl : 0;
                    bool isAbove = (item.ItemIndex % 2 == 0);
                    double yPosition = ComputePeriodYPosition(isAbove, level);
                    DrawPeriodLine(startX, endX, yPosition, item.Color);
                }
            }
        }

        /// <summary>
        /// Computes stable cascade levels for periods using ItemIndex ordering and time overlap
        /// </summary>
        private Dictionary<string, int> ComputePeriodLevels(List<(IItem item, double startYear, double endYear, double duration)> periods)
        {
            var levels = new Dictionary<string, int>();

            // Order for stability
            periods = periods.OrderBy(p => p.item.ItemIndex).ThenBy(p => p.duration).ToList();

            // Split above/below based on ItemIndex parity
            var above = new List<(IItem item, double startYear, double endYear, double duration)>();
            var below = new List<(IItem item, double startYear, double endYear, double duration)>();
            foreach (var p in periods)
            {
                if ((p.item.ItemIndex % 2) == 0) above.Add(p);
                else below.Add(p);
            }

            // Within each side, prioritize shorter durations (further from center), tie-break with ItemIndex
            above = above.OrderBy(p => p.duration).ThenBy(p => p.item.ItemIndex).ToList();
            below = below.OrderBy(p => p.duration).ThenBy(p => p.item.ItemIndex).ToList();

            AssignPeriodLevels(above, levels);
            AssignPeriodLevels(below, levels);

            return levels;
        }

        /// <summary>
        /// Assign levels for one side (no parity check here)
        /// </summary>
        private void AssignPeriodLevels(List<(IItem item, double startYear, double endYear, double duration)> periods, Dictionary<string, int> levels)
        {
            var levelRanges = new List<List<(double start, double end)>>();

            foreach (var (item, startYear, endYear, duration) in periods)
            {
                int assigned = 0;
                bool placed = false;
                for (int lvl = 0; lvl < levelRanges.Count; lvl++)
                {
                    // Touching periods (end == start) are allowed; only true overlap cascades
                    bool overlaps = levelRanges[lvl].Any(r => !(endYear <= r.start || startYear >= r.end));
                    if (!overlaps)
                    {
                        assigned = lvl;
                        levelRanges[lvl].Add((startYear, endYear));
                        placed = true;
                        break;
                    }
                }

                if (!placed)
                {
                    assigned = levelRanges.Count;
                    levelRanges.Add(new List<(double start, double end)> { (startYear, endYear) });
                }

                levels[item.Id] = assigned;
            }
        }

        /// <summary>
        /// Computes the Y position for a period based on its level and side
        /// Ensures clearance from the age line and other periods.
        /// </summary>
        private double ComputePeriodYPosition(bool isAbove, int level)
        {
            // Clear the age thickness plus a spacer, then add per-level spacing
            double baseOffsetFromCenter = (AgeLineThickness / 2.0) + PeriodVerticalSpacing + (PeriodLineThickness / 2.0);
            double perLevel = PeriodLineThickness + PeriodVerticalSpacing;

            if (isAbove)
            {
                return Renderer.CenterY - (baseOffsetFromCenter + level * perLevel);
            }
            else
            {
                return Renderer.CenterY + (baseOffsetFromCenter + level * perLevel);
            }
        }

        /// <summary>
        /// Draws a Period line
        /// </summary>
        private void DrawPeriodLine(double startX, double endX, double yPosition, System.Drawing.Color color)
        {
            double halfThickness = PeriodLineThickness / 2.0;

            var c = Color.FromArgb(color.A, color.R, color.G, color.B);

            Rectangle periodRect = new Rectangle
            {
                Width = Math.Abs(endX - startX),
                Height = PeriodLineThickness,
                Fill = new SolidColorBrush(c),
                RadiusX = 4,
                RadiusY = 4
            };

            Canvas.SetLeft(periodRect, Math.Min(startX, endX));
            Canvas.SetTop(periodRect, yPosition - halfThickness);
            
            Timeline.Children.Add(periodRect);
        }

        /// <summary>
        /// Renders all Event items with layout algorithm
        /// </summary>
        private void RenderEvents()
        {
            if (Renderer.TimeLine == null || Renderer.TimeLine.Items == null)
                return;

            // Get visible year range for optimization (for drawing), but compute cascade using all events for stability
            Renderer.GetVisibleYearRange(out double minYear, out double maxYear);

            // Collect all events (for stable cascade computation)
            var allEvents = new List<(IItem item, double year, double pixelX)>();
            foreach (var item in Renderer.TimeLine.Items)
            {
                if (item.ItemType != ItemTypes.Event || item.StartDate == null)
                    continue;

                double year = GetYearFromItemDate(item.StartDate);
                double pixelX = Renderer.GetPixelFromYear(year);

                allEvents.Add((item, year, pixelX));
            }

            // Compute stable levels using all events, ordered by ItemIndex
            EventLevelCache = ComputeEventLevels(allEvents);

            // Filter visible events for rendering
            var visibleEvents = allEvents.Where(e => e.year >= minYear && e.year <= maxYear && e.pixelX >= -20 && e.pixelX <= Timeline.ActualWidth + 20).ToList();

            // First pass: Draw all connector lines
            foreach (var (item, year, pixelX) in visibleEvents)
            {
                int level = EventLevelCache.TryGetValue(item.Id, out var lvl) ? lvl : 0;
                bool isAbove = (item.ItemIndex % 2 == 0);
                double yPosition = ComputeEventYPosition(isAbove, level, item, year);
                DrawEventLine(item, pixelX, yPosition, year);
            }

            // Second pass: Draw all bubbles on top of lines
            foreach (var (item, year, pixelX) in visibleEvents)
            {
                int level = EventLevelCache.TryGetValue(item.Id, out var lvl) ? lvl : 0;
                bool isAbove = (item.ItemIndex % 2 == 0);
                double yPosition = ComputeEventYPosition(isAbove, level, item, year);
                DrawEventBubble(item, pixelX, yPosition, year);
            }
        }

        /// <summary>
        /// Computes stable cascade levels for all events using ItemIndex ordering
        /// </summary>
        private Dictionary<string, int> ComputeEventLevels(List<(IItem item, double year, double pixelX)> events)
        {
            var levels = new Dictionary<string, int>();

            // Sort events by ItemIndex for consistent ordering
            events = events.OrderBy(e => e.item.ItemIndex).ToList();

            // Separate events into above and below center line based on ItemIndex
            var eventsAbove = new List<(IItem item, double year, double pixelX)>();
            var eventsBelow = new List<(IItem item, double year, double pixelX)>();

            foreach (var evt in events)
            {
                bool isAbove = (evt.item.ItemIndex % 2 == 0);
                if (isAbove)
                    eventsAbove.Add(evt);
                else
                    eventsBelow.Add(evt);
            }

            // Assign levels for above and below independently
            AssignEventLevels(eventsAbove, levels, -1); // above
            AssignEventLevels(eventsBelow, levels, 1);  // below

            return levels;
        }

        /// <summary>
        /// Assigns cascade levels for one side (above or below)
        /// </summary>
        private void AssignEventLevels(List<(IItem item, double year, double pixelX)> events, Dictionary<string, int> levels, int direction)
        {
            // Horizontal overlap threshold in years (convert pixels using TickGap)
            const double horizontalOverlapThresholdPx = 180.0;
            double thresholdYears = Renderer.TickGap > 0 ? horizontalOverlapThresholdPx / Renderer.TickGap : 0.0;

            // Levels store event years at that level to test horizontal overlap
            var levelYears = new List<List<double>>();

            foreach (var (item, year, pixelX) in events)
            {
                // Find the first level without horizontal overlap
                int assignedLevel = 0;
                bool placed = false;
                for (int lvl = 0; lvl < levelYears.Count; lvl++)
                {
                    bool overlaps = levelYears[lvl].Any(existingYear => Math.Abs(existingYear - year) < thresholdYears);
                    if (!overlaps)
                    {
                        assignedLevel = lvl;
                        levelYears[lvl].Add(year);
                        placed = true;
                        break;
                    }
                }

                if (!placed)
                {
                    assignedLevel = levelYears.Count;
                    levelYears.Add(new List<double> { year });
                }

                levels[item.Id] = assignedLevel;
            }
        }

        /// <summary>
        /// Computes the Y position for an event based on its level and side
        /// </summary>
        private double ComputeEventYPosition(bool isAbove, int level, IItem item, double year)
        {
            var bubbleSize = CalculateEventBubbleSize(item, year);
            double bubbleHeight = bubbleSize.height;

            if (isAbove)
            {
                // Start at top, move down per level
                return EventVerticalOffset + level * (bubbleHeight + EventCascadeSpacing);
            }
            else
            {
                // Start at bottom, move up per level
                return Timeline.ActualHeight - bubbleHeight - EventVerticalOffset - level * (bubbleHeight + EventCascadeSpacing);
            }
        }

        /// <summary>
        /// Calculates cascading positions for a list of events
        /// Based on JavaScript logic: start at edge, cascade inward when overlapping
        /// </summary>
        private void CalculateEventCascade(List<(IItem item, double year, double pixelX)> events, 
            Dictionary<IItem, double> positions, double nowYearX, int direction)
        {
            // Track placed items for overlap detection (similar to JavaScript abovePlaced/belowPlaced)
            var placedItems = new List<(double x, double y, double width, double height)>();
            
            // Horizontal overlap threshold (from JavaScript: 150px)
            const double horizontalOverlapThreshold = 180.0;
            
            foreach (var (item, year, pixelX) in events)
            {
                // Calculate bubble dimensions
                var bubbleSize = CalculateEventBubbleSize(item, year);
                double bubbleWidth = bubbleSize.width;
                double bubbleHeight = bubbleSize.height;
                
                // Start at the edge (furthest from center)
                // Above: start at top (y = 0), Below: start at bottom (y = ActualHeight - bubbleHeight)
                double yPosition = direction < 0 
                    ? 0  // Above: start at top
                    : Timeline.ActualHeight - bubbleHeight; // Below: start at bottom
                
                // Cascade inward if overlapping (similar to JavaScript while loop)
                // Note: We check overlap using base positions, then apply verticalOffset at the end
                bool foundOverlap = true;
                while (foundOverlap)
                {
                    foundOverlap = false;
                    foreach (var placed in placedItems)
                    {
                        // Check horizontal overlap (similar to JavaScript: Math.abs(placed.x - itemX) < 150)
                        if (Math.Abs(placed.x - pixelX) < horizontalOverlapThreshold)
                        {
                            // Calculate placed item's base position (remove verticalOffset for comparison)
                            double placedBaseY = direction < 0 
                                ? placed.y - EventVerticalOffset  // Above: subtract offset
                                : placed.y + EventVerticalOffset; // Below: add offset
                            
                            // Check vertical overlap using base positions
                            if (direction < 0) // Above: cascade down
                            {
                                if (yPosition + bubbleHeight > placedBaseY && yPosition < placedBaseY + placed.height)
                                {
                                    yPosition = placedBaseY + placed.height + EventCascadeSpacing;
                                    foundOverlap = true;
                                }
                            }
                            else // Below: cascade up
                            {
                                if (yPosition < placedBaseY + placed.height && yPosition + bubbleHeight > placedBaseY)
                                {
                                    yPosition = placedBaseY - bubbleHeight - EventCascadeSpacing;
                                    foundOverlap = true;
                                }
                            }
                        }
                    }
                }
                
                // Adjust y so the connector line connects closer to the edge nearest the timeline center
                // Similar to JavaScript: verticalOffset = 10
                double finalY = yPosition;
                if (direction < 0) // Above
                {
                    finalY = yPosition + EventVerticalOffset;
                }
                else // Below
                {
                    finalY = yPosition - EventVerticalOffset;
                }
                
                positions[item] = finalY;
                placedItems.Add((pixelX, finalY, bubbleWidth, bubbleHeight));
            }
        }

        /// <summary>
        /// Calculates the size of an event bubble
        /// </summary>
        private (double width, double height) CalculateEventBubbleSize(IItem item, double year)
        {
            string text = $"{item.Title ?? ""} - {year}";
            
            // Create a temporary TextBlock to measure text
            TextBlock tempText = new TextBlock
            {
                Text = text,
                Padding = new Thickness(EventBubblePaddingHorizontal, EventBubblePaddingVertical, 
                                       EventBubblePaddingHorizontal, EventBubblePaddingVertical)
            };
            
            tempText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            tempText.Arrange(new Rect(tempText.DesiredSize));
            
            return (tempText.DesiredSize.Width, tempText.DesiredSize.Height);
        }

        /// <summary>
        /// Checks if two event bubbles overlap
        /// </summary>
        private bool EventBubblesOverlap(double x1, double y1, double w1, double h1,
                                         double x2, double y2, double w2, double h2)
        {
            return x1 < x2 + w2 && x1 + w1 > x2 &&
                   y1 < y2 + h2 && y1 + h1 > y2;
        }

        /// <summary>
        /// Draws the connector line for an Event
        /// Line connects from center to bubble edge
        /// </summary>
        private void DrawEventLine(IItem item, double pixelX, double yPosition, double year)
        {
            // Calculate bubble size to determine where line should end
            var bubbleSize = CalculateEventBubbleSize(item, year);
            
            // Determine where the line should end (at the bubble edge)
            // If bubble is above center, line ends at bottom edge of bubble
            // If bubble is below center, line ends at top edge of bubble
            double lineEndY;
            if (yPosition < Renderer.CenterY)
            {
                // Above center: line ends at bottom edge of bubble
                lineEndY = yPosition + bubbleSize.height;
            }
            else
            {
                // Below center: line ends at top edge of bubble
                lineEndY = yPosition;
            }
            
            // Draw vertical line from center line to bubble edge
            Line eventLine = new Line
            {
                X1 = pixelX,
                Y1 = Renderer.CenterY,
                X2 = pixelX,
                Y2 = lineEndY,
                Stroke = new SolidColorBrush(Colors.Gray),
                StrokeThickness = 1.0
            };
            Timeline.Children.Add(eventLine);
        }

        /// <summary>
        /// Draws the text bubble for an Event
        /// Bubbles are drawn after lines so they render on top
        /// </summary>
        private void DrawEventBubble(IItem item, double pixelX, double yPosition, double year)
        {
            // Calculate bubble position relative to NowYear
            double nowYearX = Timeline.ActualWidth / 2.0;
            var bubbleSize = CalculateEventBubbleSize(item, year);
            
            // Bubble X position: left or right of vertical line based on position relative to NowYear
            double bubbleX = pixelX < nowYearX 
                ? pixelX - bubbleSize.width - EventBubbleVerticalOffset + 4  // Left of line
                : pixelX + EventBubbleVerticalOffset - 4;                        // Right of line
            
            // Draw text bubble
            Border bubble = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(Colors.Black),
                BorderThickness = new Thickness(EventBubbleBorderThickness),
                CornerRadius = new CornerRadius(EventBubbleBorderRadius),
                Padding = new Thickness(EventBubblePaddingHorizontal, EventBubblePaddingVertical,
                                       EventBubblePaddingHorizontal, EventBubblePaddingVertical)
            };
            
            TextBlock bubbleText = new TextBlock
            {
                Text = $"{item.Title ?? ""} - {year}",
                TextWrapping = TextWrapping.NoWrap
            };
            
            bubble.Child = bubbleText;
            
            Canvas.SetLeft(bubble, bubbleX);
            Canvas.SetTop(bubble, yPosition);
            
            Timeline.Children.Add(bubble);
        }
    }
}
