using System;
using System.Windows;

namespace PicScreenSaver.Runtime
{
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            var config = ResourceLoader.LoadConfig() ?? new ScreensaverConfig
            {
                Version = "1.4",
                DisplayDuration = 5.0,
                TransitionDuration = 1.2,
                ShuffleImages = true,
                SelectedEffects = new[] { "Fade", "SlideLeft", "CrossFade" },
                ImageCount = 3,
                CreatedBy = "PicScreenSaver"
            };

            // 解析命令行参数：支持 /s /c /p:12345 -s -c
            string raw = args.Length > 0 ? args[0].ToLower().TrimStart('/', '-') : "";
            string command = raw.Length > 0 ? raw.Split(':')[0].Substring(0, 1) : "";

            // 无参数：直接弹出设置窗口（Windows 对桌面 .scr 右键不传参）
            if (command == "")
            {
                ShowConfigDialog(config);
                return;
            }

            if (command == "p")
            {
                IntPtr parentHandle = IntPtr.Zero;
                if (raw.Contains(":"))
                {
                    string afterColon = raw.Substring(raw.IndexOf(':') + 1);
                    IntPtrTryParse(afterColon, out parentHandle);
                }
                else if (args.Length >= 2)
                {
                    IntPtrTryParse(args[1], out parentHandle);
                }

                if (parentHandle != IntPtr.Zero)
                    RunPreview(parentHandle, config);
                else
                    RunScreensaver(false, config);
            }
            else if (command == "c")
            {
                ShowConfigDialog(config);
            }
            else if (command == "t")
            {
                RunScreensaver(false, config);
            }
            else
            {
                RunScreensaver(false, config);
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
            window.Closed += (s, e) => app.Shutdown();
            app.Run(window);
            Environment.Exit(0);
        }

        private static void RunPreview(IntPtr parentHandle, ScreensaverConfig config)
        {
            var app = new Application();
            app.ShutdownMode = ShutdownMode.OnLastWindowClose;
            var window = new ScreensaverWindow(config, true, parentHandle);
            window.Closed += (s, e) => app.Shutdown();
            app.Run(window);
            Environment.Exit(0);
        }

        private static void ShowConfigDialog(ScreensaverConfig config)
        {
            var app = new Application();
            app.ShutdownMode = ShutdownMode.OnLastWindowClose;
            var dialog = new ConfigDialog(config);
            dialog.Closed += (s, e) => app.Shutdown();
            app.Run(dialog);
            Environment.Exit(0);
        }
    }
}
