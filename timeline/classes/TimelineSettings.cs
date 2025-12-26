using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace timeline.classes
{
    public class TimelineSettings
    {
        public long Id { get; set; }
        public long TimelineId { get; set; }
        public string FontFamily { get; set; }
        public double FontSizeScale { get; set; }
        public int PixelsPerSubtick { get; set; }
        public string CustomCSS { get; set; }
        public bool UseCustomCSS { get; set; }
        public bool IsFullScreen { get; set; }
        public bool ShowGuides { get; set; }
        public int WindowSizeX { get; set; }
        public int WindowSizeY { get; set; }
        public int WindowPositionX { get; set; }
        public int WindowPositionY { get; set; }
        public bool UseCustomScaling { get; set; }
        public double CustomScale { get; set; }
        public int DisplayRadius {  get; set; }
        public string CanvasSettings { get; set; }
        public string UpdatedAt { get; set; }

        /// <summary>
        /// Returns default timeline settings
        /// </summary>
        /// <returns>TimelineSettings for specific timelines</returns>
        public TimelineSettings getDefaultTimelineSettings()
        {
            //TODO: Return default settings
            return null;
        }

        /// <summary>
        /// Returns default main form settings
        /// </summary>
        /// <returns>TimelineSettings for main form</returns>
        public TimelineSettings getDefaultMainFormSettings()
        {
            //TODO: Return default main form settings
            return null;
        }
    }
}
