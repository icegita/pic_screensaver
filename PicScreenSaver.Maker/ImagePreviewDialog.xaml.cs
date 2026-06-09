using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PicScreenSaver.Maker.Models;

namespace PicScreenSaver.Maker
{
    public partial class ImagePreviewDialog : Window
    {
        private readonly List<ImageItem> _images;
        private int _currentIndex;

        public ImagePreviewDialog(List<ImageItem> images, int startIndex)
        {
            InitializeComponent();
            _images = images;
            _currentIndex = startIndex;

            ApplyTheme();
            UpdateNavigation();
            LoadCurrentImage();
            UpdateTitle();
        }

        private void ClipBorder_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateClip();
            ClipBorder.SizeChanged += (s, ev) => UpdateClip();
        }

        private void UpdateClip()
        {
            if (ClipBorder.ActualWidth > 0 && ClipBorder.ActualHeight > 0)
                ClipBorder.Clip = new RectangleGeometry(
                    new Rect(0, 0, ClipBorder.ActualWidth, ClipBorder.ActualHeight), 10, 10);
        }

        private void ApplyTheme()
        {
            var bg = ThemeColors.Brush(ThemeColors.Bg);
            ShadowHost.Background = bg;
            ClipBorder.Background = bg;
            TitleBar.Background = ThemeColors.Brush(ThemeColors.Surface);
            TitleBar.BorderBrush = ThemeColors.Brush(ThemeColors.Border);
            TitleBar.BorderThickness = new Thickness(0, 0, 0, 1);

            var text2Brush = ThemeColors.Brush(ThemeColors.Text2);
            var text3Brush = ThemeColors.Brush(ThemeColors.Text3);

            TitleText.Foreground = text2Brush;
            TitleText.Text = "PicScreenSaver";

            CloseLine1.Stroke = text2Brush;
            CloseLine2.Stroke = text2Brush;

            PrevBtnBg.Fill = text3Brush;
            NextBtnBg.Fill = text3Brush;

            PrevArrow.Foreground = text2Brush;
            NextArrow.Foreground = text2Brush;
        }

        private void UpdateTitle()
        {
            TitleText.Text = $"{_currentIndex + 1} / {_images.Count}";
        }

        private void UpdateNavigation()
        {
            PrevBtn.Visibility = _currentIndex > 0 ? Visibility.Visible : Visibility.Collapsed;
            NextBtn.Visibility = _currentIndex < _images.Count - 1 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void LoadCurrentImage()
        {
            if (_currentIndex < 0 || _currentIndex >= _images.Count)
            {
                PreviewImage.Source = null;
                return;
            }

            var item = _images[_currentIndex];
            var bytes = item.JpegBytes;
            if (bytes == null || bytes.Length == 0)
            {
                PreviewImage.Source = null;
                return;
            }

            var bitmap = new BitmapImage();
            using (var ms = new MemoryStream(bytes))
            {
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = ms;
                bitmap.EndInit();
                bitmap.Freeze();
            }
            PreviewImage.Source = bitmap;
        }

        private void GoToPrev()
        {
            if (_currentIndex <= 0) return;
            _currentIndex--;
            LoadCurrentImage();
            UpdateNavigation();
            UpdateTitle();
        }

        private void GoToNext()
        {
            if (_currentIndex >= _images.Count - 1) return;
            _currentIndex++;
            LoadCurrentImage();
            UpdateNavigation();
            UpdateTitle();
        }

        // ── 事件处理 ──────────────────────────────────────────

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                Close();
                return;
            }
            DragMove();
        }

        private void PrevBtn_Click(object sender, MouseButtonEventArgs e) => GoToPrev();
        private void NextBtn_Click(object sender, MouseButtonEventArgs e) => GoToNext();
        private void CloseBtn_Click(object sender, RoutedEventArgs e) => Close();

        private void CloseBtn_MouseEnter(object sender, MouseEventArgs e)
        {
            CloseBtn.Background = ThemeColors.Brush(ThemeColors.DangerBg);
        }

        private void CloseBtn_MouseLeave(object sender, MouseEventArgs e)
        {
            CloseBtn.Background = Brushes.Transparent;
        }

        private void NavBtn_MouseEnter(object sender, MouseEventArgs e)
        {
            var ellipse = sender == PrevBtn ? PrevBtnBg : NextBtnBg;
            ellipse.Opacity = 0.3;
        }

        private void NavBtn_MouseLeave(object sender, MouseEventArgs e)
        {
            var ellipse = sender == PrevBtn ? PrevBtnBg : NextBtnBg;
            ellipse.Opacity = 0.15;
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Escape:
                    Close();
                    break;
                case Key.Left:
                    GoToPrev();
                    break;
                case Key.Right:
                    GoToNext();
                    break;
            }
        }
    }

}
