using System;
using System.Collections.Generic;
using System.Linq;
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
        private int _posInOrder;        // index into _playOrder
        private int _imageCount;
        private int[] _playOrder;       // shuffled or sequential playback sequence
        private byte[][] _imageCache;
        private bool _isPreloading;

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
            for (int i = 0; i < _imageCount; i++)
                _imageCache[i] = null;

            // 构建播放顺序
            _playOrder = Enumerable.Range(0, _imageCount).ToArray();
            if (config.ShuffleImages)
            {
                // Fisher-Yates shuffle
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
            // 1x1 黑色占位图，用于图片加载失败时的后备
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
            if (_isPreloading) return;
            if (_imageCount <= 1) return;

            _isPreloading = true;
            int preloadIndex = imageIndex;

            Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    var bytes = ResourceLoader.GetImageBytes(preloadIndex);
                    if (bytes != null)
                        _imageCache[preloadIndex] = bytes;
                }
                finally
                {
                    _isPreloading = false;
                }
            }));
        }

        private void ResetTransforms(FrameworkElement element)
        {
            element.RenderTransform = null;
            element.Clip = null;
            element.Effect = null;
            Panel.SetZIndex(element, 0);
        }

        /// <summary>
        /// 清理过渡特效可能遗留的动态元素（如 FadeWhite 的白色遮罩 Rectangle）
        /// </summary>
        private void CleanupEffects()
        {
            var parent = _outgoingImage.Parent as Panel;
            if (parent == null) return;

            // 移除动态添加的 Rectangle 叠加层（FadeWhite 等留下的）
            var toRemove = new List<UIElement>();
            foreach (var child in parent.Children)
            {
                if (child is System.Windows.Shapes.Rectangle && child != _outgoingImage && child != _incomingImage)
                    toRemove.Add((UIElement)child);
            }
            foreach (var item in toRemove)
                parent.Children.Remove(item);
        }
    }
}
