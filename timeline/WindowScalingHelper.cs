using System.Windows;
using System.Windows.Media;

namespace timeline
{
    /// <summary>
    /// Helper class for applying consistent window scaling across all forms
    /// </summary>
    public static class WindowScalingHelper
    {
        /// <summary>
        /// Gets the global scaling factor from App resources
        /// </summary>
        public static double GetGlobalScaleFactor()
        {
            var scaleTransform = Application.Current.Resources["GlobalWindowScaleTransform"] as ScaleTransform;
            return scaleTransform?.ScaleX ?? 1.0;
        }

        /// <summary>
        /// Applies the global scaling transform to a window's main content container
        /// Call this in code-behind if you need to apply scaling programmatically
        /// </summary>
        /// <param name="window">The window to apply scaling to</param>
        /// <param name="contentContainer">The main content container (usually a Grid or Panel)</param>
        public static void ApplyScalingToWindow(Window window, FrameworkElement contentContainer)
        {
            var scaleTransform = Application.Current.Resources["GlobalWindowScaleTransform"] as ScaleTransform;
            if (scaleTransform != null && contentContainer != null)
            {
                contentContainer.LayoutTransform = scaleTransform;
            }
        }
    }
}

