using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Newtonsoft.Json;
using PicScreenSaver.Maker.Models;

namespace PicScreenSaver.Maker.Services
{
    public class PackageBuilder
    {
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr BeginUpdateResource(string pFileName,
            [MarshalAs(UnmanagedType.Bool)] bool bDeleteExistingResources);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool UpdateResource(IntPtr hUpdate, string lpType, IntPtr lpName,
            ushort wLanguage, byte[] lpData, uint cb);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool UpdateResource(IntPtr hUpdate, ushort lpType, IntPtr lpName,
            ushort wLanguage, byte[] lpData, uint cb);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EndUpdateResource(IntPtr hUpdate,
            [MarshalAs(UnmanagedType.Bool)] bool fDiscard);

        private const string CONFIG_RESOURCE = "SSCONFIG";
        private const string IMAGE_RESOURCE  = "SSIMAGE";
        private const ushort RT_ICON = 3;
        private const ushort RT_GROUP_ICON = 14;

        public static byte[] LoadEmbeddedRuntime()
        {
            var asm = Assembly.GetExecutingAssembly();
            using (var stream = asm.GetManifestResourceStream(
                "PicScreenSaver.Maker.Resources.PicScreenSaver.Runtime.exe"))
            {
                if (stream == null)
                    throw new InvalidOperationException("嵌入的 Runtime.exe 未找到");
                var bytes = new byte[stream.Length];
                stream.Read(bytes, 0, bytes.Length);
                return bytes;
            }
        }

        public static byte[] LoadEmbeddedImage(string name)
        {
            var asm = Assembly.GetExecutingAssembly();
            using (var stream = asm.GetManifestResourceStream(
                "PicScreenSaver.Maker.Resources." + name))
            {
                if (stream == null) return null;
                var bytes = new byte[stream.Length];
                stream.Read(bytes, 0, bytes.Length);
                return bytes;
            }
        }

        // Bug修复 #2：新增 quality 参数，生成时按当前质量重新编码所有图片
        public string BuildPackage(
            byte[] runtimeTemplate,
            ScreensaverConfig config,
            List<ImageItem> images,
            string outputPath,
            int quality = 75,
            int maxWidth = 1920,
            string iconPath = null)
        {
            string tempPath = Path.Combine(Path.GetTempPath(),
                "PicScreenSaver_" + Guid.NewGuid().ToString("N") + ".exe");

            try
            {
                File.WriteAllBytes(tempPath, runtimeTemplate);

                var hUpdate = BeginUpdateResource(tempPath, false);
                if (hUpdate == IntPtr.Zero)
                    throw new InvalidOperationException(
                        "BeginUpdateResource 失败: " + Marshal.GetLastWin32Error());

                try
                {
                    // 写入配置 JSON
                    var configJson  = JsonConvert.SerializeObject(config, Formatting.None);
                    var configBytes = Encoding.UTF8.GetBytes(configJson);
                    var configName  = Marshal.StringToHGlobalUni(CONFIG_RESOURCE);
                    try
                    {
                        if (!UpdateResource(hUpdate, CONFIG_RESOURCE, configName, 0,
                            configBytes, (uint)configBytes.Length))
                            throw new InvalidOperationException(
                                "UpdateResource SSCONFIG 失败: " + Marshal.GetLastWin32Error());
                    }
                    finally { Marshal.FreeHGlobal(configName); }

                    // Bug修复 #2：按当前 quality 重新编码每张图片写入 PE
                    for (int i = 0; i < images.Count; i++)
                    {
                        // 如果源文件存在，按当前 quality 重新编码（保证质量滑块生效）
                        byte[] jpegData;
                        if (File.Exists(images[i].FilePath))
                        {
                            jpegData = ImageProcessor.GetResizedJpegBytes(
                                images[i].FilePath, quality, maxWidth);
                        }
                        else if (images[i].JpegBytes != null)
                        {
                            // 源文件不存在时退回到已缓存的数据
                            jpegData = images[i].JpegBytes;
                        }
                        else continue;

                        var resName = Marshal.StringToHGlobalUni((i + 1).ToString());
                        try
                        {
                            if (!UpdateResource(hUpdate, IMAGE_RESOURCE, resName, 0,
                                jpegData, (uint)jpegData.Length))
                                throw new InvalidOperationException(
                                    $"UpdateResource SSIMAGE {i + 1} 失败: "
                                    + Marshal.GetLastWin32Error());
                        }
                        finally { Marshal.FreeHGlobal(resName); }
                    }

                    // 注入图标
                    if (!string.IsNullOrEmpty(iconPath) && File.Exists(iconPath))
                    {
                        InjectIcoFile(hUpdate, iconPath);
                    }

                    if (!EndUpdateResource(hUpdate, false))
                        throw new InvalidOperationException(
                            "EndUpdateResource 失败: " + Marshal.GetLastWin32Error());
                }
                catch
                {
                    EndUpdateResource(hUpdate, true);
                    throw;
                }

                File.Copy(tempPath, outputPath, true);
                return outputPath;
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }

        /// <summary>
        /// 注入图标到PE资源
        /// 使用IconConverter模块处理ico解析和图片转换
        /// </summary>
        private void InjectIcoFile(IntPtr hUpdate, string iconPath)
        {
            // 使用IconConverter加载图标
            var allEntries = IconConverter.LoadIcons(iconPath);
            if (allEntries == null || allEntries.Count == 0)
                return;

            // 获取Windows 7+所需的图标
            var entries = IconConverter.GetWin7Entries(allEntries);
            if (entries == null || entries.Count == 0)
                return;

            // 写入RT_ICON资源（ID从1开始）
            var groupEntries = new List<byte>();
            groupEntries.AddRange(BitConverter.GetBytes((ushort)0));
            groupEntries.AddRange(BitConverter.GetBytes((ushort)1));
            groupEntries.AddRange(BitConverter.GetBytes((ushort)entries.Count));

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                ushort iconId = (ushort)(i + 1);

                IntPtr iconName = new IntPtr(iconId);
                if (!UpdateResource(hUpdate, RT_ICON, iconName, 0,
                    entry.Data, (uint)entry.Data.Length))
                    throw new InvalidOperationException(
                        "UpdateResource RT_ICON 失败: " + Marshal.GetLastWin32Error());

                groupEntries.Add(entry.Width);
                groupEntries.Add(entry.Height);
                groupEntries.Add(entry.ColorCount);
                groupEntries.Add(entry.Reserved);
                groupEntries.AddRange(BitConverter.GetBytes(entry.Planes));
                groupEntries.AddRange(BitConverter.GetBytes(entry.BitCount));
                groupEntries.AddRange(BitConverter.GetBytes((uint)entry.Data.Length));
                groupEntries.AddRange(BitConverter.GetBytes(iconId));
            }

            IntPtr groupName = new IntPtr(1);
            var groupData = groupEntries.ToArray();
            if (!UpdateResource(hUpdate, RT_GROUP_ICON, groupName, 0,
                groupData, (uint)groupData.Length))
                throw new InvalidOperationException(
                    "UpdateResource RT_GROUP_ICON 失败: " + Marshal.GetLastWin32Error());
        }
    }
}