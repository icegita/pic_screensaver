using System;
using System.Windows;

namespace PicScreenSaver.Runtime
{
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            var config = ResourceLoader.LoadConfig();
            if (config == null)
            {
                MessageBox.Show("无法加载配置文件。", "PicScreenSaver", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string command = args.Length > 0 ? args[0].ToLower().TrimStart('/') : "s";

            switch (command)
            {
                case "s":
                    RunScreensaver(false, config);
                    break;
                case "c":
                    ShowConfigDialog(config);
                    break;
                case "p":
                    if (args.Length >= 2)
                    {
                        IntPtr parentHandle;
                        if (IntPtrTryParse(args[1], out parentHandle))
                            RunPreview(parentHandle, config);
                        else
                            RunScreensaver(false, config);
                    }
                    else
                        RunScreensaver(false, config);
                    break;
                default:
                    RunScreensaver(false, config);
                    break;
            }
        }

        private static bool IntPtrTryParse(string s, out IntPtr result)
        {
            try
            {
                long value = Convert.ToInt64(s, 10);
                result = new IntPtr(value);
                return true;
            }
            catch
            {
                result = IntPtr.Zero;
                return false;
            }
        }

        private static void RunScreensaver(bool preview, ScreensaverConfig config)
        {
            var app = new Application();
            app.ShutdownMode = ShutdownMode.OnLastWindowClose;
            var window = new ScreensaverWindow(config, preview);
            app.Run(window);
        }

        private static void RunPreview(IntPtr parentHandle, ScreensaverConfig config)
        {
            var app = new Application();
            app.ShutdownMode = ShutdownMode.OnLastWindowClose;
            var window = new ScreensaverWindow(config, true, parentHandle);
            app.Run(window);
        }

        private static void ShowConfigDialog(ScreensaverConfig config)
        {
            var app = new Application();
            app.ShutdownMode = ShutdownMode.OnLastWindowClose;
            var dialog = new ConfigDialog(config);
            app.Run(dialog);
        }
    }
}
