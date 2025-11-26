using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace timeline_test.classes
{
    public class Timeline
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Author { get; set; }
        public int StartYear { get; set; }
        public int Granularity { get; set; }
        public string ImagePath { get; set; }
        public Bitmap Image { get; set; }
        public List<IItem> Items { get; set; }
        public TimelineSettings Settings { get; set; }

        public Timeline(List<IItem> items, TimelineSettings settings)
        {
            Items = items ?? new List<IItem>();
            Settings = settings ?? new TimelineSettings();
        }

        public Timeline()
        {
            Items = new List<IItem>();
            Settings = new TimelineSettings();
        }

        public void AddItem(IItem item)
        {
            if (Items == null)
            {
                Items = new List<IItem>();
            }
            Items.Add(item);
        }

        

        /// <summary>
        /// Load image as bitmap from ImagePath if an image exists there
        /// </summary>
        public void setImage()
        {
            if (!string.IsNullOrEmpty(ImagePath))
            {
                try
                {
                    if (File.Exists(ImagePath) && IsImage(new FileInfo(ImagePath).Extension))
                    {
                        Image = new Bitmap(ImagePath);
                    }
                }
                catch (Exception)
                {
                    Image = null; // Failed to load image
                }
            }
        }

        /// <summary>
        /// Checks if extension is an image format
        /// </summary>
        /// <param name="extension"></param>
        /// <returns>Boolean, true if extension is image, false otherwise</returns>
        public bool IsImage(string extension)
        {
            string[] imageExtensions = { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tiff" };
            return imageExtensions.Contains(extension.ToLower());
        }
    }
}
