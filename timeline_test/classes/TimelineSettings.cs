using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace timeline_test.classes
{
    public class TimelineSettings
    {
        //TODO: Setting stuff comes here
        // The following fields are examples only, and may change
        public int StartingYear { get; set; }
        public string FontFamily { get; set; }
        public int FontSize { get; set; }
        public int SubyearValuesBetweenYears { get; set; }
        public bool DisplayDarkMode { get; set; }
        public Dictionary<int, string> ImportantDates { get; set; }
        /*
         Example:
        [
            {"1900": "the king is born"},
            {"1918": "the war ends"},
            {"1939": "the new war starts"},
            {"1945": "the new war ends"}    
        ]
         */

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
