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

            // 找到最接近且不小于目标尺寸的条目（优先选择清晰度足够的）
            var entry = entries.FirstOrDefault(e => GetEffectiveWidth(e.Width) == size) ??
                       entries.Where(e => GetEffectiveWidth(e.Width) >= size)
                              .OrderBy(e => GetEffectiveWidth(e.Width))
                              .FirstOrDefault() ??
                       entries.OrderBy(e => Math.Abs(GetEffectiveWidth(e.Width) - size)).FirstOrDefault();

            if (entry == null || entry.Data == null)
                return null;

            try
            {
                // 检查是否是PNG格式（头部 89 50 4E 47）
                if (entry.Data.Length >= 4 && 
                    entry.Data[0] == 0x89 && entry.Data[1] == 0x50 && 
                    entry.Data[2] == 0x4E && entry.Data[3] == 0x47)
                {
                    // PNG格式，直接加载
                    using (var ms = new MemoryStream(entry.Data))
                    {
                        return new Bitmap(ms);
                    }
                }
                else
                {
                    // BMP格式，使用更可靠的直接像素复制方法
                    int width = GetEffectiveWidth(entry.Width);
                    return CreateBitmapFromDib(entry.Data, width, entry.Height);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetIconPreview异常: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 获取实际宽度（ICO格式中0表示256）
        /// </summary>
        private static int GetEffectiveWidth(byte width)
        {
            return width == 0 ? 256 : width;
        }

        /// <summary>
        /// 从DIB数据创建Bitmap（使用直接像素复制方法）
        /// </summary>
        private static Bitmap CreateBitmapFromDib(byte[] dibData, int width, int height)
        {
            try
            {
                if (dibData == null || dibData.Length < 40)
                    return null;

                int biSize = BitConverter.ToInt32(dibData, 0);
                short bitCount = BitConverter.ToInt16(dibData, 14);
                int bytesPerPixel = (bitCount + 7) / 8;
                
                // 计算像素数据起始位置（跳过 BITMAPINFOHEADER 和颜色表）
                int pixelOffset = biSize;
                
                // 如果有颜色表，跳过它
                // 颜色表大小 = biClrUsed * 4，如果 biClrUsed = 0 且 bitCount <= 8，则颜色表大小 = 2^bitCount * 4
                if (bitCount <= 8)
                {
                    int clrUsed = BitConverter.ToInt32(dibData, 32);
                    if (clrUsed == 0)
                        clrUsed = 1 << bitCount;
                    pixelOffset += clrUsed * 4;
                }
                
                // 像素数据大小 = width * height * bytesPerPixel
                int pixelDataSize = width * height * bytesPerPixel;
                
                // 检查是否有足够的数据
                if (dibData.Length < pixelOffset + pixelDataSize)
                {
                    System.Diagnostics.Debug.WriteLine($"DIB数据不足: 需要 {pixelOffset + pixelDataSize} 字节，实际 {dibData.Length} 字节");
                    return null;
                }

                // 创建位图（直接使用像素数据）
                var bmp = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                var data = bmp.LockBits(new Rectangle(0, 0, width, height), 
                    System.Drawing.Imaging.ImageLockMode.WriteOnly, 
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                try
                {
                    // ICO 中的像素数据是 BGRA 格式，从上到下排列
                    int srcOffset = pixelOffset;
                    int dstStride = data.Stride;
                    System.IntPtr dstPtr = data.Scan0;

                    for (int y = 0; y < height; y++)
                    {
                        // 计算目标指针位置（注意：Bitmap 是从下到上存储的）
                        System.IntPtr rowPtr = dstPtr + (height - 1 - y) * dstStride;
                        
                        // 复制一行像素数据
                        System.Runtime.InteropServices.Marshal.Copy(
                            dibData, srcOffset, rowPtr, width * bytesPerPixel);
                        
                        srcOffset += width * bytesPerPixel;
                    }
                }
                finally
                {
                    bmp.UnlockBits(data);
                }

                return bmp;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CreateBitmapFromDib异常: {ex.Message}");
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
                var match = allEntries.FirstOrDefault(e => GetEffectiveWidth(e.Width) == targetSize) ??
                           allEntries.Where(e => GetEffectiveWidth(e.Width) >= targetSize)
                                    .OrderBy(e => GetEffectiveWidth(e.Width))
                                    .FirstOrDefault() ??
                           allEntries.OrderBy(e => Math.Abs(GetEffectiveWidth(e.Width) - targetSize)).FirstOrDefault();

                if (match != null && !selected.Contains(match))
                    selected.Add(match);
            }

            return selected.Count > 0 ? selected : allEntries.Take(4).ToList();
        }
    }
}
