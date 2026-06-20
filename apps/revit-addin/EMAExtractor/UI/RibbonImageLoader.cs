using System.IO;
using System.Reflection;
using System.Windows.Media.Imaging;

namespace EMAExtractor.UI
{
    public static class RibbonImageLoader
    {
        public static BitmapSource LoadPng(string fileName)
        {
            try
            {
                string assemblyFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string iconPath = Path.Combine(assemblyFolder, "Resources", "Icons", fileName);

                if (!File.Exists(iconPath))
                    return null;

                using (FileStream stream = new FileStream(iconPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    PngBitmapDecoder decoder = new PngBitmapDecoder(
                        stream,
                        BitmapCreateOptions.PreservePixelFormat,
                        BitmapCacheOption.OnLoad);

                    BitmapSource frame = decoder.Frames[0];
                    frame.Freeze();
                    return frame;
                }
            }
            catch
            {
                return null;
            }
        }
    }
}