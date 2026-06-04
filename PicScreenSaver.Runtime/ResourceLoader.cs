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
