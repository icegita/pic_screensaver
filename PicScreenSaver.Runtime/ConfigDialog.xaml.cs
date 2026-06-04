using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;

namespace PicScreenSaver.Runtime
{
    public partial class ConfigDialog : Window
    {
        private readonly ScreensaverConfig _config;
        private static readonly string[] AllEffects = new[]
        {
            "Fade", "FadeBlack", "FadeWhite", "CrossFade",
            "SlideLeft", "SlideRight", "SlideUp", "SlideDown",
            "ZoomIn", "ZoomOut", "ZoomInFade", "ZoomOutFade",
            "WipeLeft", "WipeRight", "WipeUp", "WipeDown",
            "FlipHorizontal", "FlipVertical", "PushLeft", "PushUp"
        };

        private readonly System.Collections.Generic.HashSet<string> _selectedEffects =
            new System.Collections.Generic.HashSet<string>();

        public ConfigDialog(ScreensaverConfig config)
        {
            InitializeComponent();
            _config = config;

            string scrName = Path.GetFileName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string systemDir = System.Environment.GetFolderPath(System.Environment.SpecialFolder.System);
            string destPath = Path.Combine(systemDir, scrName);
            bool alreadyInstalled = File.Exists(destPath);

            Title = alreadyInstalled ? $"{scrName} 设置" : $"安装 {scrName}";
            InstallButton.Visibility = alreadyInstalled ? Visibility.Collapsed : Visibility.Visible;

            if (config == null) return;

            InfoText.Text = $"版本 {config.CreatedBy ?? "1.0"}  ·  {config.ImageCount} 张图片";

            DisplayDurationSlider.Value = config.DisplayDuration;
            DisplayDurationLabel.Text = $"{config.DisplayDuration:F1} 秒";

            TransitionDurationSlider.Value = config.TransitionDuration;
            TransitionDurationLabel.Text = $"{config.TransitionDuration:F1} 秒";

            if (config.ShuffleImages)
                OrderRandom.IsChecked = true;
            else
                OrderSequential.IsChecked = true;

            if (config.SelectedEffects != null)
                foreach (var e in config.SelectedEffects)
                    _selectedEffects.Add(e);

            foreach (var effect in AllEffects)
            {
                var cb = new System.Windows.Controls.CheckBox
                {
                    Content = new System.Windows.Controls.TextBlock
                    {
                        Text = effect,
                        Foreground = new System.Windows.Media.SolidColorBrush(
                            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E8E8F0")),
                        FontSize = 12,
                        FontFamily = new System.Windows.Media.FontFamily("Segoe UI")
                    },
                    IsChecked = _selectedEffects.Contains(effect),
                    Margin = new Thickness(0, 0, 0, 4),
                    Cursor = System.Windows.Input.Cursors.Hand
                };
                cb.Checked += EffectCheckBox_Changed;
                cb.Unchecked += EffectCheckBox_Changed;
                EffectsList.Children.Add(cb);
            }
        }

        private void EffectCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            var cb = (System.Windows.Controls.CheckBox)sender;
            var tb = cb.Content as System.Windows.Controls.TextBlock;
            var effectName = tb?.Text;
            if (effectName == null) return;
            if (cb.IsChecked == true)
                _selectedEffects.Add(effectName);
            else
                _selectedEffects.Remove(effectName);
        }

        private void DisplayDuration_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (DisplayDurationLabel != null)
                DisplayDurationLabel.Text = $"{e.NewValue:F1} 秒";
        }

        private void TransitionDuration_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TransitionDurationLabel != null)
                TransitionDurationLabel.Text = $"{e.NewValue:F1} 秒";
        }

        private void InstallButton_Click(object sender, RoutedEventArgs e)
        {
            string scrPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string systemDir = System.Environment.GetFolderPath(System.Environment.SpecialFolder.System);
            string destPath = Path.Combine(systemDir, Path.GetFileName(scrPath));

            try
            {
                File.Copy(scrPath, destPath, true);
                MessageBox.Show(
                    $"屏保已安装到：\n{destPath}\n\n请在桌面右键 → 个性化 → 锁屏界面 → 屏幕保护程序 中选择。",
                    "PicScreenSaver", MessageBoxButton.OK, MessageBoxImage.Information);
                InstallButton.Visibility = Visibility.Collapsed;
                Title = $"{Path.GetFileName(scrPath)} 设置";
            }
            catch (System.UnauthorizedAccessException)
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c copy \"{scrPath}\" \"{destPath}\" /Y",
                        Verb = "runas",
                        UseShellExecute = true,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };
                    var proc = Process.Start(psi);
                    proc.WaitForExit(10000);

                    if (proc.ExitCode == 0)
                    {
                        MessageBox.Show(
                            $"屏保已安装到：\n{destPath}\n\n请在桌面右键 → 个性化 → 锁屏界面 → 屏幕保护程序 中选择。",
                            "PicScreenSaver", MessageBoxButton.OK, MessageBoxImage.Information);
                        InstallButton.Visibility = Visibility.Collapsed;
                        Title = $"{Path.GetFileName(scrPath)} 设置";
                    }
                    else
                    {
                        MessageBox.Show(
                            $"安装失败，请手动将 .scr 复制到：\n{systemDir}",
                            "PicScreenSaver", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                catch
                {
                    MessageBox.Show(
                        $"安装需要管理员权限。\n\n请手动将 .scr 复制到：\n{systemDir}",
                        "PicScreenSaver", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(
                    $"安装失败：\n{ex.Message}",
                    "PicScreenSaver", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
