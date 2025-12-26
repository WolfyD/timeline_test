using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace timeline.classes
{
    public class ItemImage
    {
        public string ImagePath { get; set; }
        public string Description { get; set; }
        public Bitmap Image { get; set; }

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
