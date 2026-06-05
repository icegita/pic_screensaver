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
        private double _displayDuration = 5.0;
        private double _transitionDuration = 1.2;
        private int _quality = 75;
        private string _outputPath = "";
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
            "Fade", "FadeBlack", "FadeWhite", "CrossFade",
            "SlideLeft", "SlideRight", "SlideUp", "SlideDown",
            "ZoomInFade", "ZoomOutFade",
            "WipeLeft", "WipeRight", "WipeUp", "WipeDown",
            "FlipHorizontal", "FlipVertical",
            "PushLeft", "PushUp"
        };

        private static readonly string[] EffectDescriptions = new[]
        {
            "旧图渐隐，新图渐现——最经典的过渡效果",
            "旧图淡至黑场，新图从黑场淡入",
            "旧图淡至白场，新图从白场淡入",
            "新旧图叠加交叉淡变",
            "旧图静止，新图从右侧滑入",
            "旧图静止，新图从左侧滑入",
            "旧图静止，新图从下方滑入",
            "旧图静止，新图从上方滑入",
            "放大同时淡入",
            "缩小同时淡出",
            "遮罩从左向右展开，逐渐露出新图",
            "遮罩从右向左展开，逐渐露出新图",
            "遮罩从上向下展开，逐渐露出新图",
            "遮罩从下向上展开，逐渐露出新图",
            "以 Y 轴为中心水平翻转切换",
            "以 X 轴为中心垂直翻转切换",
            "新旧图同步向左平移（翻页感）",
            "新旧图同步向上平移（翻页感）"
        };

        private readonly HashSet<string> _selectedEffects = new HashSet<string>();

        public MainWindow()
        {
            InitializeComponent();
            ThemeColors.SetDark(false);
            _outputPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            OutputPathInput.Text = _outputPath;
            InitializeEffectsList();
            InitializePreviewImages();
            UpdateEstimate();
            CreateSliderButtons();
            SetThemeIcon(_isDarkTheme);
            UpdateSliderButtons();
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
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string img1 = Path.Combine(baseDir, "img", "pic1.jpg");
            string img2 = Path.Combine(baseDir, "img", "pic2.jpg");

            EffectPreviewImageA.Source = LoadImageOrDefault(img1, "#2E6B9E", "#1E3A5F");
            EffectPreviewImageB.Source = LoadImageOrDefault(img2, "#9E4A2E", "#5F1E3A");
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
                    EffectPreviewImageB.RenderTransform = new TranslateTransform(420, 0);
                    Panel.SetZIndex(EffectPreviewImageB, 1);
                    sb.Children.Add(CreateTranslateAnimation(EffectPreviewImageB, "X", 420, 0, duration));
                    break;
                case "SlideRight":
                    EffectPreviewImageA.Opacity = 1; EffectPreviewImageB.Opacity = 1;
                    EffectPreviewImageB.RenderTransform = new TranslateTransform(-420, 0);
                    Panel.SetZIndex(EffectPreviewImageB, 1);
                    sb.Children.Add(CreateTranslateAnimation(EffectPreviewImageB, "X", -420, 0, duration));
                    break;
                case "SlideUp":
                    EffectPreviewImageA.Opacity = 1; EffectPreviewImageB.Opacity = 1;
                    EffectPreviewImageB.RenderTransform = new TranslateTransform(0, 263);
                    Panel.SetZIndex(EffectPreviewImageB, 1);
                    sb.Children.Add(CreateTranslateAnimation(EffectPreviewImageB, "Y", 263, 0, duration));
                    break;
                case "SlideDown":
                    EffectPreviewImageA.Opacity = 1; EffectPreviewImageB.Opacity = 1;
                    EffectPreviewImageB.RenderTransform = new TranslateTransform(0, -263);
                    Panel.SetZIndex(EffectPreviewImageB, 1);
                    sb.Children.Add(CreateTranslateAnimation(EffectPreviewImageB, "Y", -263, 0, duration));
                    break;

                case "WipeLeft":
                case "WipeRight":
                case "WipeUp":
                case "WipeDown":
                    PlayWipePreview(effectName, duration);
                    return;

                case "FlipHorizontal":
                case "FlipVertical":
                    PlayFlipPreview(effectName, duration);
                    return;

                case "PushLeft":
                    EffectPreviewImageA.Opacity = 1; EffectPreviewImageB.Opacity = 1;
                    EffectPreviewImageA.RenderTransform = new TranslateTransform(0, 0);
                    EffectPreviewImageB.RenderTransform = new TranslateTransform(420, 0);
                    Panel.SetZIndex(EffectPreviewImageB, 1);
                    sb.Children.Add(CreateTranslateAnimation(EffectPreviewImageA, "X", 0, -420, duration));
                    sb.Children.Add(CreateTranslateAnimation(EffectPreviewImageB, "X", 420, 0, duration));
                    break;

                case "PushUp":
                    EffectPreviewImageA.Opacity = 1; EffectPreviewImageB.Opacity = 1;
                    EffectPreviewImageA.RenderTransform = new TranslateTransform(0, 0);
                    EffectPreviewImageB.RenderTransform = new TranslateTransform(0, 263);
                    Panel.SetZIndex(EffectPreviewImageB, 1);
                    sb.Children.Add(CreateTranslateAnimation(EffectPreviewImageA, "Y", 0, -263, duration));
                    sb.Children.Add(CreateTranslateAnimation(EffectPreviewImageB, "Y", 263, 0, duration));
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
            double pw = 420, ph = 263;
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

        private void UpdateEffectCount() { SelectedEffectCount.Text = _selectedEffects.Count.ToString(); }

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
                        _images.Add(_imageProcessor.ProcessImage(file, _quality));
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

                grid.Children.Add(new TextBlock { Text = item.FileName, FontSize = 11.5, FontWeight = FontWeights.Medium, Foreground = ThemeColors.Brush(ThemeColors.CardInfoMain), FontFamily = new FontFamily("Segoe UI"), TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(10, 0, 10, 1), VerticalAlignment = VerticalAlignment.Bottom });



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
            ghostGrid.Children.Add(new TextBlock { Text = item.FileName, FontSize = 11.5, FontWeight = FontWeights.Medium, Foreground = ThemeColors.Brush(ThemeColors.CardInfoMain), FontFamily = new FontFamily("Segoe UI"), TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(10, 0, 10, 0), VerticalAlignment = VerticalAlignment.Bottom });

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
        private void QualitySlider_ValueChanged(object s, RoutedPropertyChangedEventArgs<double> e) { _quality = (int)e.NewValue; if (QualityValue != null) QualityValue.Text = $"{_quality}%"; if (StatusQuality != null) StatusQuality.Text = $"压缩质量 {_quality}%"; UpdateEstimate(); }

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

        private void GenerateBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_images.Count == 0) { MessageBox.Show("请先添加图片。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (_selectedEffects.Count == 0) { MessageBox.Show("请至少选择一种过渡效果。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (string.IsNullOrWhiteSpace(SaverNameInput.Text)) { MessageBox.Show("请输入屏保名称。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (string.IsNullOrWhiteSpace(_outputPath) || !Directory.Exists(_outputPath)) { MessageBox.Show("请选择有效的输出路径。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

            var runtimePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PicScreenSaver.Runtime.exe");
            if (!File.Exists(runtimePath)) { MessageBox.Show("找不到运行时模板文件 PicScreenSaver.Runtime.exe。\n请确保它与制作器在同一目录下。", "错误", MessageBoxButton.OK, MessageBoxImage.Error); return; }

            var runtimeTemplate = File.ReadAllBytes(runtimePath);
            var config = new ScreensaverConfig
            {
                Version = "1.4",
                Title = SaverNameInput.Text.Trim(),
                DisplayDuration = _displayDuration,
                TransitionDuration = _transitionDuration,
                ShuffleImages = OrderRandom.IsChecked == true,
                SelectedEffects = _selectedEffects.ToArray(),
                ImageCount = _images.Count,
                CreatedBy = "PicScreenSaver v1.0",
                CreatedAt = DateTime.UtcNow.ToString("o")
            };

            var outputName = SaverNameInput.Text.Trim();
            if (string.IsNullOrEmpty(outputName)) outputName = "MyScreensaver";
            var outputPath = Path.Combine(_outputPath, outputName + ".scr");

            try
            {
                _packageBuilder.BuildPackage(runtimeTemplate, config, _images, outputPath);
                if (InstallYes.IsChecked == true) { InstallScreensaver(outputPath); MessageBox.Show($"屏保已生成并安装：\n{outputPath}", "成功", MessageBoxButton.OK, MessageBoxImage.Information); }
                else MessageBox.Show($"屏保已生成：\n{outputPath}", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { MessageBox.Show($"生成失败：\n{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error); }
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
            }
            catch
            {
                try { File.Copy(scrPath, destPath, true); }
                catch { }
            }
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
                if (project == null) { MessageBox.Show("项目文件格式无效。", "错误", MessageBoxButton.OK, MessageBoxImage.Error); return; }

                // 恢复设置参数
                _displayDuration = project.DisplayDuration;
                DisplayDurationValue.Text = _displayDuration.ToString("F1");
                _transitionDuration = project.TransitionDuration;
                TransitionDurationValue.Text = _transitionDuration.ToString("F1");
                _quality = project.Quality;
                QualitySlider.Value = _quality;
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
                            var item = _imageProcessor.ProcessImage(path, _quality);
                            if (item.JpegBytes != null) _images.Add(item);
                        }
                    }
                }
                RefreshImageGrid();
                UpdateEstimate();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开项目失败：\n{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DebugBtn_Click(object sender, RoutedEventArgs e)
        {
            var runtimePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PicScreenSaver.Runtime.exe");
            if (!File.Exists(runtimePath))
            {
                MessageBox.Show("未找到 PicScreenSaver.Runtime.exe", "调试", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var menu = new ContextMenu();
            var testItem = new MenuItem { Header = "/s 全屏测试" };
            testItem.Click += (s, args) => System.Diagnostics.Process.Start(runtimePath, "/s");
            var configItem = new MenuItem { Header = "/c 设置弹窗" };
            configItem.Click += (s, args) => System.Diagnostics.Process.Start(runtimePath, "/c");
            var previewItem = new MenuItem { Header = "/p 预览(需HWND)" };
            previewItem.Click += (s, args) => System.Diagnostics.Process.Start(runtimePath, "/p");
            var noArgsItem = new MenuItem { Header = "(无参数)" };
            noArgsItem.Click += (s, args) => System.Diagnostics.Process.Start(runtimePath);

            menu.Items.Add(testItem);
            menu.Items.Add(configItem);
            menu.Items.Add(previewItem);
            menu.Items.Add(noArgsItem);
            menu.IsOpen = true;
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
                MessageBox.Show($"保存项目失败：\n{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2) MaximizeBtn_Click(sender, e);
            else DragMove();
        }

        private void MinimizeBtn_Click(object s, RoutedEventArgs e) { WindowState = WindowState.Minimized; }
        private void MaximizeBtn_Click(object s, RoutedEventArgs e) { WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; }
        private void CloseBtn_Click(object s, RoutedEventArgs e) { Close(); }
        private void Window_StateChanged(object s, EventArgs e) { }

        private void WindowBtn_MouseEnter(object s, MouseEventArgs e) { ((Button)s).Foreground = ThemeColors.Brush(ThemeColors.Text); }
        private void WindowBtn_MouseLeave(object s, MouseEventArgs e) { ((Button)s).Foreground = ThemeColors.Brush(ThemeColors.Text2); }
        private void CloseBtn_MouseEnter(object s, MouseEventArgs e) { ((Button)s).Foreground = ThemeColors.Brush(ThemeColors.Danger); ((Button)s).Background = ThemeColors.Brush(ThemeColors.DangerBg); }
        private void CloseBtn_MouseLeave(object s, MouseEventArgs e) { ((Button)s).Foreground = ThemeColors.Brush(ThemeColors.Text2); ((Button)s).Background = Brushes.Transparent; }

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
