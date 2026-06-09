using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using PicScreenSaver.Maker.Models;
using PicScreenSaver.Maker.Services;

namespace PicScreenSaver.Maker
{
    public partial class MainWindow : Window
    {
        private readonly List<ImageItem> _images = new List<ImageItem>();
        private readonly ImageProcessor _imageProcessor = new ImageProcessor();
        private readonly PackageBuilder _packageBuilder = new PackageBuilder();
        private int _selectedIndex = -1;
        private double _displayDuration = 10.0;
        private double _transitionDuration = 1.2;
        private int _quality = 70;
        private int _maxWidth = 1920;
        private string _outputPath = "";
        private System.Threading.CancellationTokenSource _encodeCts;
        private bool _isDragging = false;
        private int _dragFromIndex = -1;
        private Border _draggedCard = null;
        private bool _isDarkTheme = false;
        private Point _mouseDownPos;
        private bool _dragThresholdMet = false;
        private Border _dragGhost = null;

        private int _dropTargetIndex = -1;
        private int _prevHighlightIndex = -1;

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

        private readonly HashSet<string> _selectedEffects = new HashSet<string>();

        public MainWindow()
        {
            InitializeComponent();

            // 设置窗口图标
            try
            {
                string exeDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                string iconPath = System.IO.Path.Combine(exeDir, "img", "icon.png");
                if (System.IO.File.Exists(iconPath))
                    Icon = new System.Windows.Media.Imaging.BitmapImage(new Uri(iconPath));
            }
            catch { }

            ThemeColors.SetDark(false);
            _outputPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            OutputPathInput.Text = _outputPath;
            SaverNameInput.TextChanged += (s, e) => UpdateTitle();
            UpdateTitle();
            InitializeEffectsList();
            InitializePreviewImages();
            UpdateEstimate();
            CreateSliderButtons();
            SetThemeIcon(_isDarkTheme);
            UpdateSliderButtons();
        }

        private void UpdateTitle()
        {
            TitleText.Text = "PicScreenSaver";
        }

        private DispatcherTimer _toastTimer;

        private void ShowToast(string message, int durationMs = 2500)
        {
            ToastText.Text = message;
            ToastOverlay.Opacity = 0;
            ToastOverlay.Visibility = Visibility.Visible;

            _toastTimer?.Stop();
            _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
            _toastTimer.Tick += (s, e) =>
            {
                _toastTimer.Stop();
                var fadeIn = new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150));
                ToastOverlay.BeginAnimation(UIElement.OpacityProperty, fadeIn);

                var hideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(durationMs) };
                hideTimer.Tick += (s2, e2) =>
                {
                    hideTimer.Stop();
                    var fadeOut = new System.Windows.Media.Animation.DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
                    fadeOut.Completed += (s3, e3) => ToastOverlay.Visibility = Visibility.Collapsed;
                    ToastOverlay.BeginAnimation(UIElement.OpacityProperty, fadeOut);
                };
                hideTimer.Start();
            };
            _toastTimer.Start();
        }

        private void CreateSliderButtons()
        {
            SetupSliderBtn(DisplayDurationMinusBtn, "\u2212");
            SetupSliderBtn(DisplayDurationPlusBtn, "\uFF0B");
            SetupSliderBtn(TransitionDurationMinusBtn, "\u2212");
            SetupSliderBtn(TransitionDurationPlusBtn, "\uFF0B");
        }

        private void SetupSliderBtn(Button btn, string content)
        {
            btn.Content = content;
            btn.Background = ThemeColors.Brush(ThemeColors.SliderTrack);
            btn.Foreground = ThemeColors.Brush(ThemeColors.Text2);
            btn.BorderThickness = new Thickness(0);

            var template = new ControlTemplate(typeof(Button));
            var border = new FrameworkElementFactory(typeof(Border));
            border.Name = "border";
            border.SetBinding(Border.BackgroundProperty, new Binding("Background") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            border.SetBinding(Border.BorderBrushProperty, new Binding("BorderBrush") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            border.SetBinding(Border.BorderThicknessProperty, new Binding("BorderThickness") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(cp);
            template.VisualTree = border;
            btn.Template = template;
        }

        private void InitializePreviewImages()
        {
            EffectPreviewImageA.Source = LoadImageFromBytes(PackageBuilder.LoadEmbeddedImage("pic1.jpg"), "#2E6B9E");
            EffectPreviewImageB.Source = LoadImageFromBytes(PackageBuilder.LoadEmbeddedImage("pic2.jpg"), "#9E4A2E");
        }

        private BitmapSource LoadImageFromBytes(byte[] bytes, string fallbackColor)
        {
            if (bytes != null)
            {
                try
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
                catch { }
            }

            var color = (Color)ColorConverter.ConvertFromString(fallbackColor);
            var group = new DrawingGroup();
            group.Children.Add(new GeometryDrawing(
                new SolidColorBrush(color), null,
                new RectangleGeometry(new Rect(0, 0, 420, 263))));
            var visual = new DrawingVisual();
            using (var context = visual.RenderOpen()) { context.DrawDrawing(group); }
            var target = new RenderTargetBitmap(420, 263, 96, 96, PixelFormats.Pbgra32);
            target.Render(visual);
            return target;
        }

        private BitmapSource LoadImageOrDefault(string filePath, string fallbackColor1, string fallbackColor2)
        {
            if (File.Exists(filePath))
            {
                try
                {
                    var img = new BitmapImage();
                    img.BeginInit();
                    img.CacheOption = BitmapCacheOption.OnLoad;
                    img.UriSource = new Uri(filePath, UriKind.Absolute);
                    img.EndInit();
                    img.Freeze();
                    return img;
                }
                catch { }
            }

            var group = new DrawingGroup();
            group.Children.Add(new GeometryDrawing(
                new SolidColorBrush((Color)ColorConverter.ConvertFromString(fallbackColor1)), null,
                new RectangleGeometry(new Rect(0, 0, 420, 263))));
            var visual = new DrawingVisual();
            using (var context = visual.RenderOpen()) { context.DrawDrawing(group); }
            var target = new RenderTargetBitmap(420, 263, 96, 96, PixelFormats.Pbgra32);
            target.Render(visual);
            return target;
        }

        private void InitializeEffectsList()
        {
            for (int i = 0; i < AllEffects.Length; i++)
            {
                var effectName = AllEffects[i];
                var cb = new CheckBox
                {
                    Content = effectName,
                    Tag = effectName,
                    Style = (Style)FindResource("EffectCheckBoxStyle")
                };
                cb.Checked += EffectCheckBox_Changed;
                cb.Unchecked += EffectCheckBox_Changed;
                EffectsList.Children.Add(cb);
            }
            UpdateEffectCount();
        }

        private void EffectCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            var cb = (CheckBox)sender;
            var effectName = (string)cb.Tag;
            if (cb.IsChecked == true) _selectedEffects.Add(effectName);
            else _selectedEffects.Remove(effectName);
            UpdateEffectCount();
            UpdateEffectPreview(effectName);
        }

        private void UpdateEffectPreview(string effectName)
        {
            SelectedEffectName.Text = effectName;
            int idx = Array.IndexOf(AllEffects, effectName);
            if (idx >= 0 && idx < EffectDescriptions.Length)
                SelectedEffectDesc.Text = EffectDescriptions[idx];
            PlayEffectAnimation(effectName);
        }

        private string _currentEffect = "Fade";

        private void PlayEffectAnimation(string effectName)
        {
            _currentEffect = effectName;
            EffectPreviewImageA.RenderTransform = null;
            EffectPreviewImageB.RenderTransform = null;
            EffectPreviewImageA.Clip = null;
            EffectPreviewImageB.Clip = null;
            Panel.SetZIndex(EffectPreviewImageA, 0);
            Panel.SetZIndex(EffectPreviewImageB, 0);

            var sb = new Storyboard();
            var duration = TimeSpan.FromSeconds(1.2);

            switch (effectName)
            {
                case "Fade":
                    EffectPreviewImageA.Opacity = 1;
                    EffectPreviewImageB.Opacity = 0;
                    sb.Children.Add(CreateDoubleAnimation(EffectPreviewImageA, "Opacity", 1, 0, duration));
                    sb.Children.Add(CreateDoubleAnimation(EffectPreviewImageB, "Opacity", 0, 1, duration));
                    break;

                case "FadeBlack":
                    EffectPreviewImageA.Opacity = 1;
                    EffectPreviewImageB.Opacity = 0;
                    sb.Children.Add(CreateDoubleAnimation(EffectPreviewImageA, "Opacity", 1, 0, TimeSpan.FromSeconds(0.6)));
                    sb.Children.Add(CreateDoubleAnimation(EffectPreviewImageB, "Opacity", 0, 1, TimeSpan.FromSeconds(0.6)));
                    ((DoubleAnimation)sb.Children[1]).BeginTime = TimeSpan.FromSeconds(0.6);
                    break;

                case "FadeWhite":
                    EffectPreviewImageA.Opacity = 1;
                    EffectPreviewImageB.Opacity = 0;
                    var fadeWhiteOverlay = new System.Windows.Shapes.Rectangle
                    {
                        Fill = System.Windows.Media.Brushes.White,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        VerticalAlignment = VerticalAlignment.Stretch,
                        Opacity = 0
                    };
                    var previewParent = EffectPreviewImageA.Parent as Panel;
                    if (previewParent != null)
                        previewParent.Children.Insert(1, fadeWhiteOverlay);
                    Panel.SetZIndex(EffectPreviewImageA, 0);
                    Panel.SetZIndex(fadeWhiteOverlay, 1);
                    Panel.SetZIndex(EffectPreviewImageB, 2);
                    sb.Children.Add(CreateDoubleAnimation(EffectPreviewImageA, "Opacity", 1, 0, TimeSpan.FromSeconds(0.6)));
                    sb.Children.Add(CreateDoubleAnimation(fadeWhiteOverlay, "Opacity", 0, 1, TimeSpan.FromSeconds(0.6)));
                    var hideWhiteAnim = CreateDoubleAnimation(fadeWhiteOverlay, "Opacity", 1, 0, TimeSpan.FromSeconds(0.6));
                    ((DoubleAnimation)hideWhiteAnim).BeginTime = TimeSpan.FromSeconds(0.6);
                    sb.Children.Add(hideWhiteAnim);
                    sb.Children.Add(CreateDoubleAnimation(EffectPreviewImageB, "Opacity", 0, 1, TimeSpan.FromSeconds(0.6)));
                    ((DoubleAnimation)sb.Children[3]).BeginTime = TimeSpan.FromSeconds(0.6);
                    sb.Completed += (s2, e2) =>
                    {
                        if (previewParent != null && previewParent.Children.Contains(fadeWhiteOverlay))
                            previewParent.Children.Remove(fadeWhiteOverlay);
                    };
                    break;

                case "ZoomInFade":
                    EffectPreviewImageA.Opacity = 1; EffectPreviewImageB.Opacity = 0;
                    EffectPreviewImageB.RenderTransform = new ScaleTransform(0.95, 0.95);
                    EffectPreviewImageB.RenderTransformOrigin = new Point(0.5, 0.5);
                    Panel.SetZIndex(EffectPreviewImageB, 1);
                    sb.Children.Add(CreateScaleAnimation(EffectPreviewImageB, "ScaleX", 0.95, 1.0, duration));
                    sb.Children.Add(CreateScaleAnimation(EffectPreviewImageB, "ScaleY", 0.95, 1.0, duration));
                    sb.Children.Add(CreateDoubleAnimation(EffectPreviewImageB, "Opacity", 0, 1, duration));
                    sb.Children.Add(CreateDoubleAnimation(EffectPreviewImageA, "Opacity", 1, 0, duration));
                    break;

                case "ZoomOutFade":
                    EffectPreviewImageA.Opacity = 1; EffectPreviewImageB.Opacity = 0;
                    EffectPreviewImageA.RenderTransform = new ScaleTransform(1.05, 1.05);
                    EffectPreviewImageA.RenderTransformOrigin = new Point(0.5, 0.5);
                    Panel.SetZIndex(EffectPreviewImageA, 1);
                    sb.Children.Add(CreateScaleAnimation(EffectPreviewImageA, "ScaleX", 1.05, 1.0, duration));
                    sb.Children.Add(CreateScaleAnimation(EffectPreviewImageA, "ScaleY", 1.05, 1.0, duration));
                    sb.Children.Add(CreateDoubleAnimation(EffectPreviewImageA, "Opacity", 1, 0, duration));
                    sb.Children.Add(CreateDoubleAnimation(EffectPreviewImageB, "Opacity", 0, 1, duration));
                    break;

                case "CrossFade":
                    EffectPreviewImageA.Opacity = 1;
                    EffectPreviewImageB.Opacity = 0;
                    Panel.SetZIndex(EffectPreviewImageB, 1);
                    sb.Children.Add(CreateDoubleAnimation(EffectPreviewImageA, "Opacity", 1, 0, duration));
                    sb.Children.Add(CreateDoubleAnimation(EffectPreviewImageB, "Opacity", 0, 1, duration));
                    break;

                case "SlideLeft":
                    EffectPreviewImageA.Opacity = 1; EffectPreviewImageB.Opacity = 1;
                    { var ox = EffectPreviewImageA.ActualWidth; EffectPreviewImageB.RenderTransform = new TranslateTransform(ox, 0);
                    Panel.SetZIndex(EffectPreviewImageB, 1);
                    sb.Children.Add(CreateTranslateAnimation(EffectPreviewImageB, "X", ox, 0, duration)); }
                    break;
                case "SlideRight":
                    EffectPreviewImageA.Opacity = 1; EffectPreviewImageB.Opacity = 1;
                    { var ox = EffectPreviewImageA.ActualWidth; EffectPreviewImageB.RenderTransform = new TranslateTransform(-ox, 0);
                    Panel.SetZIndex(EffectPreviewImageB, 1);
                    sb.Children.Add(CreateTranslateAnimation(EffectPreviewImageB, "X", -ox, 0, duration)); }
                    break;
                case "SlideUp":
                    EffectPreviewImageA.Opacity = 1; EffectPreviewImageB.Opacity = 1;
                    { var oy = EffectPreviewImageA.ActualHeight; EffectPreviewImageB.RenderTransform = new TranslateTransform(0, oy);
                    Panel.SetZIndex(EffectPreviewImageB, 1);
                    sb.Children.Add(CreateTranslateAnimation(EffectPreviewImageB, "Y", oy, 0, duration)); }
                    break;
                case "SlideDown":
                    EffectPreviewImageA.Opacity = 1; EffectPreviewImageB.Opacity = 1;
                    { var oy = EffectPreviewImageA.ActualHeight; EffectPreviewImageB.RenderTransform = new TranslateTransform(0, -oy);
                    Panel.SetZIndex(EffectPreviewImageB, 1);
                    sb.Children.Add(CreateTranslateAnimation(EffectPreviewImageB, "Y", -oy, 0, duration)); }
                    break;

                case "WipeLeft":
                case "WipeRight":
                case "WipeUp":
                case "WipeDown":
                    PlayWipePreview(effectName, duration);
                    return;

                case "PushLeft":
                    EffectPreviewImageA.Opacity = 1; EffectPreviewImageB.Opacity = 1;
                    { var ox = EffectPreviewImageA.ActualWidth;
                    EffectPreviewImageA.RenderTransform = new TranslateTransform(0, 0);
                    EffectPreviewImageB.RenderTransform = new TranslateTransform(ox, 0);
                    Panel.SetZIndex(EffectPreviewImageB, 1);
                    sb.Children.Add(CreateTranslateAnimation(EffectPreviewImageA, "X", 0, -ox, duration));
                    sb.Children.Add(CreateTranslateAnimation(EffectPreviewImageB, "X", ox, 0, duration)); }
                    break;

                case "PushUp":
                    EffectPreviewImageA.Opacity = 1; EffectPreviewImageB.Opacity = 1;
                    { var oy = EffectPreviewImageA.ActualHeight;
                    EffectPreviewImageA.RenderTransform = new TranslateTransform(0, 0);
                    EffectPreviewImageB.RenderTransform = new TranslateTransform(0, oy);
                    Panel.SetZIndex(EffectPreviewImageB, 1);
                    sb.Children.Add(CreateTranslateAnimation(EffectPreviewImageA, "Y", 0, -oy, duration));
                    sb.Children.Add(CreateTranslateAnimation(EffectPreviewImageB, "Y", oy, 0, duration)); }
                    break;

                case "PushRight":
                    EffectPreviewImageA.Opacity = 1; EffectPreviewImageB.Opacity = 1;
                    { var ox = EffectPreviewImageA.ActualWidth;
                    EffectPreviewImageA.RenderTransform = new TranslateTransform(0, 0);
                    EffectPreviewImageB.RenderTransform = new TranslateTransform(-ox, 0);
                    Panel.SetZIndex(EffectPreviewImageB, 1);
                    sb.Children.Add(CreateTranslateAnimation(EffectPreviewImageA, "X", 0, ox, duration));
                    sb.Children.Add(CreateTranslateAnimation(EffectPreviewImageB, "X", -ox, 0, duration)); }
                    break;

                case "PushDown":
                    EffectPreviewImageA.Opacity = 1; EffectPreviewImageB.Opacity = 1;
                    { var oy = EffectPreviewImageA.ActualHeight;
                    EffectPreviewImageA.RenderTransform = new TranslateTransform(0, 0);
                    EffectPreviewImageB.RenderTransform = new TranslateTransform(0, -oy);
                    Panel.SetZIndex(EffectPreviewImageB, 1);
                    sb.Children.Add(CreateTranslateAnimation(EffectPreviewImageA, "Y", 0, oy, duration));
                    sb.Children.Add(CreateTranslateAnimation(EffectPreviewImageB, "Y", -oy, 0, duration)); }
                    break;

                case "FadeBlur":
                    EffectPreviewImageA.Opacity = 1; EffectPreviewImageB.Opacity = 0;
                    EffectPreviewImageA.Effect = new System.Windows.Media.Effects.BlurEffect { Radius = 0 };
                    EffectPreviewImageB.Effect = new System.Windows.Media.Effects.BlurEffect { Radius = 6 };
                    Panel.SetZIndex(EffectPreviewImageB, 1);
                    sb.Children.Add(CreateDoubleAnimation(EffectPreviewImageA, "Opacity", 1, 0, duration));
                    sb.Children.Add(CreateDoubleAnimation(EffectPreviewImageB, "Opacity", 0, 1, duration));
                    sb.Completed += (s2, e2) => { EffectPreviewImageA.Effect = null; EffectPreviewImageB.Effect = null; };
                    break;

                case "ZoomIn":
                    EffectPreviewImageA.Opacity = 1; EffectPreviewImageB.Opacity = 0;
                    EffectPreviewImageB.RenderTransform = new ScaleTransform(1.2, 1.2);
                    EffectPreviewImageB.RenderTransformOrigin = new Point(0.5, 0.5);
                    Panel.SetZIndex(EffectPreviewImageB, 1);
                    sb.Children.Add(CreateScaleAnimation(EffectPreviewImageB, "ScaleX", 1.2, 1.0, duration));
                    sb.Children.Add(CreateScaleAnimation(EffectPreviewImageB, "ScaleY", 1.2, 1.0, duration));
                    sb.Children.Add(CreateDoubleAnimation(EffectPreviewImageB, "Opacity", 0, 1, duration));
                    sb.Children.Add(CreateDoubleAnimation(EffectPreviewImageA, "Opacity", 1, 0, duration));
                    break;

                case "ZoomOut":
                    EffectPreviewImageA.Opacity = 1; EffectPreviewImageB.Opacity = 0;
                    EffectPreviewImageA.RenderTransform = new ScaleTransform(1.0, 1.0);
                    EffectPreviewImageA.RenderTransformOrigin = new Point(0.5, 0.5);
                    Panel.SetZIndex(EffectPreviewImageA, 1);
                    sb.Children.Add(CreateScaleAnimation(EffectPreviewImageA, "ScaleX", 1.0, 1.2, duration));
                    sb.Children.Add(CreateScaleAnimation(EffectPreviewImageA, "ScaleY", 1.0, 1.2, duration));
                    sb.Children.Add(CreateDoubleAnimation(EffectPreviewImageA, "Opacity", 1, 0, duration));
                    sb.Children.Add(CreateDoubleAnimation(EffectPreviewImageB, "Opacity", 0, 1, duration));
                    break;

                case "RotateCW":
                    EffectPreviewImageA.Opacity = 1; EffectPreviewImageB.Opacity = 0;
                    EffectPreviewImageB.RenderTransformOrigin = new Point(0.5, 0.5);
                    EffectPreviewImageB.RenderTransform = new RotateTransform(-15);
                    Panel.SetZIndex(EffectPreviewImageB, 1);
                    sb.Children.Add(CreateDoubleAnimationT(EffectPreviewImageB, "(UIElement.RenderTransform).(RotateTransform.Angle)", -15, 0, duration));
                    sb.Children.Add(CreateDoubleAnimation(EffectPreviewImageB, "Opacity", 0, 1, duration));
                    sb.Children.Add(CreateDoubleAnimation(EffectPreviewImageA, "Opacity", 1, 0, duration));
                    break;

                case "RotateCCW":
                    EffectPreviewImageA.Opacity = 1; EffectPreviewImageB.Opacity = 0;
                    EffectPreviewImageB.RenderTransformOrigin = new Point(0.5, 0.5);
                    EffectPreviewImageB.RenderTransform = new RotateTransform(15);
                    Panel.SetZIndex(EffectPreviewImageB, 1);
                    sb.Children.Add(CreateDoubleAnimationT(EffectPreviewImageB, "(UIElement.RenderTransform).(RotateTransform.Angle)", 15, 0, duration));
                    sb.Children.Add(CreateDoubleAnimation(EffectPreviewImageB, "Opacity", 0, 1, duration));
                    sb.Children.Add(CreateDoubleAnimation(EffectPreviewImageA, "Opacity", 1, 0, duration));
                    break;

                case "BlindsH":
                case "BlindsV":
                    PlayBlindsPreview(effectName, duration);
                    return;

                case "CrossZoom":
                    EffectPreviewImageA.Opacity = 1; EffectPreviewImageB.Opacity = 0;
                    EffectPreviewImageA.RenderTransform = new ScaleTransform(1.0, 1.0);
                    EffectPreviewImageA.RenderTransformOrigin = new Point(0.5, 0.5);
                    EffectPreviewImageB.RenderTransform = new ScaleTransform(1.3, 1.3);
                    EffectPreviewImageB.RenderTransformOrigin = new Point(0.5, 0.5);
                    Panel.SetZIndex(EffectPreviewImageB, 1);
                    sb.Children.Add(CreateScaleAnimation(EffectPreviewImageA, "ScaleX", 1.0, 1.3, duration));
                    sb.Children.Add(CreateScaleAnimation(EffectPreviewImageA, "ScaleY", 1.0, 1.3, duration));
                    sb.Children.Add(CreateScaleAnimation(EffectPreviewImageB, "ScaleX", 1.3, 1.0, duration));
                    sb.Children.Add(CreateScaleAnimation(EffectPreviewImageB, "ScaleY", 1.3, 1.0, duration));
                    sb.Children.Add(CreateDoubleAnimation(EffectPreviewImageA, "Opacity", 1, 0, duration));
                    sb.Children.Add(CreateDoubleAnimation(EffectPreviewImageB, "Opacity", 0, 1, duration));
                    break;

                case "CircleReveal":
                    EffectPreviewImageA.Opacity = 1; EffectPreviewImageB.Opacity = 1;
                    Panel.SetZIndex(EffectPreviewImageB, 1);
                    { double pw = EffectPreviewImageA.ActualWidth, ph = EffectPreviewImageA.ActualHeight;
                    double maxR = Math.Sqrt(pw*pw + ph*ph) / 2.0;
                    EffectPreviewImageB.Clip = new EllipseGeometry(new Point(pw/2, ph/2), 0, 0);
                    var circleAnim = new EllipseRadiusAnimation(new Point(pw/2, ph/2), 0, 0, maxR, maxR, duration);
                    Storyboard.SetTarget(circleAnim, EffectPreviewImageB);
                    Storyboard.SetTargetProperty(circleAnim, new PropertyPath("Clip"));
                    sb.Children.Add(circleAnim);
                    sb.Completed += (s2, e2) => { EffectPreviewImageB.Clip = null; }; }
                    break;

                case "DiamondReveal":
                    EffectPreviewImageA.Opacity = 1; EffectPreviewImageB.Opacity = 1;
                    Panel.SetZIndex(EffectPreviewImageB, 1);
                    { var dAnim = new DiamondRevealAnimation(EffectPreviewImageA.ActualWidth, EffectPreviewImageA.ActualHeight, duration);
                    Storyboard.SetTarget(dAnim, EffectPreviewImageB);
                    Storyboard.SetTargetProperty(dAnim, new PropertyPath("Clip"));
                    sb.Children.Add(dAnim);
                    sb.Completed += (s2, e2) => { EffectPreviewImageB.Clip = null; }; }
                    break;

                case "Checkerboard":
                    PlayCheckerboardPreview(duration);
                    return;

                case "RadialWipe":
                    EffectPreviewImageA.Opacity = 1; EffectPreviewImageB.Opacity = 1;
                    Panel.SetZIndex(EffectPreviewImageB, 1);
                    { double rw = EffectPreviewImageA.ActualWidth, rh = EffectPreviewImageA.ActualHeight;
                    double r = Math.Sqrt(rw*rw + rh*rh);
                    var radialAnim = new RadialSectorAnimation(rw/2, rh/2, r, duration);
                    Storyboard.SetTarget(radialAnim, EffectPreviewImageB);
                    Storyboard.SetTargetProperty(radialAnim, new PropertyPath("Clip"));
                    sb.Children.Add(radialAnim);
                    sb.Completed += (s2, e2) => { EffectPreviewImageB.Clip = null; }; }
                    break;

                default:
                    EffectPreviewImageA.Opacity = 1; EffectPreviewImageB.Opacity = 0;
                    sb.Children.Add(CreateDoubleAnimation(EffectPreviewImageA, "Opacity", 1, 0, duration));
                    sb.Children.Add(CreateDoubleAnimation(EffectPreviewImageB, "Opacity", 0, 1, duration));
                    break;
            }

            sb.Completed += (s, e) =>
            {
                EffectPreviewImageA.Opacity = 1; EffectPreviewImageB.Opacity = 0;
                EffectPreviewImageA.RenderTransform = null; EffectPreviewImageB.RenderTransform = null;
            };
            sb.Begin();
        }

        private void PlayWipePreview(string effectName, TimeSpan duration)
        {
            double pw = EffectPreviewImageA.ActualWidth, ph = EffectPreviewImageA.ActualHeight;
            EffectPreviewImageA.Opacity = 1; EffectPreviewImageB.Opacity = 1;
            Panel.SetZIndex(EffectPreviewImageB, 1);

            Rect fromRect;
            Rect toRect = new Rect(0, 0, pw, ph);
            switch (effectName)
            {
                case "WipeLeft":  fromRect = new Rect(0, 0, 0, ph); break;
                case "WipeRight": fromRect = new Rect(pw, 0, 0, ph); break;
                case "WipeUp":    fromRect = new Rect(0, 0, pw, 0); break;
                default:          fromRect = new Rect(0, ph, pw, 0); break;
            }
            EffectPreviewImageB.Clip = new RectangleGeometry(fromRect);

            var anim = new RectAnimation(fromRect, toRect, duration);
            Storyboard.SetTarget(anim, EffectPreviewImageB);
            Storyboard.SetTargetProperty(anim, new PropertyPath("Clip"));

            var sb = new Storyboard();
            sb.Children.Add(anim);
            sb.Completed += (s, e) => { EffectPreviewImageA.Opacity = 1; EffectPreviewImageB.Opacity = 0; };
            sb.Begin();
        }

        private void PlayFlipPreview(string effectName, TimeSpan duration)
        {
            var sb = new Storyboard();
            var halfTime = TimeSpan.FromSeconds(duration.TotalSeconds / 2);
            EffectPreviewImageA.Opacity = 1; EffectPreviewImageB.Opacity = 0;
            EffectPreviewImageA.RenderTransformOrigin = new Point(0.5, 0.5);
            EffectPreviewImageA.RenderTransform = new ScaleTransform(1, 1);
            EffectPreviewImageB.RenderTransformOrigin = new Point(0.5, 0.5);
            EffectPreviewImageB.RenderTransform = new ScaleTransform(1, 1);
            Panel.SetZIndex(EffectPreviewImageA, 2);
            Panel.SetZIndex(EffectPreviewImageB, 1);

            string axis = effectName == "FlipHorizontal" ? "ScaleX" : "ScaleY";

            var flipOut = new DoubleAnimation(1, 0, new Duration(halfTime));
            Storyboard.SetTarget(flipOut, EffectPreviewImageA);
            Storyboard.SetTargetProperty(flipOut, new PropertyPath($"(UIElement.RenderTransform).(ScaleTransform.{axis})"));

            var flipIn = new DoubleAnimation(0, 1, new Duration(halfTime));
            Storyboard.SetTarget(flipIn, EffectPreviewImageB);
            Storyboard.SetTargetProperty(flipIn, new PropertyPath($"(UIElement.RenderTransform).(ScaleTransform.{axis})"));
            flipIn.BeginTime = halfTime;

            var showIncoming = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(1)));
            Storyboard.SetTarget(showIncoming, EffectPreviewImageB);
            Storyboard.SetTargetProperty(showIncoming, new PropertyPath(UIElement.OpacityProperty));
            showIncoming.BeginTime = halfTime;

            sb.Children.Add(flipOut); sb.Children.Add(flipIn); sb.Children.Add(showIncoming);
            sb.Completed += (s, e) =>
            {
                EffectPreviewImageA.Opacity = 1; EffectPreviewImageB.Opacity = 0;
                EffectPreviewImageA.RenderTransform = null; EffectPreviewImageB.RenderTransform = null;
            };
            sb.Begin();
        }

        private DoubleAnimation CreateDoubleAnimation(DependencyObject target, string property, double from, double to, TimeSpan duration)
        {
            var anim = new DoubleAnimation(from, to, new Duration(duration));
            Storyboard.SetTarget(anim, target);
            Storyboard.SetTargetProperty(anim, new PropertyPath(property));
            return anim;
        }

        private DoubleAnimation CreateTranslateAnimation(FrameworkElement target, string axis, double from, double to, TimeSpan duration)
        {
            var anim = new DoubleAnimation(from, to, new Duration(duration));
            Storyboard.SetTarget(anim, target);
            Storyboard.SetTargetProperty(anim, new PropertyPath($"(UIElement.RenderTransform).(TranslateTransform.{axis})"));
            return anim;
        }

        private DoubleAnimation CreateScaleAnimation(FrameworkElement target, string axis, double from, double to, TimeSpan duration)
        {
            var anim = new DoubleAnimation(from, to, new Duration(duration));
            Storyboard.SetTarget(anim, target);
            Storyboard.SetTargetProperty(anim, new PropertyPath($"(UIElement.RenderTransform).(ScaleTransform.{axis})"));
            return anim;
        }

        private DoubleAnimation CreateDoubleAnimationT(FrameworkElement target, string propertyPath, double from, double to, TimeSpan duration)
        {
            var anim = new DoubleAnimation(from, to, new Duration(duration));
            Storyboard.SetTarget(anim, target);
            Storyboard.SetTargetProperty(anim, new PropertyPath(propertyPath));
            return anim;
        }

        private void PlayBlindsPreview(string effectName, TimeSpan duration)
        {
            double pw = EffectPreviewImageA.ActualWidth, ph = EffectPreviewImageA.ActualHeight;
            int count = 8;

            EffectPreviewImageA.Opacity = 1;
            EffectPreviewImageB.Opacity = 1;
            Panel.SetZIndex(EffectPreviewImageB, 1);

            var anim = new BlindsGeometryAnimation(pw, ph, count, effectName == "BlindsH", duration);
            Storyboard.SetTarget(anim, EffectPreviewImageB);
            Storyboard.SetTargetProperty(anim, new PropertyPath("Clip"));

            var sb = new Storyboard();
            sb.Children.Add(anim);
            sb.Completed += (s, e) => { EffectPreviewImageB.Clip = null; EffectPreviewImageB.Opacity = 0; };
            sb.Begin();
        }

        private void PlayCheckerboardPreview(TimeSpan duration)
        {
            double pw = EffectPreviewImageA.ActualWidth, ph = EffectPreviewImageA.ActualHeight;
            int cols = 8, rows = 6;

            EffectPreviewImageA.Opacity = 1;
            EffectPreviewImageB.Opacity = 1;
            Panel.SetZIndex(EffectPreviewImageB, 1);

            var anim = new CheckerboardGeometryAnimation(pw, ph, cols, rows, duration);
            Storyboard.SetTarget(anim, EffectPreviewImageB);
            Storyboard.SetTargetProperty(anim, new PropertyPath("Clip"));

            var sb = new Storyboard();
            sb.Children.Add(anim);
            sb.Completed += (s, e) => { EffectPreviewImageB.Clip = null; EffectPreviewImageB.Opacity = 0; };
            sb.Begin();
        }

        private void UpdateEffectCount() {
            var count = _selectedEffects.Count.ToString();
            SelectedEffectCount.Text = count;
            SelectedEffectCountLabel.Text = count;
        }

        private void SelectAllEffects_Click(object sender, RoutedEventArgs e)
        {
            _selectedEffects.Clear();
            foreach (var child in EffectsList.Children)
                if (child is CheckBox cb) { cb.IsChecked = true; _selectedEffects.Add((string)cb.Tag); }
            UpdateEffectCount();
        }

        private void ClearEffects_Click(object sender, RoutedEventArgs e)
        {
            _selectedEffects.Clear();
            foreach (var child in EffectsList.Children)
                if (child is CheckBox cb) cb.IsChecked = false;
            UpdateEffectCount();
        }

        private void TabImages_Click(object s, RoutedEventArgs e) { ImagesPanel.Visibility = Visibility.Visible; EffectsPanel.Visibility = Visibility.Collapsed; SettingsPanel.Visibility = Visibility.Collapsed; }
        private void TabEffects_Click(object s, RoutedEventArgs e) { ImagesPanel.Visibility = Visibility.Collapsed; EffectsPanel.Visibility = Visibility.Visible; SettingsPanel.Visibility = Visibility.Collapsed; }
        private void TabSettings_Click(object s, RoutedEventArgs e) { ImagesPanel.Visibility = Visibility.Collapsed; EffectsPanel.Visibility = Visibility.Collapsed; SettingsPanel.Visibility = Visibility.Visible; }

        private void AddImages_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "图片文件|*.jpg;*.jpeg;*.png;*.bmp;*.tiff|所有文件|*.*",
                Multiselect = true,
                Title = "选择图片"
            };
            if (dialog.ShowDialog() == true)
            {
                foreach (var file in dialog.FileNames)
                    if (!_images.Any(img => img.FilePath == file))
                        _images.Add(_imageProcessor.ProcessImage(file, _quality, _maxWidth));
                RefreshImageGrid();
                UpdateEstimate();
            }
        }

        private void RefreshImageGrid()
        {
            ImageGrid.Children.Clear();
            for (int i = 0; i < _images.Count; i++)
            {
                var item = _images[i];
                item.DisplayOrder = i + 1;
                bool isSelected = (i == _selectedIndex);
                var card = new Border
                {
                    Background = ThemeColors.Brush(ThemeColors.CardBg),
                    BorderBrush = ThemeColors.Brush(isSelected ? ThemeColors.Accent : ThemeColors.CardBorder),
                    BorderThickness = new Thickness(1.5),
                    CornerRadius = new CornerRadius(10),
                    Margin = new Thickness(6), Cursor = Cursors.Hand, Tag = i,
                    Effect = isSelected ? new System.Windows.Media.Effects.DropShadowEffect { Color = ThemeColors.Accent, BlurRadius = 12, ShadowDepth = 0, Opacity = 0.3 } : null
                };
                // 首次布局完成后设置圆角裁剪
                EventHandler layoutClip = null;
                layoutClip = (s, args) =>
                {
                    if (card.ActualWidth > 0 && card.ActualHeight > 0)
                    {
                        card.Clip = new RectangleGeometry(new Rect(0, 0, card.ActualWidth, card.ActualHeight), 9.5, 9.5);
                        card.LayoutUpdated -= layoutClip;
                    }
                };
                card.LayoutUpdated += layoutClip;
                card.MouseLeftButtonDown += ImageCard_Click;
                card.MouseLeftButtonDown += Card_MouseLeftButtonDown;
                card.MouseMove += Card_MouseMove;
                card.MouseLeftButtonUp += Card_MouseLeftButtonUp;
                card.MouseEnter += Card_MouseEnter;
                card.MouseLeave += Card_MouseLeave;

                var grid = new Grid();
                if (item.Thumbnail != null)
                    grid.Children.Add(new Image { Source = item.Thumbnail, Stretch = Stretch.UniformToFill, Height = 110 });

                grid.Children.Add(new TextBlock { Text = item.DisplayOrder.ToString("D2"), FontSize = 10, FontWeight = FontWeights.SemiBold, Foreground = ThemeColors.Brush(ThemeColors.BadgeText), FontFamily = new FontFamily("Consolas"), Margin = new Thickness(7, 1, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top });

                var removeBtn = new TextBlock { Text = "✕", FontSize = 11, Foreground = ThemeColors.Brush(ThemeColors.BadgeText), Margin = new Thickness(0, 7, 7, 0), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Cursor = Cursors.Hand, RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = new ScaleTransform(1, 1) };
                removeBtn.MouseEnter += (s, ev) => { removeBtn.Foreground = ThemeColors.Brush(ThemeColors.Accent); removeBtn.RenderTransform = new ScaleTransform(1.4, 1.4); };
                removeBtn.MouseLeave += (s, ev) => { removeBtn.Foreground = ThemeColors.Brush(ThemeColors.BadgeText); removeBtn.RenderTransform = new ScaleTransform(1, 1); };
                removeBtn.MouseLeftButtonDown += (s, ev) => { _images.Remove(item); RefreshImageGrid(); UpdateEstimate(); };
                grid.Children.Add(removeBtn);

                grid.Children.Add(new TextBlock { Text = item.FileName, FontSize = 10.5, FontWeight = FontWeights.Medium, Foreground = ThemeColors.Brush(ThemeColors.CardInfoMain), FontFamily = new FontFamily("Noto Sans SC"), TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(10, 0, 10, 0), VerticalAlignment = VerticalAlignment.Bottom });



                card.Child = grid;
                ImageGrid.Children.Add(card);
            }

            ImageCountText.Text = $"{_images.Count} 张图片";
            StatusImageCount.Text = $"{_images.Count} 张图片";
            if (_images.Count == 0) { _selectedIndex = -1; ClearPreview(); }
            else if (_selectedIndex < 0 || _selectedIndex >= _images.Count) { _selectedIndex = 0; UpdatePreview(); }
        }

        private void ImageCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging) return;
            var card = (Border)sender;
            var index = (int)card.Tag;

            // 双击打开大图预览
            if (e.ClickCount == 2)
            {
                var dialog = new ImagePreviewDialog(_images, index);
                dialog.Owner = this;
                dialog.ShowDialog();
                return;
            }

            int prevIndex = _selectedIndex;
            _selectedIndex = index;
            if (prevIndex >= 0 && prevIndex < ImageGrid.Children.Count && ImageGrid.Children[prevIndex] is Border prevCard)
            { prevCard.BorderBrush = ThemeColors.Brush(ThemeColors.CardBorder); prevCard.Effect = null; }
            card.BorderBrush = ThemeColors.Brush(ThemeColors.Accent);
            card.Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = ThemeColors.Accent, BlurRadius = 12, ShadowDepth = 0, Opacity = 0.3 };
            UpdatePreview();
        }

        // ── Drag and drop ──────────────────────────────────────────
        private void Card_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            _isDragging = false;
            _dragThresholdMet = false;
            _dragFromIndex = (int)((Border)sender).Tag;
            _draggedCard = (Border)sender;
            _mouseDownPos = e.GetPosition(this);
            _draggedCard.CaptureMouse();
        }

        private void Card_MouseMove(object sender, MouseEventArgs e)
        {
            if (_dragFromIndex < 0 || _draggedCard == null) return;

            if (!_dragThresholdMet)
            {
                var pos = e.GetPosition(this);
                if (Math.Abs(pos.X - _mouseDownPos.X) > 5 || Math.Abs(pos.Y - _mouseDownPos.Y) > 5)
                {
                    _dragThresholdMet = true;
                    _isDragging = true;
                    _draggedCard.Opacity = 0.4;
                    StartDragVisual();
                }
                return;
            }

            // 跟随鼠标移动幽灵卡片
            if (_dragGhost != null)
            {
                var pos = e.GetPosition(DragOverlay);
                Canvas.SetLeft(_dragGhost, pos.X - 97.5);
                Canvas.SetTop(_dragGhost, pos.Y - 75);
            }

            // 计算落点位置
            var posInGrid = e.GetPosition(ImageGrid);
            _dropTargetIndex = CalculateDropIndex(posInGrid);

            // 高亮目标卡片
            HighlightTargetCard(_dropTargetIndex);
        }

        private void Card_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _draggedCard?.ReleaseMouseCapture();

            if (_isDragging && _dropTargetIndex >= 0 && _dropTargetIndex != _dragFromIndex)
            {
                int insertAt = _dropTargetIndex;
                if (insertAt > _dragFromIndex) insertAt--;

                // 记录每个卡片当前的位置（用于后续平移动画）
                var oldPositions = new Dictionary<int, Point>();
                for (int i = 0; i < ImageGrid.Children.Count; i++)
                {
                    var card = (Border)ImageGrid.Children[i];
                    oldPositions[i] = card.TransformToAncestor(ImageGrid).Transform(new Point(0, 0));
                }

                // 移动数据
                var item = _images[_dragFromIndex];
                _images.RemoveAt(_dragFromIndex);
                if (insertAt >= _images.Count) _images.Add(item);
                else _images.Insert(insertAt, item);

                // 移动 WrapPanel 中的子元素
                var child = ImageGrid.Children[_dragFromIndex];
                ImageGrid.Children.RemoveAt(_dragFromIndex);
                ImageGrid.Children.Insert(insertAt, child);

                // 更新 Tag 和 DisplayOrder
                for (int i = 0; i < ImageGrid.Children.Count; i++)
                {
                    ((Border)ImageGrid.Children[i]).Tag = i;
                    _images[i].DisplayOrder = i + 1;
                }

                // 强制布局，获取新位置
                ImageGrid.UpdateLayout();

                // 平移动画：从旧位置滑到新位置
                for (int i = 0; i < ImageGrid.Children.Count; i++)
                {
                    var card = (Border)ImageGrid.Children[i];
                    if (!oldPositions.TryGetValue(i, out var oldPos)) continue;

                    var newPos = card.TransformToAncestor(ImageGrid).Transform(new Point(0, 0));
                    double dx = oldPos.X - newPos.X;
                    double dy = oldPos.Y - newPos.Y;

                    if (Math.Abs(dx) < 1 && Math.Abs(dy) < 1) continue;

                    card.RenderTransform = new TranslateTransform(dx, dy);
                    var sb = new Storyboard();
                    var animX = new DoubleAnimation(dx, 0, new Duration(TimeSpan.FromSeconds(0.3)));
                    animX.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
                    Storyboard.SetTarget(animX, card);
                    Storyboard.SetTargetProperty(animX, new PropertyPath("RenderTransform.X"));
                    var animY = new DoubleAnimation(dy, 0, new Duration(TimeSpan.FromSeconds(0.3)));
                    animY.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
                    Storyboard.SetTarget(animY, card);
                    Storyboard.SetTargetProperty(animY, new PropertyPath("RenderTransform.Y"));
                    sb.Children.Add(animX);
                    sb.Children.Add(animY);
                    sb.Completed += (s, args) => card.RenderTransform = null;
                    sb.Begin();
                }

                _selectedIndex = insertAt;
                UpdatePreview();
                ImageCountText.Text = $"{_images.Count} 张图片";
                StatusImageCount.Text = $"{_images.Count} 张图片";

                // 恢复卡片透明度
                if (_draggedCard != null) _draggedCard.Opacity = 1.0;
            }
            else
            {
                if (_draggedCard != null) _draggedCard.Opacity = 1.0;
            }

            EndDragVisual();
            _isDragging = false;
            _dragThresholdMet = false;
            _dragFromIndex = -1;
            _draggedCard = null;
            _dropTargetIndex = -1;
        }

        private void StartDragVisual()
        {
            if (_dragFromIndex < 0 || _dragFromIndex >= _images.Count || _draggedCard == null) return;

            DragOverlay.Visibility = Visibility.Visible;

            // 创建幽灵卡片（克隆视觉效果）
            var ghost = new Border
            {
                Width = 183,
                Height = 138,
                Background = ThemeColors.Brush(ThemeColors.CardBg),
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(10),
                Opacity = 0.85,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = ThemeColors.Accent,
                    BlurRadius = 16,
                    ShadowDepth = 0,
                    Opacity = 0.4
                }
            };

            // 复制缩略图 + 序号到幽灵
            var item = _images[_dragFromIndex];
            var ghostGrid = new Grid();
            if (item.Thumbnail != null)
                ghostGrid.Children.Add(new Image { Source = item.Thumbnail, Stretch = Stretch.UniformToFill, Height = 110 });
            ghostGrid.Children.Add(new TextBlock
            {
                Text = (_dragFromIndex + 1).ToString("D2"),
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = ThemeColors.Brush(ThemeColors.BadgeText),
                FontFamily = new FontFamily("Consolas"),
                Margin = new Thickness(7, 1, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            });

            // 幽灵底部文件名
            ghostGrid.Children.Add(new TextBlock { Text = item.FileName, FontSize = 11.5, FontWeight = FontWeights.Medium, Foreground = ThemeColors.Brush(ThemeColors.CardInfoMain), FontFamily = new FontFamily("Noto Sans SC"), TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(10, 0, 10, 0), VerticalAlignment = VerticalAlignment.Bottom });

            ghost.Child = ghostGrid;

            // 以窗口为公共祖先做坐标转换
            try
            {
                var pos = _draggedCard.TransformToAncestor(this).Transform(new Point(0, 0));
                var overlayOrigin = DragOverlay.TransformToAncestor(this).Transform(new Point(0, 0));
                Canvas.SetLeft(ghost, pos.X - overlayOrigin.X);
                Canvas.SetTop(ghost, pos.Y - overlayOrigin.Y);
            }
            catch
            {
                // 卡片已从视觉树移除,取消拖拽
                _isDragging = false;
                _dragThresholdMet = false;
                if (_draggedCard != null) _draggedCard.Opacity = 1.0;
                EndDragVisual();
                return;
            }
            DragOverlay.Children.Add(ghost);
            _dragGhost = ghost;
        }

        private void EndDragVisual()
        {
            DragOverlay.Children.Clear();
            DragOverlay.Visibility = Visibility.Collapsed;
            _dragGhost = null;
            ClearHighlight();
        }

        private void ClearHighlight()
        {
            if (_prevHighlightIndex >= 0 && _prevHighlightIndex < ImageGrid.Children.Count)
            {
                if (ImageGrid.Children[_prevHighlightIndex] is Border card)
                {
                    if (_prevHighlightIndex == _selectedIndex)
                    {
                        card.BorderBrush = ThemeColors.Brush(ThemeColors.Accent);
                        card.Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = ThemeColors.Accent, BlurRadius = 12, ShadowDepth = 0, Opacity = 0.3 };
                    }
                    else
                    {
                        card.BorderBrush = ThemeColors.Brush(ThemeColors.CardBorder);
                        card.Effect = null;
                    }
                }
            }
            _prevHighlightIndex = -1;
        }

        private void HighlightTargetCard(int targetIndex)
        {
            // 清除前一个高亮
            if (_prevHighlightIndex >= 0 && _prevHighlightIndex != targetIndex)
                ClearHighlight();

            if (targetIndex < 0 || targetIndex >= ImageGrid.Children.Count || targetIndex == _dragFromIndex)
            {
                _prevHighlightIndex = targetIndex;
                return;
            }

            if (ImageGrid.Children[targetIndex] is Border target)
            {
                target.BorderBrush = ThemeColors.Brush(ThemeColors.Accent);
                target.Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = ThemeColors.Accent, BlurRadius = 14, ShadowDepth = 0, Opacity = 0.5 };
                _prevHighlightIndex = targetIndex;
            }
        }

        private int CalculateDropIndex(Point posInGrid)
        {
            if (_images.Count == 0) return 0;

            double slotW = 195.0;
            double slotH = 150.0;

            // 获取 ScrollViewer 滚动偏移
            var scrollViewer = ImageGrid.Parent as ScrollViewer;
            double scrollY = scrollViewer?.VerticalOffset ?? 0;

            double x = posInGrid.X;
            double y = posInGrid.Y + scrollY;

            // 确保不超出边界
            x = Math.Max(0, Math.Min(x, ImageGrid.ActualWidth - 1));
            y = Math.Max(0, Math.Min(y, ImageGrid.ActualHeight - 1));

            int cols = Math.Max(1, (int)(ImageGrid.ActualWidth / slotW));
            int row = (int)(y / slotH);
            int col = (int)(x / slotW);

            int index = row * cols + col;
            return Math.Max(0, Math.Min(index, _images.Count));
        }



        private void UpdatePreview()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _images.Count) return;
            var item = _images[_selectedIndex];
            PreviewImage.Source = item.Thumbnail;
            PreviewFileName.Text = item.FileName;
            PreviewResolution.Text = item.ResolutionText;
            PreviewOriginalSize.Text = item.SizeText;
            PreviewCompressedSize.Text = item.CompressedSizeText;
            PreviewCompressionRatio.Text = $"{item.CompressionRatio:F0}%";
        }

        // Card hover
        private void Card_MouseEnter(object sender, MouseEventArgs e)
        {
            var card = (Border)sender;
            card.RenderTransform = new TranslateTransform(0, -2);
            if ((int)card.Tag == _selectedIndex) return;
            card.BorderBrush = ThemeColors.Brush(ThemeColors.Accent);
        }
        private void Card_MouseLeave(object sender, MouseEventArgs e)
        {
            var card = (Border)sender;
            card.RenderTransform = null;
            if ((int)card.Tag == _selectedIndex) return;
            card.BorderBrush = ThemeColors.Brush(ThemeColors.CardBorder);
        }

        private void MoveUp_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedIndex > 0) { var t = _images[_selectedIndex]; _images[_selectedIndex] = _images[_selectedIndex - 1]; _images[_selectedIndex - 1] = t; _selectedIndex--; RefreshImageGrid(); }
        }
        private void MoveDown_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedIndex >= 0 && _selectedIndex < _images.Count - 1) { var t = _images[_selectedIndex]; _images[_selectedIndex] = _images[_selectedIndex + 1]; _images[_selectedIndex + 1] = t; _selectedIndex++; RefreshImageGrid(); }
        }
        private void RemoveAllImages_Click(object sender, RoutedEventArgs e) { _images.Clear(); _selectedIndex = -1; RefreshImageGrid(); UpdateEstimate(); ClearPreview(); }
        private void ClearPreview() { PreviewImage.Source = null; PreviewFileName.Text = ""; PreviewResolution.Text = ""; PreviewOriginalSize.Text = ""; PreviewCompressedSize.Text = ""; PreviewCompressionRatio.Text = ""; }

        private void DisplayDurationMinus_Click(object s, RoutedEventArgs e) { _displayDuration = Math.Max(1.0, _displayDuration - 0.5); DisplayDurationValue.Text = _displayDuration.ToString("F1"); }
        private void DisplayDurationPlus_Click(object s, RoutedEventArgs e) { _displayDuration = Math.Min(60.0, _displayDuration + 0.5); DisplayDurationValue.Text = _displayDuration.ToString("F1"); }
        private void TransitionDurationMinus_Click(object s, RoutedEventArgs e) { _transitionDuration = Math.Max(0.3, _transitionDuration - 0.1); TransitionDurationValue.Text = _transitionDuration.ToString("F1"); }
        private void TransitionDurationPlus_Click(object s, RoutedEventArgs e) { _transitionDuration = Math.Min(5.0, _transitionDuration + 0.1); TransitionDurationValue.Text = _transitionDuration.ToString("F1"); }

        private System.Windows.Threading.DispatcherTimer _repeatTimer;
        private Action _repeatAction;
        private DateTime _repeatStartTime;
        private bool _repeatFastMode;

        private void Btn_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is Button btn)
            {
                string name = btn.Name;
                _repeatFastMode = false;
                Action act = null;
                if (name == "DisplayDurationMinusBtn") act = () =>
                {
                    if (_repeatFastMode) { _displayDuration = Math.Max(1.0, _displayDuration - 1.0); DisplayDurationValue.Text = _displayDuration.ToString("F1"); }
                    else DisplayDurationMinus_Click(btn, null);
                };
                else if (name == "DisplayDurationPlusBtn") act = () =>
                {
                    if (_repeatFastMode) { _displayDuration = Math.Min(60.0, _displayDuration + 1.0); DisplayDurationValue.Text = _displayDuration.ToString("F1"); }
                    else DisplayDurationPlus_Click(btn, null);
                };
                else if (name == "TransitionDurationMinusBtn") act = () =>
                {
                    if (_repeatFastMode) { _transitionDuration = Math.Max(0.3, _transitionDuration - 0.5); TransitionDurationValue.Text = _transitionDuration.ToString("F1"); }
                    else TransitionDurationMinus_Click(btn, null);
                };
                else if (name == "TransitionDurationPlusBtn") act = () =>
                {
                    if (_repeatFastMode) { _transitionDuration = Math.Min(5.0, _transitionDuration + 0.5); TransitionDurationValue.Text = _transitionDuration.ToString("F1"); }
                    else TransitionDurationPlus_Click(btn, null);
                };
                _repeatAction = act;
                if (_repeatAction != null)
                {
                    _repeatStartTime = DateTime.UtcNow;
                    _repeatTimer?.Stop();
                    _repeatTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
                    _repeatTimer.Tick += (s, args) =>
                    {
                        if (!btn.IsMouseCaptured) { _repeatTimer.Stop(); return; }

                        double elapsed = (DateTime.UtcNow - _repeatStartTime).TotalSeconds;
                        if (elapsed >= 1.5 && !_repeatFastMode)
                        {
                            _repeatFastMode = true;
                            _repeatTimer.Interval = TimeSpan.FromMilliseconds(50);
                        }
                        _repeatAction();
                    };
                    _repeatTimer.Start();
                }
            }
        }

        private void Btn_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e) { _repeatTimer?.Stop(); _repeatFastMode = false; }
        private void Btn_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e) { _repeatTimer?.Stop(); _repeatFastMode = false; }

        private void QualitySlider_ValueChanged(object s, RoutedPropertyChangedEventArgs<double> e)
        {
            int raw = (int)Math.Round(e.NewValue, MidpointRounding.AwayFromZero);
            int display = raw;
            // WIC JPEG 编码器在部分系统上 QualityLevel=75 映射到与 90 相同的量化表，
            // 导致压缩结果一致。此处将 75 偏移为 76 绕过此问题，显示仍保持 75
            if (raw == 75) { raw = 76; display = 75; }
            _quality = raw;
            if (QualityValue != null) QualityValue.Text = $"{display}%";
            if (StatusQuality != null) StatusQuality.Text = $"压缩质量 {display}%";

            if (_images.Count > 0)
            {
                _encodeCts?.Cancel();
                var cts = new System.Threading.CancellationTokenSource();
                _encodeCts = cts;
                int q = _quality;
                int mw = _maxWidth;
                System.Threading.Tasks.Task.Run(() =>
                {
                    _imageProcessor.UpdateAllJpegBytes(_images, q, mw, cts.Token);
                    if (!cts.IsCancellationRequested)
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            UpdateEstimate();
                            UpdatePreview();
                        }));
                });
            }
            else
            {
                UpdateEstimate();
            }
        }

        private void ResolutionRadio_Click(object sender, RoutedEventArgs e)
        {
            var rb = (RadioButton)sender;
            _maxWidth = int.Parse((string)rb.Tag);
            if (MaxWidthValue != null) MaxWidthValue.Text = _maxWidth == 0 ? "原图" : _maxWidth.ToString();
            if (_images.Count > 0)
            {
                _encodeCts?.Cancel();
                var cts = new System.Threading.CancellationTokenSource();
                _encodeCts = cts;
                int q = _quality;
                int mw = _maxWidth;
                System.Threading.Tasks.Task.Run(() =>
                {
                    _imageProcessor.UpdateAllJpegBytes(_images, q, mw, cts.Token);
                    if (!cts.IsCancellationRequested)
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            UpdateEstimate();
                            UpdatePreview();
                        }));
                });
            }
            else
            {
                UpdateEstimate();
            }
        }

        private void BrowseOutputPath_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog { Description = "选择输出路径", SelectedPath = _outputPath };
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) { _outputPath = dialog.SelectedPath; OutputPathInput.Text = _outputPath; }
        }

        private void UpdateEstimate()
        {
            if (EstimateSize == null) return;
            long totalCompressed = _images.Sum(img => img.CompressedSize);
            double totalMB = (totalCompressed + 200 * 1024 + 1024) / (1024.0 * 1024.0);
            EstimateSize.Text = totalMB.ToString("F1");
            EstimateImagesText.Text = $"图片（{_images.Count} 张 {_quality}%）";
            StatusEstimate.Text = $"预计体积 ~{totalMB:F1} MB";
        }

        private string FormatSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            return $"{bytes / (1024.0 * 1024.0):F1} MB";
        }

        private void PreviewBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_images.Count == 0) { ShowToast("请先添加图片"); return; }
            if (_selectedEffects.Count == 0) { ShowToast("请至少选择一种过渡效果"); return; }

            byte[] runtimeTemplate;
            try { runtimeTemplate = PackageBuilder.LoadEmbeddedRuntime(); }
            catch { ShowToast("找不到运行时模板文件"); return; }

            var config = new ScreensaverConfig
            {
                Version = "1.4",
                Title = SaverNameInput.Text.Trim(),
                DisplayDuration = _displayDuration,
                TransitionDuration = _transitionDuration,
                ShuffleImages = OrderRandom.IsChecked == true,
                SelectedEffects = _selectedEffects.ToArray(),
                ImageCount = _images.Count,
                CreatedBy = "PicScreenSaver v" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(3),
                CreatedAt = DateTime.UtcNow.ToString("o")
            };

            var tempPath = Path.Combine(Path.GetTempPath(), "PicScreenSaver_Preview.scr");
            try
            {
                _packageBuilder.BuildPackage(runtimeTemplate, config, _images, tempPath, _quality, _maxWidth);
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = tempPath,
                    Arguments = "/t",
                    UseShellExecute = false
                };
                System.Diagnostics.Process.Start(psi);
            }
            catch (Exception ex) { ShowToast($"预览失败：{ex.Message}", 4000); }
        }

        private void GenerateBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_images.Count == 0) { ShowToast("请先添加图片"); return; }
            if (_selectedEffects.Count == 0) { ShowToast("请至少选择一种过渡效果"); return; }
            if (string.IsNullOrWhiteSpace(SaverNameInput.Text)) { ShowToast("请输入屏保名称"); return; }
            if (string.IsNullOrWhiteSpace(_outputPath) || !Directory.Exists(_outputPath)) { ShowToast("请选择有效的输出路径"); return; }

            byte[] runtimeTemplate;
            try
            {
                runtimeTemplate = PackageBuilder.LoadEmbeddedRuntime();
            }
            catch
            {
                ShowToast("找不到运行时模板文件"); return;
            }
            var config = new ScreensaverConfig
            {
                Version = "1.4",
                Title = SaverNameInput.Text.Trim(),
                DisplayDuration = _displayDuration,
                TransitionDuration = _transitionDuration,
                ShuffleImages = OrderRandom.IsChecked == true,
                SelectedEffects = _selectedEffects.ToArray(),
                ImageCount = _images.Count,
                CreatedBy = "PicScreenSaver v" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(3),
                CreatedAt = DateTime.UtcNow.ToString("o")
            };

            var outputName = SaverNameInput.Text.Trim();
            if (string.IsNullOrEmpty(outputName)) outputName = "MyScreensaver";
            var outputPath = Path.Combine(_outputPath, outputName + ".scr");

            try
            {
                _packageBuilder.BuildPackage(runtimeTemplate, config, _images, outputPath, _quality, _maxWidth);
                if (InstallYes.IsChecked == true) { InstallScreensaver(outputPath); ShowToast($"屏保已生成并安装：{outputName}.scr"); }
                else ShowToast($"屏保已生成：{outputName}.scr");
            }
            catch (Exception ex) { ShowToast($"生成失败：{ex.Message}", 4000); }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern bool SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);
        private const int SPI_SETSCREENSAVER = 0x0011;
        private const int SPIF_SENDWININICHANGE = 0x02;

        private void InstallScreensaver(string scrPath)
        {
            string system32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
            string destPath = Path.Combine(system32, Path.GetFileName(scrPath));
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c copy /Y /B \"{scrPath}\" \"{destPath}\"",
                    Verb = "runas",
                    UseShellExecute = true,
                    CreateNoWindow = true,
                    WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
                };
                var proc = System.Diagnostics.Process.Start(psi);
                if (proc != null) proc.WaitForExit(15000);

                SetScreensaverRegistry(destPath);
            }
            catch
            {
                try
                {
                    File.Copy(scrPath, destPath, true);
                    SetScreensaverRegistry(destPath);
                }
                catch { }
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

        private void OpenProject_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "屏保项目文件 (*.ssproj)|*.ssproj|所有文件|*.*",
                Title = "打开项目"
            };
            if (dialog.ShowDialog() != true) return;

            try
            {
                var json = File.ReadAllText(dialog.FileName);
                var project = Newtonsoft.Json.JsonConvert.DeserializeObject<ProjectFile>(json);
                if (project == null) { ShowToast("项目文件格式无效"); return; }

                // 恢复设置参数
                _displayDuration = project.DisplayDuration;
                DisplayDurationValue.Text = _displayDuration.ToString("F1");
                _transitionDuration = project.TransitionDuration;
                TransitionDurationValue.Text = _transitionDuration.ToString("F1");
                _quality = project.Quality;
                QualitySlider.Value = _quality;
                _maxWidth = project.MaxWidth > 0 ? project.MaxWidth : 1920;
                if (_maxWidth == 0) ResOriginal.IsChecked = true;
                else if (_maxWidth == 2560) Res2K.IsChecked = true;
                else Res1080.IsChecked = true;
                MaxWidthValue.Text = _maxWidth == 0 ? "原图" : _maxWidth.ToString();
                _outputPath = project.OutputPath ?? _outputPath;
                OutputPathInput.Text = _outputPath;
                SaverNameInput.Text = project.SaverName ?? "MyScreensaver";
                if (project.ShuffleImages) OrderRandom.IsChecked = true;
                else OrderSequential.IsChecked = true;
                if (project.AutoInstall) InstallYes.IsChecked = true;
                else InstallNo.IsChecked = true;

                // 恢复勾选效果
                _selectedEffects.Clear();
                if (project.SelectedEffects != null)
                    foreach (var ef in project.SelectedEffects) _selectedEffects.Add(ef);
                for (int i = 0; i < EffectsList.Children.Count; i++)
                {
                    if (EffectsList.Children[i] is CheckBox cb)
                        cb.IsChecked = _selectedEffects.Contains((string)cb.Tag);
                }
                UpdateEffectCount();

                // 恢复图片列表
                _images.Clear();
                _selectedIndex = -1;
                if (project.ImagePaths != null)
                {
                    foreach (var path in project.ImagePaths)
                    {
                        if (File.Exists(path))
                        {
                            var item = _imageProcessor.ProcessImage(path, _quality, _maxWidth);
                            if (item.JpegBytes != null) _images.Add(item);
                        }
                    }
                }
                RefreshImageGrid();
                UpdateEstimate();
            }
            catch (Exception ex)
            {
                ShowToast($"打开项目失败：{ex.Message}", 4000);
            }
        }

        private void SaveProject_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "屏保项目文件 (*.ssproj)|*.ssproj|所有文件|*.*",
                Title = "保存项目",
                FileName = SaverNameInput.Text.Trim() + ".ssproj"
            };
            if (dialog.ShowDialog() != true) return;

            try
            {
                var project = new ProjectFile
                {
                    Version = "1.0",
                    DisplayDuration = _displayDuration,
                    TransitionDuration = _transitionDuration,
                    Quality = _quality,
                    MaxWidth = _maxWidth,
                    ShuffleImages = OrderRandom.IsChecked == true,
                    SelectedEffects = _selectedEffects.ToArray(),
                    OutputPath = _outputPath,
                    SaverName = SaverNameInput.Text.Trim(),
                    Description = "",
                    AutoInstall = InstallYes.IsChecked == true,
                    ImagePaths = _images.Select(img => img.FilePath).ToList(),
                    CreatedAt = DateTime.Now.ToString("o"),
                    ModifiedAt = DateTime.Now.ToString("o")
                };

                var json = Newtonsoft.Json.JsonConvert.SerializeObject(project, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(dialog.FileName, json);
            }
            catch (Exception ex)
            {
                ShowToast($"保存项目失败：{ex.Message}", 4000);
            }
        }

        private void SetThemeIcon(bool isDark)
        {
            var viewbox = new Viewbox { Width = 16, Height = 16, Stretch = Stretch.Uniform };

            string pathData;
            if (isDark)
            {
                // 月亮（新月形）
                pathData = "M164.571429 241.371429c-171.885714 171.885714-171.885714 449.828571 0 621.714285s449.828571 171.885714 621.714285 0c54.857143-54.857143 95.085714-124.342857 113.371429-193.828571l3.657143-14.628572c7.314286-25.6-10.971429-47.542857-32.914286-54.857142-14.628571-3.657143-25.6 0-36.571429 7.314285-124.342857 69.485714-285.257143 51.2-391.314285-54.857143-91.428571-91.428571-117.028571-219.428571-80.457143-332.8l3.657143-10.971428c3.657143-25.6-10.971429-51.2-32.914286-62.171429-10.971429-3.657143-21.942857-3.657143-32.914286 0-3.657143 0-3.657143 0-3.657143 3.657143-47.542857 21.942857-91.428571 51.2-131.657142 91.428572z";
            }
            else
            {
                // 太阳（圆环 + 8 道光芒，含斜角）
                pathData = "M512 511.4m-212 0a212 212 0 1 0 424 0 212 212 0 1 0-424 0Z M511.7 130.2c12.9-0.2 24.2 11.3 24.5 24.3l0.4 79.6c0.2 12.9-11.3 24.2-24.3 24.5-12.9 0.2-24.2-11.3-24.5-24.3l-0.4-79.6c0.9-14.9 11.4-24.3 24.3-24.5z M901.5 510c-0.2 12.9-9.6 23.4-24.5 24.3l-79.6-0.4c-12.9-0.2-24.5-11.5-24.3-24.5 0.2-12.9 11.5-24.5 24.5-24.3l79.6 0.4c12.9 0.3 24.5 11.6 24.3 24.5z M250.9 510c-0.2 12.9-9.6 23.4-24.5 24.3l-79.6-0.4c-12.9-0.2-24.5-11.5-24.3-24.5 0.2-12.9 11.5-24.5 24.5-24.3l79.6 0.4c12.9 0.3 24.5 11.6 24.3 24.5z M512.3 893.8c-12.9 0.2-24.2-11.3-24.5-24.3l-0.4-79.6c-0.2-12.9 11.3-24.2 24.3-24.5 12.9-0.2 24.2 11.3 24.5 24.3l0.4 79.6c-0.9 14.9-11.4 24.3-24.3 24.5z M781.2 242.3c9.3 9 9.1 25.1 0.1 34.5l-56 56.5c-9 9.3-25.1 9.1-34.5 0.1-9.3-9-9.1-25.1-0.1-34.5l56-56.5c11.1-9.9 25.1-9.1 34.5-0.1z M788.2 786.5c-9.3 9-23.3 9.8-34.5-0.1l-56-56.5c-9-9.3-9.2-25.5 0.1-34.5 9.3-9 25.5-9.2 34.5 0.1l56 56.5c9 9.3 9.2 25.5-0.1 34.5z M328.1 326.4c-9.3 9-23.3 9.8-34.5-0.1l-56-56.5c-9-9.3-9.2-25.5 0.1-34.5 9.3-9 25.5-9.2 34.5 0.1l56 56.5c9 9.4 9.3 25.5-0.1 34.5z M241.6 782.6c-9.3-9-9.1-25.1-0.1-34.5l56-56.5c9-9.3 25.1-9.1 34.5-0.1 9.3 9 9.1 25.1 0.1 34.5l-56 56.5c-11.1 9.9-25.2 9.1-34.5 0.1z";
            }

            viewbox.Child = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse(pathData),
                Fill = ThemeColors.Brush(ThemeColors.Text2),
                Stretch = Stretch.Uniform
            };

            ThemeToggleBtn.Content = viewbox;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) { }

        private void RootBorder_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateBorderClip();
            RootBorder.SizeChanged += (s, ev) => UpdateBorderClip();
        }

        private void UpdateBorderClip()
        {
            if (RootBorder.ActualWidth > 0 && RootBorder.ActualHeight > 0)
                RootBorder.Clip = new RectangleGeometry(
                    new Rect(0, 0, RootBorder.ActualWidth, RootBorder.ActualHeight), 10, 10);
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2) MaximizeBtn_Click(sender, e);
            else DragMove();
        }

        private void MinimizeBtn_Click(object s, RoutedEventArgs e) { WindowState = WindowState.Minimized; }
        private void MaximizeBtn_Click(object s, RoutedEventArgs e) { WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; }
        private void CloseBtn_Click(object s, RoutedEventArgs e) { Close(); }
        private void Window_StateChanged(object s, EventArgs e) { }

        private void WindowBtn_MouseEnter(object s, MouseEventArgs e)
        {
            var btn = (Button)s;
            UpdateBtnIconColor(btn, ThemeColors.Brush(ThemeColors.Text));
        }
        private void WindowBtn_MouseLeave(object s, MouseEventArgs e)
        {
            var btn = (Button)s;
            UpdateBtnIconColor(btn, ThemeColors.Brush(ThemeColors.Text2));
        }
        private void CloseBtn_MouseEnter(object s, MouseEventArgs e)
        {
            var btn = (Button)s;
            btn.Background = ThemeColors.Brush(ThemeColors.DangerBg);
            UpdateCloseBtnColor(btn, ThemeColors.Brush(ThemeColors.Danger));
        }
        private void CloseBtn_MouseLeave(object s, MouseEventArgs e)
        {
            var btn = (Button)s;
            btn.Background = Brushes.Transparent;
            UpdateCloseBtnColor(btn, ThemeColors.Brush(ThemeColors.Text2));
        }

        private static void UpdateBtnIconColor(Button btn, Brush brush)
        {
            if (btn.Content is Viewbox vb)
            {
                if (vb.Child is System.Windows.Shapes.Rectangle rect)
                {
                    if (rect.StrokeThickness > 0) rect.Stroke = brush;
                    else rect.Fill = brush;
                }
            }
        }

        private static void UpdateCloseBtnColor(Button btn, Brush brush)
        {
            if (btn.Content is Viewbox vb && vb.Child is Grid g)
                foreach (var child in g.Children)
                    if (child is System.Windows.Shapes.Line line) line.Stroke = brush;
        }

        private void ThemeToggleBtn_Click(object s, RoutedEventArgs e) { _isDarkTheme = !_isDarkTheme; ApplyTheme(); }

        private void ApplyTheme()
        {
            ThemeColors.SetDark(_isDarkTheme);
            SetThemeIcon(_isDarkTheme);

            string themeFile = _isDarkTheme ? "Themes/Dark.xaml" : "Themes/Light.xaml";
            Application.Current.Resources.MergedDictionaries[0] =
                new ResourceDictionary { Source = new Uri("pack://application:,,,/" + themeFile, UriKind.Absolute) };

            RefreshImageGrid();
            UpdateSliderButtons();
        }

        private void UpdateSliderButtons()
        {
            var bg = ThemeColors.Brush(ThemeColors.SliderTrack);
            var fg = ThemeColors.Brush(ThemeColors.Text2);
            DisplayDurationMinusBtn.Background = bg; DisplayDurationMinusBtn.Foreground = fg;
            DisplayDurationPlusBtn.Background = bg; DisplayDurationPlusBtn.Foreground = fg;
            TransitionDurationMinusBtn.Background = bg; TransitionDurationMinusBtn.Foreground = fg;
            TransitionDurationPlusBtn.Background = bg; TransitionDurationPlusBtn.Foreground = fg;
        }
    }
}

public class EllipseRadiusAnimation : System.Windows.Media.Animation.AnimationTimeline
{
    public override Type TargetPropertyType => typeof(System.Windows.Media.Geometry);
    private System.Windows.Point _center;
    private double _fromRX, _fromRY, _toRX, _toRY;
    private TimeSpan _duration;
    public EllipseRadiusAnimation(System.Windows.Point center, double fromRX, double fromRY, double toRX, double toRY, TimeSpan duration)
    { _center = center; _fromRX = fromRX; _fromRY = fromRY; _toRX = toRX; _toRY = toRY; _duration = duration; }
    protected override Freezable CreateInstanceCore() { return new EllipseRadiusAnimation(_center, _fromRX, _fromRY, _toRX, _toRY, _duration); }
    public override object GetCurrentValue(object defaultOriginValue, object defaultDestinationValue, System.Windows.Media.Animation.AnimationClock clock)
    {
        if (clock == null || clock.CurrentProgress == null) return new System.Windows.Media.EllipseGeometry(_center, _fromRX, _fromRY);
        double t = clock.CurrentProgress.Value;
        return new System.Windows.Media.EllipseGeometry(_center,
            _fromRX + (_toRX - _fromRX) * t,
            _fromRY + (_toRY - _fromRY) * t);
    }
}

public class DiamondRevealAnimation : System.Windows.Media.Animation.AnimationTimeline
{
    public override Type TargetPropertyType => typeof(System.Windows.Media.Geometry);
    private double _w, _h; private TimeSpan _duration;
    public DiamondRevealAnimation(double w, double h, TimeSpan duration) { _w = w; _h = h; _duration = duration; }
    protected override Freezable CreateInstanceCore() { return new DiamondRevealAnimation(_w, _h, _duration); }
    public override object GetCurrentValue(object defaultOriginValue, object defaultDestinationValue, System.Windows.Media.Animation.AnimationClock clock)
    {
        if (clock == null || clock.CurrentProgress == null) return new System.Windows.Media.RectangleGeometry(new System.Windows.Rect(0, 0, 0, 0));
        double t = clock.CurrentProgress.Value;
        double cx = _w / 2.0, cy = _h / 2.0;
        double size = Math.Max(_w, _h) * t;
        var pts = new System.Windows.Media.PointCollection(new[]
        {
            new System.Windows.Point(cx, cy - size),
            new System.Windows.Point(cx + size, cy),
            new System.Windows.Point(cx, cy + size),
            new System.Windows.Point(cx - size, cy),
        });
        var seg = new System.Windows.Media.PathSegmentCollection { new System.Windows.Media.PolyLineSegment(pts, true) };
        var fig = new System.Windows.Media.PathFigure(pts[0], seg, true);
        return new System.Windows.Media.PathGeometry(new[] { fig });
    }
}

public class BlindsGeometryAnimation : System.Windows.Media.Animation.AnimationTimeline
{
    public override Type TargetPropertyType => typeof(System.Windows.Media.Geometry);
    private double _w, _h; private int _count; private bool _horizontal; private TimeSpan _duration;
    public BlindsGeometryAnimation(double w, double h, int count, bool horizontal, TimeSpan duration)
    { _w = w; _h = h; _count = count; _horizontal = horizontal; _duration = duration; }
    protected override Freezable CreateInstanceCore() { return new BlindsGeometryAnimation(_w, _h, _count, _horizontal, _duration); }
    public override object GetCurrentValue(object defaultOriginValue, object defaultDestinationValue, System.Windows.Media.Animation.AnimationClock clock)
    {
        if (clock == null || clock.CurrentProgress == null) return new System.Windows.Media.RectangleGeometry(new System.Windows.Rect(0, 0, 0, 0));
        double t = clock.CurrentProgress.Value;
        var group = new System.Windows.Media.GeometryGroup();
        if (_horizontal)
        {
            double stripH = _h / _count;
            for (int i = 0; i < _count; i++)
            {
                double sh = stripH * t;
                if (sh > 0.5) group.Children.Add(new System.Windows.Media.RectangleGeometry(new System.Windows.Rect(0, i * stripH, _w, sh)));
            }
        }
        else
        {
            double stripW = _w / _count;
            for (int i = 0; i < _count; i++)
            {
                double sw = stripW * t;
                if (sw > 0.5) group.Children.Add(new System.Windows.Media.RectangleGeometry(new System.Windows.Rect(i * stripW, 0, sw, _h)));
            }
        }
        if (group.Children.Count == 0) return new System.Windows.Media.RectangleGeometry(new System.Windows.Rect(0, 0, 0, 0));
        return group;
    }
}

public class CheckerboardGeometryAnimation : System.Windows.Media.Animation.AnimationTimeline
{
    public override Type TargetPropertyType => typeof(System.Windows.Media.Geometry);
    private double _w, _h; private int _cols, _rows; private TimeSpan _duration;
    public CheckerboardGeometryAnimation(double w, double h, int cols, int rows, TimeSpan duration)
    { _w = w; _h = h; _cols = cols; _rows = rows; _duration = duration; }
    protected override Freezable CreateInstanceCore() { return new CheckerboardGeometryAnimation(_w, _h, _cols, _rows, _duration); }
    public override object GetCurrentValue(object defaultOriginValue, object defaultDestinationValue, System.Windows.Media.Animation.AnimationClock clock)
    {
        if (clock == null || clock.CurrentProgress == null) return new System.Windows.Media.RectangleGeometry(new System.Windows.Rect(0, 0, 0, 0));
        double t = clock.CurrentProgress.Value;
        double cw = _w / _cols, ch = _h / _rows;
        var group = new System.Windows.Media.GeometryGroup();
        for (int r = 0; r < _rows; r++)
            for (int c = 0; c < _cols; c++)
            {
                double delay = (r + c) * 0.04;
                double cellT = (t - delay) / (1.0 - 0.04 * (_rows + _cols - 2));
                if (cellT <= 0) continue;
                if (cellT > 1.0) cellT = 1.0;
                double tw = cw * cellT, th = ch * cellT;
                if (tw > 0.5 && th > 0.5)
                    group.Children.Add(new System.Windows.Media.RectangleGeometry(new System.Windows.Rect(c * cw, r * ch, tw, th)));
            }
        if (group.Children.Count == 0) return new System.Windows.Media.RectangleGeometry(new System.Windows.Rect(0, 0, 0, 0));
        return group;
    }
}

public class RadialSectorAnimation : System.Windows.Media.Animation.AnimationTimeline
{
    public override Type TargetPropertyType => typeof(System.Windows.Media.Geometry);
    private double _cx, _cy, _r; private TimeSpan _duration;
    public RadialSectorAnimation(double cx, double cy, double r, TimeSpan duration) { _cx = cx; _cy = cy; _r = r; _duration = duration; }
    protected override Freezable CreateInstanceCore() { return new RadialSectorAnimation(_cx, _cy, _r, _duration); }
    public override object GetCurrentValue(object defaultOriginValue, object defaultDestinationValue, System.Windows.Media.Animation.AnimationClock clock)
    {
        if (clock == null || clock.CurrentProgress == null) return new System.Windows.Media.RectangleGeometry(new System.Windows.Rect(0, 0, 0, 0));
        double t = clock.CurrentProgress.Value;
        if (t <= 0) return new System.Windows.Media.RectangleGeometry(new System.Windows.Rect(0, 0, 0, 0));
        if (t >= 1.0) return new System.Windows.Media.RectangleGeometry(new System.Windows.Rect(0, 0, _cx * 2, _cy * 2));
        double angle = t * 2.0 * Math.PI;
        double ex = _cx + _r * Math.Cos(angle);
        double ey = _cy + _r * Math.Sin(angle);
        bool isLarge = angle > Math.PI;
        var startPt = new System.Windows.Point(_cx + _r, _cy);
        var arcPt = new System.Windows.Point(ex, ey);
        var segs = new System.Windows.Media.PathSegmentCollection();
        segs.Add(new System.Windows.Media.ArcSegment(arcPt, new System.Windows.Size(_r, _r), 0, isLarge, System.Windows.Media.SweepDirection.Clockwise, true));
        segs.Add(new System.Windows.Media.LineSegment(new System.Windows.Point(_cx, _cy), true));
        var fig = new System.Windows.Media.PathFigure(startPt, segs, true);
        return new System.Windows.Media.PathGeometry(new[] { fig });
    }
}

/// <summary>
/// 动画 RectangleGeometry（用于 Clip 属性的擦除过渡预览）
/// </summary>
public class RectAnimation : System.Windows.Media.Animation.AnimationTimeline
{
    public override Type TargetPropertyType => typeof(System.Windows.Media.Geometry);
    private Rect _from; private Rect _to; private TimeSpan _duration;
    public RectAnimation(Rect from, Rect to, TimeSpan duration) { _from = from; _to = to; _duration = duration; }
    protected override Freezable CreateInstanceCore() { return new RectAnimation(_from, _to, _duration); }
    public override object GetCurrentValue(object defaultOriginValue, object defaultDestinationValue, System.Windows.Media.Animation.AnimationClock clock)
    {
        if (clock == null || clock.CurrentProgress == null) return new RectangleGeometry(_from);
        double t = clock.CurrentProgress.Value;
        return new RectangleGeometry(new Rect(
            _from.X + (_to.X - _from.X) * t,
            _from.Y + (_to.Y - _from.Y) * t,
            _from.Width + (_to.Width - _from.Width) * t,
            _from.Height + (_to.Height - _from.Height) * t));
    }
}
