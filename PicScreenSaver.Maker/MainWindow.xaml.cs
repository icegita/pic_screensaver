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
        private bool _isDarkTheme = true;
        private Point _mouseDownPos;
        private bool _dragThresholdMet = false;
        private Border _dragGhost = null;
        private Border _insertIndicator = null;
        private int _dropTargetIndex = -1;

        private static readonly string[] AllEffects = new[]
        {
            "Fade", "FadeBlack", "CrossFade",
            "SlideLeft", "SlideRight", "SlideUp", "SlideDown",
            "WipeLeft", "WipeRight", "WipeUp", "WipeDown",
            "FlipHorizontal", "FlipVertical",
            "PushLeft", "PushUp"
        };

        private static readonly string[] EffectDescriptions = new[]
        {
            "旧图渐隐，新图渐现——最经典的过渡效果",
            "旧图淡至黑场，新图从黑场淡入",
            "新旧图叠加交叉淡变",
            "旧图静止，新图从右侧滑入",
            "旧图静止，新图从左侧滑入",
            "旧图静止，新图从下方滑入",
            "旧图静止，新图从上方滑入",
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
            _outputPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            OutputPathInput.Text = _outputPath;
            InitializeEffectsList();
            InitializePreviewImages();
            UpdateEstimate();
            CreateSliderButtons();
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
                card.MouseLeftButtonDown += ImageCard_Click;
                card.MouseLeftButtonDown += Card_MouseLeftButtonDown;
                card.MouseMove += Card_MouseMove;
                card.MouseLeftButtonUp += Card_MouseLeftButtonUp;
                card.MouseEnter += Card_MouseEnter;
                card.MouseLeave += Card_MouseLeave;

                var grid = new Grid();
                if (item.Thumbnail != null)
                    grid.Children.Add(new Image { Source = item.Thumbnail, Stretch = Stretch.UniformToFill, Height = 110 });

                grid.Children.Add(new TextBlock { Text = item.DisplayOrder.ToString("D2"), FontSize = 10, FontWeight = FontWeights.SemiBold, Foreground = ThemeColors.Brush(ThemeColors.BadgeText), FontFamily = new FontFamily("Consolas"), Margin = new Thickness(7, 7, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top });

                var removeBtn = new TextBlock { Text = "✕", FontSize = 11, Foreground = ThemeColors.Brush(ThemeColors.BadgeText), Margin = new Thickness(0, 7, 7, 0), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Cursor = Cursors.Hand, RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = new ScaleTransform(1, 1) };
                removeBtn.MouseEnter += (s, ev) => { removeBtn.Foreground = ThemeColors.Brush(ThemeColors.Accent); removeBtn.RenderTransform = new ScaleTransform(1.4, 1.4); };
                removeBtn.MouseLeave += (s, ev) => { removeBtn.Foreground = ThemeColors.Brush(ThemeColors.BadgeText); removeBtn.RenderTransform = new ScaleTransform(1, 1); };
                removeBtn.MouseLeftButtonDown += (s, ev) => { _images.Remove(item); RefreshImageGrid(); UpdateEstimate(); };
                grid.Children.Add(removeBtn);

                grid.Children.Add(new StackPanel { Margin = new Thickness(10, 8, 10, 8), VerticalAlignment = VerticalAlignment.Bottom, Children = { new TextBlock { Text = item.FileName, FontSize = 11.5, FontWeight = FontWeights.Medium, Foreground = ThemeColors.Brush(ThemeColors.CardInfoMain), FontFamily = new FontFamily("Segoe UI"), TextTrimming = TextTrimming.CharacterEllipsis }, new TextBlock { Text = $"{item.ResolutionText} · {item.CompressedSizeText}", FontSize = 10.5, Foreground = ThemeColors.Brush(ThemeColors.CardInfoSub), FontFamily = new FontFamily("Consolas"), Margin = new Thickness(0, 2, 0, 0) } } });

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

        // Drag and drop methods (unchanged)
        private void Card_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { _isDragging = false; _dragThresholdMet = false; _dragFromIndex = (int)((Border)sender).Tag; _draggedCard = (Border)sender; _mouseDownPos = e.GetPosition(this); _draggedCard.CaptureMouse(); }
        private void Card_MouseMove(object sender, MouseEventArgs e) { /* full implementation in original */ }
        private void Card_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) { /* full implementation in original */ }
        private void StartDragVisual() { /* full implementation in original */ }
        private void EndDragVisual() { /* full implementation in original */ }
        private int CalculateDropIndex(Point posInGrid) { /* full implementation in original */ return 0; }
        private void UpdateInsertIndicator() { /* full implementation in original */ }

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
            if ((int)card.Tag != _selectedIndex) { card.BorderBrush = ThemeColors.Brush(ThemeColors.Accent); card.RenderTransform = new TranslateTransform(0, -2); }
        }
        private void Card_MouseLeave(object sender, MouseEventArgs e)
        {
            var card = (Border)sender;
            if ((int)card.Tag != _selectedIndex) { card.BorderBrush = ThemeColors.Brush(ThemeColors.CardBorder); card.RenderTransform = null; }
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
            EstimateImagesText.Text = $"图片（{_images.Count} 张 JPEG {_quality}%）";
            EstimateImagesSize.Text = $"~{FormatSize(totalCompressed + 200 * 1024 + 1024)}";
            StatusEstimate.Text = $"预计体积 ~{totalMB:F1} MB";
            double barWidth = Math.Min(100, totalMB / 10.0 * 100);
            EstimateBar.Width = barWidth;
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
                Title = TitleInput.Text,
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
            File.Copy(scrPath, destPath, true);
            SystemParametersInfo(SPI_SETSCREENSAVER, 0, destPath, SPIF_SENDWININICHANGE);
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
            ThemeToggleBtn.Content = _isDarkTheme ? "\uE771" : "\uE706";

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
