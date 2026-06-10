using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;

namespace PicScreenSaver.Maker.Services
{
    /// <summary>
    /// 图标转换模块 - 独立处理ico相关转换，避免影响主程序
    /// 支持ico文件直接解析，以及PNG/BMP/JPG等图片自动转换为多尺寸ico
    /// </summary>
    public static class IconConverter
    {
        /// <summary>
        /// 图标条目数据
        /// </summary>
        public class IconEntry
        {
            public byte Width;
            public byte Height;
            public byte ColorCount;
            public byte Reserved;
            public ushort Planes;
            public ushort BitCount;
            public uint DataSize;
            public byte[] Data;
        }

        /// <summary>
        /// Windows 7+ 支持的图标尺寸
        /// </summary>
        private static readonly int[] TargetSizes = { 16, 32, 48, 256 };

        /// <summary>
        /// 从文件加载图标条目列表
        /// 支持ico文件，以及PNG/BMP/JPG等图片文件
        /// </summary>
        public static List<IconEntry> LoadIcons(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return null;

                var iconBytes = File.ReadAllBytes(filePath);
                if (iconBytes.Length < 6)
                    return null;

                // 检查是否是ico文件
                ushort reserved = BitConverter.ToUInt16(iconBytes, 0);
                ushort iconType = BitConverter.ToUInt16(iconBytes, 2);

                if (reserved == 0 && iconType == 1)
                {
                    // 是ico文件，直接解析
                    return ParseIcoFile(iconBytes);
                }
                else
                {
                    // 不是ico文件，当作图片处理
                    return ConvertImageToIco(filePath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载图标失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 解析ico文件
        /// </summary>
        private static List<IconEntry> ParseIcoFile(byte[] iconBytes)
        {
            ushort imageCount = BitConverter.ToUInt16(iconBytes, 4);
            if (imageCount == 0)
                return null;

            var entries = new List<IconEntry>();
            for (int i = 0; i < imageCount; i++)
            {
                int offset = 6 + (i * 16);
                if (offset + 16 > iconBytes.Length)
                    break;

                var entry = new IconEntry
                {
                    Width = iconBytes[offset],
                    Height = iconBytes[offset + 1],
                    ColorCount = iconBytes[offset + 2],
                    Reserved = iconBytes[offset + 3],
                    Planes = BitConverter.ToUInt16(iconBytes, offset + 4),
                    BitCount = BitConverter.ToUInt16(iconBytes, offset + 6),
                    DataSize = BitConverter.ToUInt32(iconBytes, offset + 8)
                };

                uint dataOffset = BitConverter.ToUInt32(iconBytes, offset + 12);
                if (dataOffset + entry.DataSize <= iconBytes.Length)
                {
                    entry.Data = new byte[entry.DataSize];
                    Array.Copy(iconBytes, (int)dataOffset, entry.Data, 0, (int)entry.DataSize);
                    entries.Add(entry);
                }
            }

            return entries.Count > 0 ? entries : null;
        }

        /// <summary>
        /// 将图片文件转换为多尺寸ico
        /// </summary>
        private static List<IconEntry> ConvertImageToIco(string imagePath)
        {
            var entries = new List<IconEntry>();

            using (var original = Image.FromFile(imagePath))
            {
                foreach (var size in TargetSizes)
                {
                    var entry = ConvertToIconEntry(original, size);
                    if (entry != null)
                        entries.Add(entry);
                }
            }

            return entries.Count > 0 ? entries : null;
        }

        /// <summary>
        /// 将图片转换为指定尺寸的IconEntry（PNG格式）
        /// </summary>
        private static IconEntry ConvertToIconEntry(Image original, int size)
        {
            try
            {
                using (var resized = new Bitmap(size, size))
                {
                    using (var g = Graphics.FromImage(resized))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.SmoothingMode = SmoothingMode.HighQuality;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        g.DrawImage(original, 0, 0, size, size);
                    }

                    using (var ms = new MemoryStream())
                    {
                        resized.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                        var pngData = ms.ToArray();

                        return new IconEntry
                        {
                            Width = (byte)size,
                            Height = (byte)size,
                            ColorCount = 0,
                            Reserved = 0,
                            Planes = 1,
                            BitCount = 32,
                            DataSize = (uint)pngData.Length,
                            Data = pngData
                        };
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 获取指定尺寸的图标，用于预览
        /// </summary>
        public static Bitmap GetIconPreview(List<IconEntry> entries, int size)
        {
            if (entries == null || entries.Count == 0)
                return null;

            // 找到最接近的尺寸
            var entry = entries.FirstOrDefault(e => e.Width == size) ??
                       entries.OrderBy(e => Math.Abs(e.Width - size)).FirstOrDefault();

            if (entry == null || entry.Data == null)
                return null;

            try
            {
                using (var ms = new MemoryStream(entry.Data))
                {
                    return new Bitmap(ms);
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 获取Windows 7+所需的图标条目（16, 32, 48, 256）
        /// </summary>
        public static List<IconEntry> GetWin7Entries(List<IconEntry> allEntries)
        {
            if (allEntries == null || allEntries.Count == 0)
                return null;

            var selected = new List<IconEntry>();

            foreach (var targetSize in TargetSizes)
            {
                var match = allEntries.FirstOrDefault(e => e.Width == targetSize) ??
                           allEntries.OrderBy(e => Math.Abs(e.Width - targetSize)).FirstOrDefault();

                if (match != null && !selected.Contains(match))
                    selected.Add(match);
            }

            return selected.Count > 0 ? selected : allEntries.Take(4).ToList();
        }
    }
}
