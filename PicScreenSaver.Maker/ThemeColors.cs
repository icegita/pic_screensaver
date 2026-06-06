using System.Windows.Media;

namespace PicScreenSaver.Maker
{
    public static class ThemeColors
    {
        public static Color Bg => _isDark ? ColorFromHex("#0F1117") : ColorFromHex("#F0F1F5");
        public static Color Surface => _isDark ? ColorFromHex("#171A22") : ColorFromHex("#FFFFFF");
        public static Color Surface2 => _isDark ? ColorFromHex("#1E2129") : ColorFromHex("#E8E9EF");
        public static Color Surface3 => _isDark ? ColorFromHex("#252932") : ColorFromHex("#DDDEE6");
        public static Color Border => _isDark ? ColorFromHex("#2C3040") : ColorFromHex("#CDD0DA");
        public static Color Border2 => _isDark ? ColorFromHex("#363B4E") : ColorFromHex("#B0B4C2");
        public static Color Accent => _isDark ? ColorFromHex("#6B8CFF") : ColorFromHex("#5570E8");
        public static Color Text => _isDark ? ColorFromHex("#E4E6F0") : ColorFromHex("#1A1D2A");
        public static Color Text2 => _isDark ? ColorFromHex("#8A90A8") : ColorFromHex("#555A70");
        public static Color Text3 => _isDark ? ColorFromHex("#565C74") : ColorFromHex("#8A90A6");
        public static Color Success => _isDark ? ColorFromHex("#3ECF8E") : ColorFromHex("#2AAF6A");
        public static Color Danger => _isDark ? ColorFromHex("#F06C6C") : ColorFromHex("#DC4545");
        public static Color DangerBg => _isDark ? ColorFromHex("#F06C6C40") : ColorFromHex("#DC454540");
        public static Color PreviewBg => _isDark ? ColorFromHex("#0A0B10") : ColorFromHex("#E4E5EB");
        public static Color CardBg => _isDark ? ColorFromHex("#1F1F23") : ColorFromHex("#F5F6FA");
        public static Color CardBorder => _isDark ? ColorFromHex("#38383F") : ColorFromHex("#D8DAE2");
        public static Color CardInfoSub => _isDark ? ColorFromHex("#606070") : ColorFromHex("#8A8FA6");
        public static Color CardInfoMain => _isDark ? ColorFromHex("#E8E8F0") : ColorFromHex("#1A1D2A");
        public static Color BadgeText => _isDark ? ColorFromHex("#9898A8") : ColorFromHex("#6B7090");
        public static Color SliderTrack => _isDark ? ColorFromHex("#252932") : ColorFromHex("#D0D2DC");
        public static Color TextBoxBg => _isDark ? ColorFromHex("#1E2129") : ColorFromHex("#F0F1F5");
        public static Color BtnBg => _isDark ? ColorFromHex("#1E2129") : ColorFromHex("#E8E9EF");
        public static Color BtnHoverBg => _isDark ? ColorFromHex("#252932") : ColorFromHex("#D8DAE2");

        private static bool _isDark = true;
        public static bool IsDark => _isDark;
        public static void SetDark(bool isDark) => _isDark = isDark;

        public static SolidColorBrush Brush(Color c) => new SolidColorBrush(c);

        private static Color ColorFromHex(string hex)
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }
    }
}
