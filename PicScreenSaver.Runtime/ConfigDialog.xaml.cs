using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using PicScreenSaver.Runtime.Engine;
using PicScreenSaver.Runtime.Engine.Transitions;

namespace PicScreenSaver.Runtime
{
    public partial class ConfigDialog : Window
    {
        private readonly ScreensaverConfig _config;
        private static readonly string[] AllEffects = new[]
        {
            "Fade", "FadeBlack", "FadeWhite", "CrossFade", "FadeBlur",
            "SlideLeft", "SlideRight", "SlideUp", "SlideDown",
            "ZoomInFade", "ZoomOutFade", "ZoomIn", "ZoomOut", "CrossZoom",
            "WipeLeft", "WipeRight", "WipeUp", "WipeDown",
            "PushLeft", "PushUp", "PushRight", "PushDown",
            "RotateCW", "RotateCCW",
            "BlindsH", "BlindsV",
            "CircleReveal", "DiamondReveal",
            "Checkerboard",
            "RadialWipe"
        };

        private readonly System.Collections.Generic.HashSet<string> _selectedEffects =
            new System.Collections.Generic.HashSet<string>();

        private static readonly string[] EffectDescriptions = new[]
        {
            "旧图渐隐，新图渐现——最经典的过渡效果",
            "旧图淡至黑场，新图从黑场淡入",
            "旧图淡至白场，新图从白场淡入",
            "新旧图叠加交叉淡变",
            "旧图模糊淡出，新图清晰淡入",
            "旧图静止，新图从右侧滑入",
            "旧图静止，新图从左侧滑入",
            "旧图静止，新图从下方滑入",
            "旧图静止，新图从上方滑入",
            "放大同时淡入",
            "缩小同时淡出",
            "新图从 1.2x 缩小到 1.0x 切入",
            "旧图从 1.0x 放大到 1.2x 淡出",
            "旧图放大淡出的同时新图缩小淡入——电影感切换",
            "遮罩从左向右展开，逐渐露出新图",
            "遮罩从右向左展开，逐渐露出新图",
            "遮罩从上向下展开，逐渐露出新图",
            "遮罩从下向上展开，逐渐露出新图",
            "新旧图同步向左平移（翻页感）",
            "新旧图同步向上平移（翻页感）",
            "新旧图同步向右平移（翻页感）",
            "新旧图同步向下平移（翻页感）",
            "新图顺时针旋转切入",
            "新图逆时针旋转切入",
            "水平百叶窗式揭开",
            "垂直百叶窗式揭开",
            "圆形遮罩从中心扩大展开",
            "菱形遮罩从中心扩大展开",
            "棋盘格小块逐个展开揭示新图",
            "时钟式扇形扫过展开新图"
        };

        private Storyboard _previewStoryboard;

        private static BitmapSource LoadImageOrDefault(int resIndex, string fallbackColor)
        {
            try
            {
                var bytes = ResourceLoader.GetImageBytes(resIndex);
                if (bytes != null)
                {
                    var img = new BitmapImage();
                    using (var ms = new MemoryStream(bytes))
                    {
                        img.BeginInit();
                        img.CacheOption = BitmapCacheOption.OnLoad;
                        img.StreamSource = ms;
                        img.EndInit();
                        img.Freeze();
                    }
                    return img;
                }
            }
            catch { }

            var color = (Color)ColorConverter.ConvertFromString(fallbackColor);
            var group = new DrawingGroup();
            group.Children.Add(new GeometryDrawing(
                new SolidColorBrush(color), null,
                new RectangleGeometry(new Rect(0, 0, 252, 189))));
            var visual = new DrawingVisual();
            using (var context = visual.RenderOpen()) { context.DrawDrawing(group); }
            var target = new RenderTargetBitmap(252, 189, 96, 96, PixelFormats.Pbgra32);
            target.Render(visual);
            return target;
        }

        public ConfigDialog(ScreensaverConfig config)
        {
            InitializeComponent();
            _config = config;

            // 设置窗口图标
            try
            {
                string exeDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                string iconPath = System.IO.Path.Combine(exeDir, "img", "sys.png");
                if (System.IO.File.Exists(iconPath))
                    Icon = new System.Windows.Media.Imaging.BitmapImage(new Uri(iconPath));
            }
            catch { }

            string myPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string systemDir = System.Environment.GetFolderPath(System.Environment.SpecialFolder.System);
            bool alreadyInstalled = myPath.StartsWith(systemDir, StringComparison.OrdinalIgnoreCase);
            InstallBtn.Visibility = alreadyInstalled ? Visibility.Collapsed : Visibility.Visible;

            Closing += (s, e) =>
            {
                try { System.Environment.Exit(0); } catch { }
            };

            if (config == null) return;

            // 更新信息
            var meta = new System.Text.StringBuilder();
            if (!string.IsNullOrEmpty(config.Title))
                meta.Append(config.Title);
            if (!string.IsNullOrEmpty(config.CreatedBy))
            {
                if (meta.Length > 0) meta.Append("  ·  ");
                meta.Append(config.CreatedBy);
            }
            if (config.ImageCount > 0)
            {
                if (meta.Length > 0) meta.Append("  ·  ");
                meta.Append($"{config.ImageCount} 张图片");
            }
            InfoText.Text = meta.Length > 0 ? meta.ToString() : "就绪";

            DisplayDurationSlider.Value = config.DisplayDuration;
            DisplayDurationLabel.Text = config.DisplayDuration.ToString("F1");

            TransitionDurationSlider.Value = config.TransitionDuration;
            TransitionDurationLabel.Text = config.TransitionDuration.ToString("F1");

            if (config.ShuffleImages)
                OrderRandom.IsChecked = true;
            else
                OrderSequential.IsChecked = true;

            if (config.SelectedEffects != null)
                foreach (var e in config.SelectedEffects)
                    _selectedEffects.Add(e);

            // 创建效果复选框列表
            foreach (var effect in AllEffects)
            {
                var cb = new CheckBox
                {
                    Content = effect,
                    Tag = effect,
                    IsChecked = _selectedEffects.Contains(effect),
                    Margin = new Thickness(2, 1, 2, 1),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Foreground = new SolidColorBrush(ColorFromHex("#1A1D2A")),
                    FontSize = 11,
                    FontFamily = new FontFamily("Noto Sans SC"),
                    Height = 22
                };
                cb.Checked += EffectCheckBox_Changed;
                cb.Unchecked += EffectCheckBox_Changed;
                EffectsList.Children.Add(cb);
            }

            // 初始化效果信息
            if (_selectedEffects.Count > 0)
            {
                string first = new System.Collections.Generic.List<string>(_selectedEffects)[0];
                UpdateEffectInfo(first);
            }
            UpdateEffectCount();

            // 初始化预览
            InitializePreviewImages();

            // 延迟播放首个效果的预览（等布局完成）
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_selectedEffects.Count > 0)
                {
                    string first = new System.Collections.Generic.List<string>(_selectedEffects)[0];
                    PlayEffectPreview(first);
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private static Color ColorFromHex(string hex)
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }

        private IEnumerable<CheckBox> GetAllCheckBoxes()
        {
            foreach (var child in EffectsList.Children)
            {
                if (child is CheckBox cb)
                    yield return cb;
            }
        }

        private void EffectCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            var cb = (CheckBox)sender;
            var effectName = (string)cb.Tag;
            if (cb.IsChecked == true)
            {
                _selectedEffects.Add(effectName);
                PlayEffectPreview(effectName);
            }
            else
            {
                _selectedEffects.Remove(effectName);
            }

            UpdateEffectInfo(effectName);
            UpdateEffectCount();
        }

        private void UpdateEffectInfo(string effectName)
        {
            if (SelectedEffectName == null || SelectedEffectDesc == null) return;

            SelectedEffectName.Text = effectName;
            int idx = Array.IndexOf(AllEffects, effectName);
            if (idx >= 0 && idx < EffectDescriptions.Length)
                SelectedEffectDesc.Text = EffectDescriptions[idx];
        }

        private void UpdateEffectCount()
        {
            if (SelectedEffectCount == null) return;
            SelectedEffectCount.Text = _selectedEffects.Count.ToString();
        }

        private void InitializePreviewImages()
        {
            EffectPreviewImageA.Source = LoadImageOrDefault(0, "#2E6B9E");
            EffectPreviewImageB.Source = LoadImageOrDefault(1, "#9E4A2E");
        }

        private void ResetPreview()
        {
            _previewStoryboard?.Stop();
            _previewStoryboard = null;

            EffectPreviewImageA.Opacity = 1.0;
            EffectPreviewImageB.Opacity = 0.0;
            EffectPreviewImageA.RenderTransform = null;
            EffectPreviewImageB.RenderTransform = null;
            EffectPreviewImageA.Clip = null;
            EffectPreviewImageB.Clip = null;
            EffectPreviewImageA.Effect = null;
            EffectPreviewImageB.Effect = null;
            Panel.SetZIndex(EffectPreviewImageA, 0);
            Panel.SetZIndex(EffectPreviewImageB, 0);
        }

        private void PlayEffectPreview(string effectName)
        {
            ResetPreview();

            var allTransitions = Engine.TransitionManager.GetAllTransitions();
            var match = allTransitions.FirstOrDefault(t => t.Id == effectName);
            if (match == null) return;

            double duration = 1.2;

            var sb = match.Build(EffectPreviewImageA, EffectPreviewImageB, duration);
            _previewStoryboard = sb;

            sb.Completed += (s, e) =>
            {
                ResetPreview();
                // 循环播放
                Dispatcher.BeginInvoke(new Action(() => PlayEffectPreview(effectName)),
                    System.Windows.Threading.DispatcherPriority.Background);
            };

            sb.Begin();
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            _selectedEffects.Clear();
            string lastEffect = null;
            foreach (var cb in GetAllCheckBoxes())
            {
                cb.IsChecked = true;
                string name = (string)cb.Tag;
                _selectedEffects.Add(name);
                lastEffect = name;
            }
            if (lastEffect != null)
            {
                UpdateEffectInfo(lastEffect);
                PlayEffectPreview(lastEffect);
            }
            UpdateEffectCount();
        }

        private void ClearAll_Click(object sender, RoutedEventArgs e)
        {
            _selectedEffects.Clear();
            foreach (var cb in GetAllCheckBoxes())
                cb.IsChecked = false;
            SelectedEffectName.Text = "—";
            SelectedEffectDesc.Text = "未选择任何效果";
            UpdateEffectCount();
            ResetPreview();
        }

        private void DisplayDuration_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (DisplayDurationLabel != null)
                DisplayDurationLabel.Text = e.NewValue.ToString("F1");
        }

        private void TransitionDuration_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TransitionDurationLabel != null)
                TransitionDurationLabel.Text = e.NewValue.ToString("F1");
        }

        private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                Close();
                return;
            }
            DragMove();
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void InstallButton_Click(object sender, RoutedEventArgs e)
        {
            string scrPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string myName = Path.GetFileName(scrPath);
            string systemDir = System.Environment.GetFolderPath(System.Environment.SpecialFolder.System);
            string destPath = Path.Combine(systemDir, myName);

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c copy /Y /B \"{scrPath}\" \"{destPath}\"",
                    Verb = "runas",
                    UseShellExecute = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                var proc = Process.Start(psi);
                if (proc == null)
                {
                    MessageBox.Show("需要管理员权限来安装屏保。");
                    return;
                }
                proc.WaitForExit(15000);

                if (proc.ExitCode == 0)
                {
                    SetScreensaverRegistry(destPath);
                    InstallBtn.Visibility = Visibility.Collapsed;
                    Close();
                }
                else
                {
                    MessageBox.Show($"安装失败 (code={proc.ExitCode})。\n请手动复制到：\n{systemDir}");
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"安装失败：{ex.Message}");
            }
        }

        private static void SetScreensaverRegistry(string scrPath)
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", true))
                {
                    if (key != null)
                        key.SetValue("SCRNSAVE.EXE", scrPath, Microsoft.Win32.RegistryValueKind.String);
                }
            }
            catch { }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            SaveCurrentConfig();
            Close();
        }

        private void SaveCurrentConfig()
        {
            if (_config == null) return;

            _config.DisplayDuration = DisplayDurationSlider.Value;
            _config.TransitionDuration = TransitionDurationSlider.Value;
            _config.ShuffleImages = OrderRandom.IsChecked == true;
            _config.SelectedEffects = _selectedEffects.Count > 0
                ? new System.Collections.Generic.List<string>(_selectedEffects).ToArray()
                : new[] { "Fade" };

            ResourceLoader.SaveConfig(_config);
        }
    }
}
