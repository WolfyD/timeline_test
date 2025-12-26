using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;

namespace timeline.classes
{
    public class TimelineRenderer
    {
        public Canvas Timeline;
        public double CenterX;
        public double CenterY;
        public double CENTER_LINE_HEIGHT;
        public double CURRENT_YEAR_LINE_WIDTH;
        public double TIMELINE_HEIGHT;
        public int YEAR_LABEL_INTERVAL;
        public double YEAR_TICK_HEIGHT;
        public double SUB_TICK_HEIGHT;
        public int YearSubdivision;
        public int StartYear;
        public double TickGap;
        public double CurrentOffset;
        public ItemRenderer ItemRenderer;
        public Timeline TimeLine;

        public TimelineRenderer(Canvas timeline, 
                                double centerX, 
                                double centerY, 
                                double centerLineHeight, 
                                double currentYearLineWidth, 
                                double timelineHeight, 
                                int yearLabelInterval,
                                double yearTickHeight,
                                double subTickHeight, 
                                int yearSubdivision, 
                                int startYear,
                                double tickGap,
                                double currentOffset,
                                Timeline timeLine)
        {
            Timeline = timeline;
            CenterX = centerX;
            CenterY = centerY;
            CENTER_LINE_HEIGHT = centerLineHeight;
            CURRENT_YEAR_LINE_WIDTH = currentYearLineWidth;
            TIMELINE_HEIGHT = timelineHeight;
            YEAR_LABEL_INTERVAL = yearLabelInterval;
            YEAR_TICK_HEIGHT = yearTickHeight;
            SUB_TICK_HEIGHT = subTickHeight;
            YearSubdivision = yearSubdivision;
            StartYear = startYear;
            TickGap = tickGap;
            CurrentOffset = currentOffset;
            TimeLine = timeLine;
            ItemRenderer = new ItemRenderer(timeline, this);
        }

        /// <summary>
        /// Draws the horizontal center line
        /// </summary>
        public void DrawCenterLine()
        {
            Line centerLine = new Line
            {
                X1 = 0,
                Y1 = CenterY,
                X2 = Timeline.ActualWidth,
                Y2 = CenterY,
                Stroke = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                StrokeThickness = CENTER_LINE_HEIGHT
            };
            Timeline.Children.Add(centerLine);
        }

        public void DrawNowLine()
        {
            var x = Timeline.ActualWidth / 2;

            Line nowLine = new Line
            {
                X1 = x,
                Y1 = 0,
                X2 = x,
                Y2 = Timeline.ActualHeight,
                Stroke = new SolidColorBrush(Color.FromArgb(1, 255, 0, 0)),
                StrokeThickness = 1
            };
            Timeline.Children.Add(nowLine);
        }

        /// <summary>
        /// Draws the vertical line at the current year (center)
        /// </summary>
        public void DrawCurrentYearLine()
        {
            double timelineHeight = Timeline.ActualHeight > 0 ? Timeline.ActualHeight : TIMELINE_HEIGHT;
            Line currentYearLine = new Line
            {
                X1 = CenterX,
                Y1 = 0,
                X2 = CenterX,
                Y2 = timelineHeight,
                Stroke = new SolidColorBrush(Color.FromRgb(100, 150, 200)),
                StrokeThickness = CURRENT_YEAR_LINE_WIDTH
            };
            Timeline.Children.Add(currentYearLine);
        }

        /// <summary>
        /// Draws year ticks (vertical marks for each year)
        /// </summary>
        public void DrawYearTicks()
        {
            GetVisibleYearRange(out double minYear, out double maxYear);

            int startYearInt = (int)Math.Floor(minYear);
            int endYearInt = (int)Math.Ceiling(maxYear);

            for (int year = startYearInt; year <= endYearInt; year++)
            {
                double pixelX = GetPixelFromYear(year);

                if (pixelX >= -10 && pixelX <= Timeline.ActualWidth + 10)
                {
                    Line tick = new Line
                    {
                        X1 = pixelX,
                        Y1 = CenterY - (YEAR_TICK_HEIGHT / 2),
                        X2 = pixelX,
                        Y2 = CenterY + (YEAR_TICK_HEIGHT / 2),
                        Stroke = new SolidColorBrush(Color.FromRgb(150, 150, 150)),
                        StrokeThickness = 1.0
                    };
                    Timeline.Children.Add(tick);
                }
            }
        }

        /// <summary>
        /// Draws sub-ticks if yearSubdivision > 0
        /// </summary>
        public void DrawSubTicks()
        {
            if (YearSubdivision <= 0) return;

            GetVisibleYearRange(out double minYear, out double maxYear);

            int startYearInt = (int)Math.Floor(minYear);
            int endYearInt = (int)Math.Ceiling(maxYear);

            for (int year = startYearInt; year <= endYearInt; year++)
            {
                for (int sub = 1; sub <= YearSubdivision; sub++)
                {
                    double subYear = year + (sub / (double)(YearSubdivision + 1));
                    double pixelX = GetPixelFromYear(subYear);

                    if (pixelX >= -10 && pixelX <= Timeline.ActualWidth + 10)
                    {
                        Line subTick = new Line
                        {
                            X1 = pixelX,
                            Y1 = CenterY - (SUB_TICK_HEIGHT / 2),
                            X2 = pixelX,
                            Y2 = CenterY + (SUB_TICK_HEIGHT / 2),
                            Stroke = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                            StrokeThickness = 0.5
                        };
                        Timeline.Children.Add(subTick);
                    }
                }
            }
        }

        /// <summary>
        /// Draws year labels every 5 years
        /// </summary>
        public void DrawYearLabels()
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

                if (pixelX >= -50 && pixelX <= Timeline.ActualWidth + 50)
                {
                    TextBlock label = new TextBlock
                    {
                        Text = year.ToString(),
                        FontSize = 12,
                        Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100))
                    };

                    Canvas.SetLeft(label, pixelX - (label.ActualWidth / 2));
                    Canvas.SetTop(label, CenterY + (YEAR_TICK_HEIGHT / 2) + 5);

                    // Measure text to center it properly
                    label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    label.Arrange(new Rect(label.DesiredSize));

                    Canvas.SetLeft(label, pixelX - (label.DesiredSize.Width / 2));

                    Timeline.Children.Add(label);
                }
            }
        }

        /// <summary>
        /// Gets the visible year range based on current viewport
        /// </summary>
        public void GetVisibleYearRange(out double minYear, out double maxYear)
        {
            double leftPixel = 0;
            double rightPixel = Timeline.ActualWidth;

            minYear = GetYearFromPixel(leftPixel);
            maxYear = GetYearFromPixel(rightPixel);

            // Add padding to ensure we draw slightly outside viewport
            double padding = 2.0;
            minYear -= padding;
            maxYear += padding;
        }

        /// <summary>
        /// Converts a pixel X coordinate to a year value
        /// </summary>
        public double GetYearFromPixel(double pixelX)
        {
            double pixelOffsetFromCenter = pixelX - CenterX;
            double totalPixelsFromStart = pixelOffsetFromCenter + CurrentOffset;
            double year = StartYear + (totalPixelsFromStart / TickGap);
            return year;
        }

        /// <summary>
        /// Converts a year value to a pixel X coordinate
        /// </summary>
        public double GetPixelFromYear(double year)
        {
            double yearOffset = year - StartYear;
            double pixelOffset = yearOffset * TickGap;
            double pixelX = CenterX - CurrentOffset + pixelOffset;
            return pixelX;
        }

        /// <summary>
        /// Updates the center X coordinate
        /// </summary>
        public void UpdateCenterX(double centerX)
        {
            CenterX = centerX;
        }

        /// <summary>
        /// Updates the center Y coordinate
        /// </summary>
        public void UpdateCenterY(double centerY)
        {
            CenterY = centerY;
        }

        /// <summary>
        /// Updates both center coordinates
        /// </summary>
        public void UpdateCenters(double centerX, double centerY)
        {
            CenterX = centerX;
            CenterY = centerY;
        }

        /// <summary>
        /// Updates the start year
        /// </summary>
        public void UpdateStartYear(int startYear)
        {
            StartYear = startYear;
        }

        /// <summary>
        /// Updates the tick gap
        /// </summary>
        public void UpdateTickGap(double tickGap)
        {
            TickGap = tickGap;
        }

        /// <summary>
        /// Updates the year subdivision
        /// </summary>
        public void UpdateYearSubdivision(int yearSubdivision)
        {
            YearSubdivision = yearSubdivision;
        }

        /// <summary>
        /// Updates the current offset without redrawing
        /// </summary>
        public void UpdateCurrentOffset(double currentOffset)
        {
            CurrentOffset = currentOffset;
        }

        /// <summary>
        /// Updates the timeline data
        /// </summary>
        public void UpdateTimeline(Timeline timeline)
        {
            TimeLine = timeline;
        }

        /// <summary>
        /// Main drawing function - redraws all timeline elements
        /// </summary>
        public void RedrawTimeline(double currentOffset)
        {
            CurrentOffset = currentOffset;
            Timeline.Children.Clear();

            // Draw in order: baseline graphics first, then other items on top
            DrawCenterLine();
            DrawNowLine();
            DrawSubTicks();
            DrawYearTicks();
            DrawYearLabels();
            DrawCurrentYearLine();
            
            // Render items on top of timeline graphics
            ItemRenderer.RenderItems();
        }
    }
}
