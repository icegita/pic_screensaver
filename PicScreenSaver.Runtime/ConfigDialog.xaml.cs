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
            "旧图放大淡出的同时新图缩小淡入",
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

            // 设置窗口图标（从嵌入资源加载）
            try
            {
                using (var stream = System.Reflection.Assembly.GetExecutingAssembly()
                    .GetManifestResourceStream("PicScreenSaver.Runtime.Resources.sys.png"))
                {
                    if (stream != null)
                    {
                        var img = new BitmapImage();
                        img.BeginInit();
                        img.CacheOption = BitmapCacheOption.OnLoad;
                        img.StreamSource = stream;
                        img.EndInit();
                        img.Freeze();
                        Icon = img;
                    }
                }
            }
            catch { }

            // 初始化主题按钮图标
            SetThemeIcon();

            string myPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string systemDir = System.Environment.GetFolderPath(System.Environment.SpecialFolder.System);
            bool alreadyInstalled = myPath.StartsWith(systemDir, StringComparison.OrdinalIgnoreCase);
            InstallBtn.Visibility = alreadyInstalled ? Visibility.Collapsed : Visibility.Visible;

            Closing += (s, e) =>
            {
                _previewStoryboard?.Stop();
                _previewStoryboard = null;
            };

            if (config == null) return;

            // 标题跟随生成时的屏保名称
            string appTitle = !string.IsNullOrEmpty(config.Title) ? config.Title : "PicScreenSaver";
            Title = appTitle;
            TitleText.Text = appTitle;

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
                    Style = (Style)FindResource("EffectCheckBoxStyle")
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

        private void RootBorder_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateBorderClip();
            RootBorder.SizeChanged += (s, ev) => UpdateBorderClip();
            EffectPreviewBorder.Loaded += EffectPreviewBorder_LayoutUpdated;
            EffectPreviewBorder.SizeChanged += (s, ev) => UpdatePreviewClip();
        }

        private void UpdateBorderClip()
        {
            if (RootBorder.ActualWidth > 0 && RootBorder.ActualHeight > 0)
                RootBorder.Clip = new RectangleGeometry(
                    new Rect(0, 0, RootBorder.ActualWidth, RootBorder.ActualHeight), 10, 10);
        }

        private void EffectPreviewBorder_LayoutUpdated(object sender, RoutedEventArgs e)
        {
            UpdatePreviewClip();
        }

        private void UpdatePreviewClip()
        {
            if (EffectPreviewBorder.ActualWidth > 0 && EffectPreviewBorder.ActualHeight > 0)
                EffectPreviewBorder.Clip = new RectangleGeometry(
                    new Rect(0, 0, EffectPreviewBorder.ActualWidth, EffectPreviewBorder.ActualHeight), 6, 6);
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

        private void CloseBtn_MouseEnter(object sender, MouseEventArgs e)
        {
            CloseBtn.Background = ThemeColors.Brush(ThemeColors.DangerBg);
            CloseLine1.Stroke = ThemeColors.Brush(ThemeColors.Danger);
            CloseLine2.Stroke = ThemeColors.Brush(ThemeColors.Danger);
        }

        private void CloseBtn_MouseLeave(object sender, MouseEventArgs e)
        {
            CloseBtn.Background = Brushes.Transparent;
            CloseLine1.Stroke = ThemeColors.Brush(ThemeColors.Text2);
            CloseLine2.Stroke = ThemeColors.Brush(ThemeColors.Text2);
        }

        private void ThemeToggleBtn_Click(object sender, RoutedEventArgs e)
        {
            ThemeColors.SetDark(!ThemeColors.IsDark);
            ApplyTheme();
        }

        private void ThemeBtn_MouseEnter(object sender, MouseEventArgs e)
        {
            ThemeToggleBtn.Background = new SolidColorBrush(Color.FromArgb(30, 0, 0, 0));
        }

        private void ThemeBtn_MouseLeave(object sender, MouseEventArgs e)
        {
            ThemeToggleBtn.Background = Brushes.Transparent;
        }

        private void SetThemeIcon()
        {
            var viewbox = new Viewbox { Width = 16, Height = 16, Stretch = Stretch.Uniform };
            string pathData = ThemeColors.IsDark
                ? "M164.571429 241.371429c-171.885714 171.885714-171.885714 449.828571 0 621.714285s449.828571 171.885714 621.714285 0c54.857143-54.857143 95.085714-124.342857 113.371429-193.828571l3.657143-14.628572c7.314286-25.6-10.971429-47.542857-32.914286-54.857142-14.628571-3.657143-25.6 0-36.571429 7.314285-124.342857 69.485714-285.257143 51.2-391.314285-54.857143-91.428571-91.428571-117.028571-219.428571-80.457143-332.8l3.657143-10.971428c3.657143-25.6-10.971429-51.2-32.914286-62.171429-10.971429-3.657143-21.942857-3.657143-32.914286 0-3.657143 0-3.657143 0-3.657143 3.657143-47.542857 21.942857-91.428571 51.2-131.657142 91.428572z"
                : "M512 511.4m-212 0a212 212 0 1 0 424 0 212 212 0 1 0-424 0Z M511.7 130.2c12.9-0.2 24.2 11.3 24.5 24.3l0.4 79.6c0.2 12.9-11.3 24.2-24.3 24.5-12.9 0.2-24.2-11.3-24.5-24.3l-0.4-79.6c0.9-14.9 11.4-24.3 24.3-24.5z M901.5 510c-0.2 12.9-9.6 23.4-24.5 24.3l-79.6-0.4c-12.9-0.2-24.5-11.5-24.3-24.5 0.2-12.9 11.5-24.5 24.5-24.3l79.6 0.4c12.9 0.3 24.5 11.6 24.3 24.5z M250.9 510c-0.2 12.9-9.6 23.4-24.5 24.3l-79.6-0.4c-12.9-0.2-24.5-11.5-24.3-24.5 0.2-12.9 11.5-24.5 24.5-24.3l79.6 0.4c12.9 0.3 24.5 11.6 24.3 24.5z M512.3 893.8c-12.9 0.2-24.2-11.3-24.5-24.3l-0.4-79.6c-0.2-12.9 11.3-24.2 24.3-24.5 12.9-0.2 24.2 11.3 24.5 24.3l0.4 79.6c-0.9 14.9-11.4 24.3-24.3 24.5z M781.2 242.3c9.3 9 9.1 25.1 0.1 34.5l-56 56.5c-9 9.3-25.1 9.1-34.5 0.1-9.3-9-9.1-25.1-0.1-34.5l56-56.5c11.1-9.9 25.1-9.1 34.5-0.1z M788.2 786.5c-9.3 9-23.3 9.8-34.5-0.1l-56-56.5c-9-9.3-9.2-25.5 0.1-34.5 9.3-9 25.5-9.2 34.5 0.1l56 56.5c9 9.3 9.2 25.5-0.1 34.5z M328.1 326.4c-9.3 9-23.3 9.8-34.5-0.1l-56-56.5c-9-9.3-9.2-25.5 0.1-34.5 9.3-9 25.5-9.2 34.5 0.1l56 56.5c9 9.4 9.3 25.5-0.1 34.5z M241.6 782.6c-9.3-9-9.1-25.1-0.1-34.5l56-56.5c9-9.3 25.1-9.1 34.5-0.1 9.3 9 9.1 25.1 0.1 34.5l-56 56.5c-11.1 9.9-25.2 9.1-34.5 0.1z";
            viewbox.Child = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse(pathData),
                Fill = ThemeColors.Brush(ThemeColors.Text2),
                Stretch = Stretch.Uniform
            };
            ThemeToggleBtn.Content = viewbox;
        }

        private void ApplyTheme()
        {
            // 更新主题图标
            SetThemeIcon();

            // 更新阴影颜色
            var shadowEffect = ShadowHost.Effect as System.Windows.Media.Effects.DropShadowEffect;
            if (shadowEffect != null)
            {
                shadowEffect.Color = ThemeColors.IsDark ? Colors.White : Colors.Black;
            }

            // 更新背景色
            RootBorder.Background = ThemeColors.Brush(ThemeColors.Bg);

            // 更新标题栏
            TitleBarBg.Background = ThemeColors.Brush(ThemeColors.Surface);
            TitleBarBg.BorderBrush = ThemeColors.Brush(ThemeColors.Border);
            TitleText.Foreground = ThemeColors.Brush(ThemeColors.Text2);
            CloseLine1.Stroke = ThemeColors.Brush(ThemeColors.Text2);
            CloseLine2.Stroke = ThemeColors.Brush(ThemeColors.Text2);

            // 更新左栏卡片
            LeftCard.Background = ThemeColors.Brush(ThemeColors.Surface);
            LeftCard.BorderBrush = ThemeColors.Brush(ThemeColors.Border);
            UpdateChildBorders(LeftCard);
            UpdateTextColors(LeftCard);

            // 更新右栏卡片
            RightCard.Background = ThemeColors.Brush(ThemeColors.Surface);
            RightCard.BorderBrush = ThemeColors.Brush(ThemeColors.Border);
            UpdateChildBorders(RightCard);
            UpdateButtonColors(RightCard);

            // 更新效果信息区域
            EffectPreviewArea.Background = ThemeColors.Brush(ThemeColors.Surface);
            EffectPreviewBorder.Background = ThemeColors.Brush(ThemeColors.Surface2);
            EffectInfoBorder.Background = ThemeColors.Brush(ThemeColors.Surface);
            SelectedEffectName.Foreground = ThemeColors.Brush(ThemeColors.Text);
            SelectedEffectDesc.Foreground = ThemeColors.Brush(ThemeColors.Text2);
            SelectedEffectCountLabel.Foreground = ThemeColors.Brush(ThemeColors.Accent);
            EffectPreviewBorder.Background = ThemeColors.Brush(ThemeColors.Surface2);

            // 更新底部栏
            BottomBarBg.Background = ThemeColors.Brush(ThemeColors.Surface);
            BottomBarBg.BorderBrush = ThemeColors.Brush(ThemeColors.Border);
            InfoText.Foreground = ThemeColors.Brush(ThemeColors.Text3);
        }

        private void UpdateChildBorders(DependencyObject parent)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is System.Windows.Controls.Border border)
                {
                    border.BorderBrush = ThemeColors.Brush(ThemeColors.Border);
                }
                else
                {
                    UpdateChildBorders(child);
                }
            }
        }

        private void UpdateTextColors(DependencyObject parent)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is TextBlock tb)
                {
                    tb.Foreground = ThemeColors.Brush(ThemeColors.Text2);
                }
                else if (child is RadioButton rb)
                {
                    rb.Foreground = ThemeColors.Brush(ThemeColors.Text2);
                }
                else if (child is Slider slider)
                {
                    slider.Foreground = ThemeColors.Brush(ThemeColors.Accent);
                }
                else
                {
                    UpdateTextColors(child);
                }
            }
        }

        private void UpdateButtonColors(DependencyObject parent)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is Button btn && btn.Content?.ToString() != null)
                {
                    string content = btn.Content.ToString();
                    if (content == "全选" || content == "清空")
                    {
                        btn.Background = ThemeColors.Brush(ThemeColors.BtnBg);
                        btn.Foreground = ThemeColors.Brush(ThemeColors.Text2);
                        btn.BorderBrush = ThemeColors.Brush(ThemeColors.Border);
                    }
                }
                else if (child is CheckBox cb)
                {
                    cb.Foreground = ThemeColors.Brush(ThemeColors.Text);
                }
                else
                {
                    UpdateButtonColors(child);
                }
            }
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
