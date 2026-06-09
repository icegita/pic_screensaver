using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace PicScreenSaver.Runtime.Engine
{
    public enum EngineState
    {
        Idle,
        Displaying,
        Transitioning
    }

    public class SlideEngine
    {
        private readonly ScreensaverConfig _config;
        private readonly TransitionManager _transitionManager;
        private readonly DispatcherTimer _displayTimer;
        private readonly Image _outgoingImage;
        private readonly Image _incomingImage;

        private EngineState _state = EngineState.Idle;
        private bool _inTransition = false;
        private int _posInOrder;
        private int _imageCount;
        private int[] _playOrder;
        private byte[][] _imageCache;

        // 修复 #2：用 Task 替代 BeginInvoke，预加载在真正的后台线程执行
        private Task _preloadTask = Task.CompletedTask;

        private static readonly Random _rng = new Random();

        public event Action<int, int> ImageChanged;

        public SlideEngine(ScreensaverConfig config, Image outgoing, Image incoming)
        {
            _config = config;
            _outgoingImage = outgoing;
            _incomingImage = incoming;
            _transitionManager = new TransitionManager(config.SelectedEffects);
            _imageCount = config.ImageCount;

            _imageCache = new byte[_imageCount][];

            _playOrder = Enumerable.Range(0, _imageCount).ToArray();
            if (config.ShuffleImages)
            {
                for (int i = _playOrder.Length - 1; i > 0; i--)
                {
                    int j = _rng.Next(i + 1);
                    int tmp = _playOrder[i];
                    _playOrder[i] = _playOrder[j];
                    _playOrder[j] = tmp;
                }
            }

            _displayTimer = new DispatcherTimer();
            _displayTimer.Tick += DisplayTimer_Tick;
        }

        public void Start()
        {
            if (_imageCount == 0) return;

            _posInOrder = 0;
            int firstIndex = _playOrder[0];
            int secondIndex = _imageCount > 1 ? _playOrder[1 % _imageCount] : firstIndex;

            LoadAndDisplayImage(_outgoingImage, firstIndex);
            _outgoingImage.Opacity = 1.0;
            _incomingImage.Opacity = 0.0;

            // 静止显示时用高质量缩放
            SetScalingMode(_outgoingImage, BitmapScalingMode.HighQuality);
            SetScalingMode(_incomingImage, BitmapScalingMode.HighQuality);

            _state = EngineState.Displaying;
            _displayTimer.Interval = TimeSpan.FromSeconds(_config.DisplayDuration);
            _displayTimer.Start();

            if (_imageCount > 1)
                PreloadNext(secondIndex);
        }

        public void Stop()
        {
            _displayTimer.Stop();
            _state = EngineState.Idle;
            CleanupEffects();
        }

        private void DisplayTimer_Tick(object sender, EventArgs e)
        {
            _displayTimer.Stop();
            if (_state != EngineState.Displaying) return;
            if (_imageCount <= 1) return;
            PerformTransition();
        }

        private void PerformTransition()
        {
            if (_inTransition) return;
            _inTransition = true;

            int nextImageIndex = _playOrder[(_posInOrder + 1) % _imageCount];
            LoadAndDisplayImage(_incomingImage, nextImageIndex);

            // 修复 #2：动画过程中降低缩放质量，减少 GPU/CPU 负担
            SetScalingMode(_outgoingImage, BitmapScalingMode.LowQuality);
            SetScalingMode(_incomingImage, BitmapScalingMode.LowQuality);

            var transition = _transitionManager.Next();
            var storyboard = transition.Build(_outgoingImage, _incomingImage, _config.TransitionDuration);

            storyboard.Completed += (s, e) =>
            {
                _inTransition = false;
                _outgoingImage.Source = _incomingImage.Source;
                _outgoingImage.Opacity = 1.0;
                _incomingImage.Opacity = 0.0;
                ResetTransforms(_outgoingImage);
                ResetTransforms(_incomingImage);
                CleanupEffects();

                // 动画结束，恢复高质量缩放
                SetScalingMode(_outgoingImage, BitmapScalingMode.HighQuality);
                SetScalingMode(_incomingImage, BitmapScalingMode.HighQuality);

                _posInOrder = (_posInOrder + 1) % _imageCount;
                int nextPos = (_posInOrder + 1) % _imageCount;
                int preloadIdx = _playOrder[nextPos];

                _state = EngineState.Displaying;
                _displayTimer.Interval = TimeSpan.FromSeconds(_config.DisplayDuration);
                _displayTimer.Start();

                PreloadNext(preloadIdx);

                int currentRealIndex = _playOrder[_posInOrder];
                int nextRealIndex = _playOrder[nextPos];
                ImageChanged?.Invoke(currentRealIndex, nextRealIndex);
            };

            _state = EngineState.Transitioning;
            storyboard.Begin();
        }

        private static readonly BitmapSource _blackPlaceholder;

        static SlideEngine()
        {
            _blackPlaceholder = new WriteableBitmap(1, 1, 96, 96, PixelFormats.Bgra32, null);
            _blackPlaceholder.Freeze();
        }

        private void LoadAndDisplayImage(Image image, int index)
        {
            var bytes = _imageCache[index] ?? ResourceLoader.GetImageBytes(index);
            if (bytes == null)
            {
                image.Source = _blackPlaceholder;
                return;
            }

            // 缓存原始字节，避免重复读 PE 资源
            if (_imageCache[index] == null)
                _imageCache[index] = bytes;

            var bitmap = new BitmapImage();
            using (var ms = new System.IO.MemoryStream(bytes))
            {
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = ms;
                bitmap.EndInit();
                bitmap.Freeze();
            }
            image.Source = bitmap;
        }

        private void PreloadNext(int imageIndex)
        {
            // 修复 #3：已缓存则跳过，未缓存才预加载
            if (_imageCache[imageIndex] != null) return;
            if (_imageCount <= 1) return;

            // 修复 #3：在真正的后台线程读取，完成后切回主线程写缓存
            // 用 Task 而非 BeginInvoke，不占用 UI 线程
            if (!_preloadTask.IsCompleted) return; // 上一个预加载未完成则跳过，不排队

            int capturedIndex = imageIndex;
            _preloadTask = Task.Run(() =>
            {
                return ResourceLoader.GetImageBytes(capturedIndex);
            }).ContinueWith(t =>
            {
                if (t.Result != null)
                    _imageCache[capturedIndex] = t.Result;
            }, TaskScheduler.FromCurrentSynchronizationContext());
            // ContinueWith 回主线程只写一个引用赋值，几乎不耗时
        }

        private void ResetTransforms(FrameworkElement element)
        {
            // 修复 Win7 黑屏：先移除动画钟（HoldEnd 持有值优先级高于本地值）
            element.BeginAnimation(FrameworkElement.RenderTransformProperty, null);
            element.BeginAnimation(FrameworkElement.RenderTransformOriginProperty, null);
            element.BeginAnimation(UIElement.ClipProperty, null);
            element.BeginAnimation(UIElement.EffectProperty, null);
            element.BeginAnimation(UIElement.OpacityProperty, null);

            element.RenderTransform = null;
            element.RenderTransformOrigin = new Point(0, 0);
            element.Clip = null;
            element.Effect = null;
            Panel.SetZIndex(element, 0);

            // Win7 DirectX9 下强制刷新画布
            element.InvalidateVisual();
        }

        private void CleanupEffects()
        {
            var parent = _outgoingImage.Parent as Panel;
            if (parent == null) return;

            var toRemove = new List<UIElement>();
            foreach (var child in parent.Children)
            {
                if (child is System.Windows.Shapes.Rectangle
                    && child != _outgoingImage
                    && child != _incomingImage)
                    toRemove.Add((UIElement)child);
            }
            foreach (var item in toRemove)
                parent.Children.Remove(item);
        }

        // 修复 #2：统一设置缩放模式的辅助方法
        private static void SetScalingMode(Image image, BitmapScalingMode mode)
        {
            RenderOptions.SetBitmapScalingMode(image, mode);
        }
    }
}