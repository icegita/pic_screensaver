using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using PicScreenSaver.Runtime.Engine;

namespace PicScreenSaver.Runtime
{
    public partial class ScreensaverWindow : Window
    {
        private readonly ScreensaverConfig _config;
        private readonly bool _isPreview;
        private SlideEngine _engine;
        private DispatcherTimer _exitMonitor;
        private bool _isExiting = false;
        private Point _lastMousePos;

        public ScreensaverWindow(ScreensaverConfig config, bool isPreview)
            : this(config, isPreview, IntPtr.Zero)
        {
        }

        public ScreensaverWindow(ScreensaverConfig config, bool isPreview, IntPtr parentHandle)
        {
            InitializeComponent();
            _config = config;
            _isPreview = isPreview;

            if (_isPreview && parentHandle != IntPtr.Zero)
            {
                SetupPreview(parentHandle);
            }
            else
            {
                SetupFullscreen();
            }
        }

        private void SetupFullscreen()
        {
            WindowState = WindowState.Maximized;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            Background = System.Windows.Media.Brushes.Black;
            ShowInTaskbar = false;
            Topmost = true;
            Cursor = Cursors.None;

            Left = 0;
            Top = 0;
            Width = SystemParameters.PrimaryScreenWidth;
            Height = SystemParameters.PrimaryScreenHeight;
        }

        private void SetupPreview(IntPtr parentHandle)
        {
            WindowState = WindowState.Normal;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            Topmost = false;

            var helper = new WindowInteropHelper(this);
            SetParent(helper.Handle, parentHandle);

            var parentRect = new RECT();
            GetClientRect(parentHandle, out parentRect);
            Width = parentRect.Right - parentRect.Left;
            Height = parentRect.Bottom - parentRect.Top;
            Left = 0;
            Top = 0;

            // 预览模式：每秒检测父窗口是否还存在
            var parentTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            parentTimer.Tick += (s, args) =>
            {
                if (!IsWindow(parentHandle) || !IsWindowVisible(parentHandle))
                {
                    DebugLog.Write("预览父窗口消失 → 退出");
                    parentTimer.Stop();
                    ExitScreensaver();
                }
            };
            parentTimer.Start();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            DebugLog.Write($"ScreensaverWindow.Loaded preview={_isPreview} images={_config.ImageCount}");
            if (_config.ImageCount == 0)
            {
                DebugLog.Write("ImageCount=0, 直接关闭");
                Close();
                return;
            }

            _engine = new SlideEngine(_config, OutgoingImage, IncomingImage);
            _engine.Start();

            if (!_isPreview)
            {
                POINT initialPos;
                GetCursorPos(out initialPos);
                _lastMousePos = new Point(initialPos.X, initialPos.Y);

                _exitMonitor = new DispatcherTimer();
                _exitMonitor.Interval = TimeSpan.FromMilliseconds(100);
                int tickCount = 0;
                _exitMonitor.Tick += (s, args) =>
                {
                    tickCount++;
                    if (tickCount < 5) return;

                    POINT currentPos;
                    GetCursorPos(out currentPos);

                    int dx = Math.Abs(currentPos.X - (int)_lastMousePos.X);
                    int dy = Math.Abs(currentPos.Y - (int)_lastMousePos.Y);

                    if (dx > 5 || dy > 5)
                    {
                        ExitScreensaver();
                        return;
                    }
                    _lastMousePos = new Point(currentPos.X, currentPos.Y);
                };
                _exitMonitor.Start();
            }
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isPreview) return;
            POINT currentPos;
            GetCursorPos(out currentPos);
            int dx = Math.Abs(currentPos.X - (int)_lastMousePos.X);
            int dy = Math.Abs(currentPos.Y - (int)_lastMousePos.Y);
            if (dx > 5 || dy > 5) ExitScreensaver();
            _lastMousePos = new Point(currentPos.X, currentPos.Y);
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!_isPreview) ExitScreensaver();
        }

        private void Window_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (!_isPreview) ExitScreensaver();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (!_isPreview) ExitScreensaver();
        }

        private void ExitScreensaver()
        {
            if (_isExiting) return;
            _isExiting = true;

            DebugLog.Write("ExitScreensaver 被调用");

            _exitMonitor?.Stop();
            _engine?.Stop();

            OutgoingImage.Source = null;
            IncomingImage.Source = null;

            Topmost = false;
            DebugLog.Write("ExitScreensaver → Close()");
            Close();
            DebugLog.Write("ExitScreensaver → Shutdown(0)");
            Application.Current?.Shutdown(0);
        }

        [DllImport("user32.dll")]
        private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }
    }
}
