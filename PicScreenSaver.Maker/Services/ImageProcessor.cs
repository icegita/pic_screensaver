using System;
using System.IO;
using System.Windows.Media.Imaging;
using PicScreenSaver.Maker.Models;

namespace PicScreenSaver.Maker.Services
{
    public class ImageProcessor
    {
        private const int MaxWidth = 1920;
        private const int MaxHeight = 1080;

        public ImageItem ProcessImage(string filePath, int quality)
        {
            var item = new ImageItem
            {
                FilePath = filePath,
                FileName = System.IO.Path.GetFileName(filePath)
            };

            try
            {
                var originalBytes = File.ReadAllBytes(filePath);
                item.OriginalSize = originalBytes.Length;

                using (var ms = new MemoryStream(originalBytes))
                {
                    var original = new BitmapImage();
                    original.BeginInit();
                    original.CacheOption = BitmapCacheOption.OnLoad;
                    original.StreamSource = ms;
                    original.EndInit();
                    original.Freeze();

                    item.OriginalWidth = original.PixelWidth;
                    item.OriginalHeight = original.PixelHeight;

                    var thumbnail = CreateThumbnail(original, 480, 300);
                    item.Thumbnail = thumbnail;

                    var jpegBytes = EncodeToJpeg(original, quality);
                    item.JpegBytes = jpegBytes;
                    item.CompressedSize = jpegBytes.Length;
                }
            }
            catch
            {
                item.OriginalWidth = 0;
                item.OriginalHeight = 0;
                item.OriginalSize = 0;
                item.CompressedSize = 0;
                item.JpegBytes = null;
            }

            return item;
        }

        private BitmapImage CreateThumbnail(BitmapImage source, int maxWidth, int maxHeight)
        {
            double scale = Math.Min((double)maxWidth / source.PixelWidth, (double)maxHeight / source.PixelHeight);
            if (scale > 1.0) scale = 1.0;

            int thumbWidth = (int)(source.PixelWidth * scale);
            int thumbHeight = (int)(source.PixelHeight * scale);

            var group = new System.Windows.Media.DrawingGroup();
            System.Windows.Media.RenderOptions.SetBitmapScalingMode(group, System.Windows.Media.BitmapScalingMode.HighQuality);
            group.Children.Add(new System.Windows.Media.ImageDrawing(source, new System.Windows.Rect(0, 0, thumbWidth, thumbHeight)));

            var drawingVisual = new System.Windows.Media.DrawingVisual();
            using (var context = drawingVisual.RenderOpen())
            {
                context.DrawDrawing(group);
            }

            var formattedBitmap = new RenderTargetBitmap(thumbWidth, thumbHeight, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            formattedBitmap.Render(drawingVisual);

            var bitmap = new BitmapImage();
            using (var ms = new MemoryStream())
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(formattedBitmap));
                encoder.Save(ms);
                ms.Position = 0;

                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = ms;
                bitmap.EndInit();
                bitmap.Freeze();
            }

            return bitmap;
        }

        private byte[] EncodeToJpeg(BitmapImage source, int quality)
        {
            int width = source.PixelWidth;
            int height = source.PixelHeight;

            if (width > MaxWidth || height > MaxHeight)
            {
                double scale = Math.Min((double)MaxWidth / width, (double)MaxHeight / height);
                width = (int)(width * scale);
                height = (int)(height * scale);
            }

            var target = new RenderTargetBitmap(width, height, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            var visual = new System.Windows.Media.DrawingVisual();

            using (var context = visual.RenderOpen())
            {
                context.DrawImage(source, new System.Windows.Rect(0, 0, width, height));
            }

            target.Render(visual);

            var encoder = new JpegBitmapEncoder();
            encoder.QualityLevel = quality;
            encoder.Frames.Add(BitmapFrame.Create(target));

            using (var ms = new MemoryStream())
            {
                encoder.Save(ms);
                return ms.ToArray();
            }
        }

        public static byte[] GetResizedJpegBytes(BitmapImage source, int quality, int maxWidth = 1920, int maxHeight = 1080)
        {
            int width = source.PixelWidth;
            int height = source.PixelHeight;

            if (width > maxWidth || height > maxHeight)
            {
                double scale = Math.Min((double)maxWidth / width, (double)maxHeight / height);
                width = (int)(width * scale);
                height = (int)(height * scale);
            }

            var target = new RenderTargetBitmap(width, height, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            var visual = new System.Windows.Media.DrawingVisual();

            using (var context = visual.RenderOpen())
            {
                context.DrawImage(source, new System.Windows.Rect(0, 0, width, height));
            }

            target.Render(visual);

            var encoder = new JpegBitmapEncoder();
            encoder.QualityLevel = quality;
            encoder.Frames.Add(BitmapFrame.Create(target));

            using (var ms = new MemoryStream())
            {
                encoder.Save(ms);
                return ms.ToArray();
            }
        }
    }
}
