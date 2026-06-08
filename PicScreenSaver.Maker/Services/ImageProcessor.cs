using System;
using System.IO;
using System.Windows.Media.Imaging;
using PicScreenSaver.Maker.Models;

namespace PicScreenSaver.Maker.Services
{
    public class ImageProcessor
    {
        public ImageItem ProcessImage(string filePath, int quality, int maxWidth = 1920)
        {
            var item = new ImageItem
            {
                FilePath = filePath,
                FileName = Path.GetFileName(filePath)
            };

            try
            {
                item.OriginalSize = new FileInfo(filePath).Length;

                // Bug修复 #3：先用 DecodePixelWidth=480 低分辨率解码读取元信息和缩略图
                // 避免把 4K 原图整张解码进内存导致 OOM 或失败
                var thumbnailBitmap = new BitmapImage();
                thumbnailBitmap.BeginInit();
                thumbnailBitmap.CacheOption     = BitmapCacheOption.OnLoad;
                thumbnailBitmap.UriSource       = new Uri(filePath, UriKind.Absolute);
                thumbnailBitmap.DecodePixelWidth = 480;  // 只解码到 480px 宽，内存占用极小
                thumbnailBitmap.EndInit();
                thumbnailBitmap.Freeze();

                // 用低分辨率版本读取宽高（需要通过实际文件获取真实分辨率）
                // 用完整解码版本获取原始尺寸
                var metaBitmap = new BitmapImage();
                metaBitmap.BeginInit();
                metaBitmap.CacheOption     = BitmapCacheOption.OnLoad;
                metaBitmap.UriSource       = new Uri(filePath, UriKind.Absolute);
                // 不限制解码尺寸，只读 metadata（CreateOptions 加速读取）
                metaBitmap.CreateOptions   = BitmapCreateOptions.DelayCreation;
                metaBitmap.EndInit();

                item.OriginalWidth  = metaBitmap.PixelWidth;
                item.OriginalHeight = metaBitmap.PixelHeight;

                // 缩略图直接用低分辨率解码结果，已经是 480px 宽
                item.Thumbnail = thumbnailBitmap;

                // Bug修复 #2：不在这里生成 JpegBytes，改为调用 ReEncodeWithQuality()
                // 这里做一次默认 quality 的编码，仅用于初始体积估算显示
                item.JpegBytes     = EncodeToJpeg(filePath, quality, maxWidth);
                item.CompressedSize = item.JpegBytes.Length;
            }
            catch
            {
                item.OriginalWidth  = 0;
                item.OriginalHeight = 0;
                item.OriginalSize   = 0;
                item.CompressedSize = 0;
                item.JpegBytes      = null;
            }

            return item;
        }

        // Bug修复 #2：新增方法，按指定 quality 重新编码单张图片
        // 生成时调用此方法确保使用当前滑块的 quality 值
        public byte[] ReEncodeWithQuality(string filePath, int quality, int maxWidth = 1920)
        {
            return EncodeToJpeg(filePath, quality, maxWidth);
        }

        // Bug修复 #2：批量重新编码所有图片（质量滑块变化时刷新体积估算）
        public void UpdateAllJpegBytes(System.Collections.Generic.List<ImageItem> images, int quality, int maxWidth = 1920, System.Threading.CancellationToken ct = default)
        {
            foreach (var item in images)
            {
                if (ct.IsCancellationRequested) return;
                if (!File.Exists(item.FilePath)) continue;
                try
                {
                    var bytes = EncodeToJpeg(item.FilePath, quality, maxWidth);
                    if (ct.IsCancellationRequested) return;
                    item.JpegBytes      = bytes;
                    item.CompressedSize = bytes.Length;
                }
                catch { }
            }
        }

        // 核心编码方法：从原始文件路径读取 → 降采样 → JPEG 编码
        private byte[] EncodeToJpeg(string filePath, int quality, int maxWidth = 1920)
        {
            var source = new BitmapImage();
            source.BeginInit();
            source.CacheOption = BitmapCacheOption.OnLoad;
            source.UriSource   = new Uri(filePath, UriKind.Absolute);
            source.EndInit();
            source.Freeze();

            int width  = source.PixelWidth;
            int height = source.PixelHeight;

            if (maxWidth > 0)
            {
                int maxHeight = maxWidth * 9 / 16;
                if (width > maxWidth || height > maxHeight)
                {
                    double scale = Math.Min((double)maxWidth / width, (double)maxHeight / height);
                    width  = (int)(width  * scale);
                    height = (int)(height * scale);
                }
            }

            var target = new RenderTargetBitmap(width, height, 96, 96,
                System.Windows.Media.PixelFormats.Pbgra32);
            var visual = new System.Windows.Media.DrawingVisual();
            using (var ctx = visual.RenderOpen())
                ctx.DrawImage(source, new System.Windows.Rect(0, 0, width, height));
            target.Render(visual);

            var encoder = new JpegBitmapEncoder { QualityLevel = quality };
            encoder.Frames.Add(BitmapFrame.Create(target));

            using (var ms = new MemoryStream())
            {
                encoder.Save(ms);
                return ms.ToArray();
            }
        }

        // 保留此方法供外部调用（PackageBuilder 不再直接用 JpegBytes，改用此方法）
        public static byte[] GetResizedJpegBytes(string filePath, int quality,
            int maxWidth = 1920, int maxHeight = 0)
        {
            var source = new BitmapImage();
            source.BeginInit();
            source.CacheOption = BitmapCacheOption.OnLoad;
            source.UriSource   = new Uri(filePath, UriKind.Absolute);
            source.EndInit();
            source.Freeze();

            int width  = source.PixelWidth;
            int height = source.PixelHeight;

            if (maxWidth > 0)
            {
                if (maxHeight <= 0) maxHeight = maxWidth * 9 / 16;
                if (width > maxWidth || height > maxHeight)
                {
                    double scale = Math.Min((double)maxWidth / width, (double)maxHeight / height);
                    width  = (int)(width  * scale);
                    height = (int)(height * scale);
                }
            }

            var target = new RenderTargetBitmap(width, height, 96, 96,
                System.Windows.Media.PixelFormats.Pbgra32);
            var visual = new System.Windows.Media.DrawingVisual();
            using (var ctx = visual.RenderOpen())
                ctx.DrawImage(source, new System.Windows.Rect(0, 0, width, height));
            target.Render(visual);

            var encoder = new JpegBitmapEncoder { QualityLevel = quality };
            encoder.Frames.Add(BitmapFrame.Create(target));

            using (var ms = new MemoryStream())
            {
                encoder.Save(ms);
                return ms.ToArray();
            }
        }
    }
}