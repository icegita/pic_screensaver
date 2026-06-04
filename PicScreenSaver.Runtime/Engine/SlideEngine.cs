using System;
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
        private int _currentIndex;
        private int _nextIndex;
        private int _imageCount;
        private byte[][] _imageCache;
        private bool _isPreloading;

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

            _displayTimer = new DispatcherTimer();
            _displayTimer.Tick += DisplayTimer_Tick;
        }

        public void Start()
        {
            if (_imageCount == 0) return;

            _currentIndex = 0;
            _nextIndex = 1;

            LoadAndDisplayImage(_outgoingImage, _currentIndex);
            _outgoingImage.Opacity = 1.0;
            _incomingImage.Opacity = 0.0;

            _state = EngineState.Displaying;
            _displayTimer.Interval = TimeSpan.FromSeconds(_config.DisplayDuration);
            _displayTimer.Start();

            PreloadNext();
        }

        public void Stop()
        {
            _displayTimer.Stop();
            _state = EngineState.Idle;
        }

        private void DisplayTimer_Tick(object sender, EventArgs e)
        {
            _displayTimer.Stop();

            if (_state != EngineState.Displaying) return;
            if (_imageCount <= 1) return;

            _state = EngineState.Transitioning;
            PerformTransition();
        }

        private void PerformTransition()
        {
            LoadAndDisplayImage(_incomingImage, _nextIndex);

            var transition = _transitionManager.Next();
            var storyboard = transition.Build(_outgoingImage, _incomingImage, _config.TransitionDuration);

            storyboard.Completed += (s, e) =>
            {
                _outgoingImage.Source = _incomingImage.Source;
                _outgoingImage.Opacity = 1.0;
                _incomingImage.Opacity = 0.0;
                ResetTransforms(_outgoingImage);
                ResetTransforms(_incomingImage);

                _currentIndex = _nextIndex;
                _nextIndex = (_nextIndex + 1) % _imageCount;

                _state = EngineState.Displaying;
                _displayTimer.Interval = TimeSpan.FromSeconds(_config.DisplayDuration);
                _displayTimer.Start();

                PreloadNext();

                ImageChanged?.Invoke(_currentIndex, _nextIndex);
            };

            _state = EngineState.Transitioning;
            storyboard.Begin();
        }

        private void LoadAndDisplayImage(Image image, int index)
        {
            var bytes = ResourceLoader.GetImageBytes(index);
            if (bytes == null) return;

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

        private void PreloadNext()
        {
            if (_isPreloading) return;
            if (_imageCount <= 1) return;

            _isPreloading = true;
            int preloadIndex = _nextIndex;

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
            Panel.SetZIndex(element, 0);
        }
    }
}
