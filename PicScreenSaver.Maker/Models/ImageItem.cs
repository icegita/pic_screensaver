using System.Windows.Media.Imaging;

namespace PicScreenSaver.Maker.Models
{
    public class ImageItem
    {
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public int OriginalWidth { get; set; }
        public int OriginalHeight { get; set; }
        public long OriginalSize { get; set; }
        public BitmapImage Thumbnail { get; set; }
        public byte[] JpegBytes { get; set; }
        public long CompressedSize { get; set; }
        public int DisplayOrder { get; set; }

        public string SizeText => FormatSize(OriginalSize);
        public string ResolutionText => $"{OriginalWidth}x{OriginalHeight}";
        public string CompressedSizeText => FormatSize(CompressedSize);
        public double CompressionRatio => OriginalSize > 0 ? (1.0 - (double)CompressedSize / OriginalSize) * 100 : 0;

        private string FormatSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            return $"{bytes / (1024.0 * 1024.0):F1} MB";
        }
    }
}
