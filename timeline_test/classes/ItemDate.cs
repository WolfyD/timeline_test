namespace timeline_test.classes
{
    public class ItemDate
    {
        public int Year { get; set; }
        public int SubYear { get; set; }
        public int OriginalSubYear { get; set; }

        /// <summary>
        /// Proper constructor
        /// </summary>
        /// <param name="year"></param>
        /// <param name="subYear"></param>
        /// <param name="originalSubYear"></param>
        public ItemDate(int year, int subYear, int originalSubYear)
        {
            Year = year;
            SubYear = subYear;
            OriginalSubYear = originalSubYear;
        }

        /// <summary>
        /// Default constructor with 0 value
        /// </summary>
        public ItemDate()
        {
            Year = 0;
            SubYear = 0;
            OriginalSubYear = 0;
        }
    }
}
