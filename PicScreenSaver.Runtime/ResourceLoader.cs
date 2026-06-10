using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Json;
using System.Text;

namespace PicScreenSaver.Runtime
{
    public static class ResourceLoader
    {
        private const string CONFIG_RESOURCE = "SSCONFIG";
        private const string IMAGE_RESOURCE = "SSIMAGE";

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr FindResource(IntPtr hModule, IntPtr lpName, IntPtr lpType);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LoadResource(IntPtr hModule, IntPtr hResInfo);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LockResource(IntPtr hResData);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint SizeofResource(IntPtr hModule, IntPtr hResInfo);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindResource(IntPtr hModule, string lpName, string lpType);

        public static ScreensaverConfig LoadConfig()
        {
            // 先从PE资源读取嵌入配置（这是最新的）
            var embeddedConfig = LoadConfigFromResources();
            
            // 再检查本地 AppData（设置页保存的）
            string configPath = GetConfigFilePath();
            if (File.Exists(configPath))
            {
                try
                {
                    string fileJson = File.ReadAllText(configPath, Encoding.UTF8);
                    var fileConfig = DeserializeConfig(fileJson);
                    if (fileConfig != null)
                    {
                        // 如果本地配置的ImageCount与嵌入配置不同，使用嵌入配置
                        // 这确保了图片数量始终正确
                        if (embeddedConfig != null && fileConfig.ImageCount != embeddedConfig.ImageCount)
                        {
                            return embeddedConfig;
                        }
                        return fileConfig;
                    }
                }
                catch { }
            }

            return embeddedConfig;
        }

        private static ScreensaverConfig LoadConfigFromResources()
        {
            try
            {
                var hModule = GetModuleHandle(null);
                var hRes = FindResource(hModule, CONFIG_RESOURCE, CONFIG_RESOURCE);
                if (hRes == IntPtr.Zero) return null;

                var hData = LoadResource(hModule, hRes);
                if (hData == IntPtr.Zero) return null;

                var pRes = LockResource(hData);
                if (pRes == IntPtr.Zero) return null;

                uint size = SizeofResource(hModule, hRes);
                var bytes = new byte[size];
                Marshal.Copy(pRes, bytes, 0, (int)size);

                string json = Encoding.UTF8.GetString(bytes);
                return DeserializeConfig(json);
            }
            catch
            {
                return null;
            }
        }

        public static void SaveConfig(ScreensaverConfig config)
        {
            try
            {
                string configPath = GetConfigFilePath();
                string dir = Path.GetDirectoryName(configPath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                using (var ms = new MemoryStream())
                {
                    var serializer = new DataContractJsonSerializer(typeof(ScreensaverConfig));
                    serializer.WriteObject(ms, config);
                    string json = Encoding.UTF8.GetString(ms.ToArray());
                    File.WriteAllText(configPath, json, Encoding.UTF8);
                }
            }
            catch { }
        }

        private static string GetConfigFilePath()
        {
            string appData = Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData);
            string scrName = Path.GetFileNameWithoutExtension(
                System.Reflection.Assembly.GetExecutingAssembly().Location);
            return Path.Combine(appData, "PicScreenSaver", scrName + "_config.json");
        }

        public static byte[] GetImageBytes(int index)
        {
            var hModule = GetModuleHandle(null);
            var hRes = FindResource(hModule, (index + 1).ToString(), IMAGE_RESOURCE);
            if (hRes == IntPtr.Zero) return null;

            var hData = LoadResource(hModule, hRes);
            if (hData == IntPtr.Zero) return null;

            var pRes = LockResource(hData);
            if (pRes == IntPtr.Zero) return null;

            uint size = SizeofResource(hModule, hRes);
            var bytes = new byte[size];
            Marshal.Copy(pRes, bytes, 0, (int)size);
            return bytes;
        }

        private static ScreensaverConfig DeserializeConfig(string json)
        {
            using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                var serializer = new DataContractJsonSerializer(typeof(ScreensaverConfig));
                return (ScreensaverConfig)serializer.ReadObject(ms);
            }
        }
    }
}
