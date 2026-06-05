using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PicScreenSaver.Runtime
{
    public partial class ConfigDialog : Window
    {
        private readonly ScreensaverConfig _config;
        private static readonly string[] AllEffects = new[]
        {
            "Fade", "FadeBlack", "FadeWhite", "CrossFade",
            "SlideLeft", "SlideRight", "SlideUp", "SlideDown",
            "ZoomInFade", "ZoomOutFade",
            "WipeLeft", "WipeRight", "WipeUp", "WipeDown",
            "FlipHorizontal", "FlipVertical",
            "PushLeft", "PushUp"
        };

        private readonly System.Collections.Generic.HashSet<string> _selectedEffects =
            new System.Collections.Generic.HashSet<string>();

        private static readonly Color AccentColor = (Color)ColorConverter.ConvertFromString("#6B8CFF");
        private static readonly Color TextColor = (Color)ColorConverter.ConvertFromString("#E4E6F0");
        private static readonly Color Text2Color = (Color)ColorConverter.ConvertFromString("#8A90A8");
        private static readonly Color Surface2Color = (Color)ColorConverter.ConvertFromString("#1E2129");

        public ConfigDialog(ScreensaverConfig config)
        {
            InitializeComponent();
            _config = config;

            string scrName = System.IO.Path.GetFileName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string systemDir = System.Environment.GetFolderPath(System.Environment.SpecialFolder.System);
            bool alreadyInstalled = System.IO.File.Exists(System.IO.Path.Combine(systemDir, scrName));
            InstallBtn.Visibility = alreadyInstalled ? Visibility.Collapsed : Visibility.Visible;

            Closing += (s, e) =>
            {
                try { System.Environment.Exit(0); } catch { }
            };

            if (config == null) return;

            InfoText.Text = $"{config.CreatedBy ?? "v1.0"}  ·  {config.ImageCount} 张图片";

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

            foreach (var effect in AllEffects)
            {
                var cb = new CheckBox
                {
                    Content = effect,
                    Tag = effect,
                    IsChecked = _selectedEffects.Contains(effect),
                    Margin = new Thickness(0, 0, 0, 2),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Foreground = new SolidColorBrush(TextColor),
                    FontSize = 12,
                    FontFamily = new FontFamily("Segoe UI")
                };
                cb.Checked += EffectCheckBox_Changed;
                cb.Unchecked += EffectCheckBox_Changed;
                EffectsList.Children.Add(cb);
            }
        }

        private void EffectCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            var cb = (CheckBox)sender;
            var effectName = (string)cb.Tag;
            if (cb.IsChecked == true)
                _selectedEffects.Add(effectName);
            else
                _selectedEffects.Remove(effectName);
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            _selectedEffects.Clear();
            foreach (var child in EffectsList.Children)
            {
                if (child is CheckBox cb)
                {
                    cb.IsChecked = true;
                    _selectedEffects.Add((string)cb.Tag);
                }
            }
        }

        private void ClearAll_Click(object sender, RoutedEventArgs e)
        {
            _selectedEffects.Clear();
            foreach (var child in EffectsList.Children)
            {
                if (child is CheckBox cb)
                    cb.IsChecked = false;
            }
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
