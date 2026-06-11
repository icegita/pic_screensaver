using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace PicScreenSaver.Runtime.Engine.Transitions
{
    public class FadeTransition : ITransition
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }

        private FadeTransition(string id, string name, string description)
        {
            Id = id;
            Name = name;
            Description = description;
        }

        public static readonly FadeTransition Fade = new FadeTransition(
            "Fade", "Fade", "旧图渐隐，新图渐现——最经典的过渡效果");

        public static readonly FadeTransition FadeBlack = new FadeTransition(
            "FadeBlack", "FadeBlack", "旧图淡至黑场，新图从黑场淡入");

        public static readonly FadeTransition FadeWhite = new FadeTransition(
            "FadeWhite", "FadeWhite", "旧图淡至白场，新图从白场淡入");

        public static readonly FadeTransition CrossFade = new FadeTransition(
            "CrossFade", "CrossFade", "新旧图叠加交叉淡变");

        public Storyboard Build(FrameworkElement outgoing, FrameworkElement incoming, double duration)
        {
            switch (Id)
            {
                case "Fade": return BuildFade(outgoing, incoming, duration);
                case "FadeBlack": return BuildFadeBlack(outgoing, incoming, duration);
                case "FadeWhite": return BuildFadeWhite(outgoing, incoming, duration);
                case "CrossFade": return BuildCrossFade(outgoing, incoming, duration);
                default: return BuildFade(outgoing, incoming, duration);
            }
        }

        private Storyboard BuildFade(FrameworkElement outgoing, FrameworkElement incoming, double duration)
        {
            var sb = new Storyboard(); sb.Duration = new Duration(TimeSpan.FromSeconds(duration));
            var time = TimeSpan.FromSeconds(duration);

            outgoing.Opacity = 1.0;
            incoming.Opacity = 0.0;

            var fadeOut = new DoubleAnimation(1.0, 0.0, new Duration(time));
            Storyboard.SetTarget(fadeOut, outgoing);
            Storyboard.SetTargetProperty(fadeOut, new PropertyPath(UIElement.OpacityProperty));

            var fadeIn = new DoubleAnimation(0.0, 1.0, new Duration(time));
            Storyboard.SetTarget(fadeIn, incoming);
            Storyboard.SetTargetProperty(fadeIn, new PropertyPath(UIElement.OpacityProperty));

            sb.Children.Add(fadeOut);
            sb.Children.Add(fadeIn);
            return sb;
        }

        private Storyboard BuildFadeBlack(FrameworkElement outgoing, FrameworkElement incoming, double duration)
        {
            var sb = new Storyboard(); sb.Duration = new Duration(TimeSpan.FromSeconds(duration));
            var halfTime = TimeSpan.FromSeconds(duration / 2.0);

            outgoing.Opacity = 1.0;
            incoming.Opacity = 0.0;

            var fadeOut = new DoubleAnimation(1.0, 0.0, new Duration(halfTime));
            Storyboard.SetTarget(fadeOut, outgoing);
            Storyboard.SetTargetProperty(fadeOut, new PropertyPath(UIElement.OpacityProperty));

            var fadeIn = new DoubleAnimation(0.0, 1.0, new Duration(halfTime));
            Storyboard.SetTarget(fadeIn, incoming);
            Storyboard.SetTargetProperty(fadeIn, new PropertyPath(UIElement.OpacityProperty));
            fadeIn.BeginTime = halfTime;

            sb.Children.Add(fadeOut);
            sb.Children.Add(fadeIn);
            return sb;
        }

        private Storyboard BuildFadeWhite(FrameworkElement outgoing, FrameworkElement incoming, double duration)
        {
            var sb = new Storyboard(); sb.Duration = new Duration(TimeSpan.FromSeconds(duration));
            var halfTime = TimeSpan.FromSeconds(duration / 2.0);

            var whiteOverlay = new Rectangle
            {
                Fill = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Opacity = 0
            };

            var parent = outgoing.Parent as Panel;
            if (parent != null)
                parent.Children.Insert(1, whiteOverlay);

            Panel.SetZIndex(outgoing, 0);
            Panel.SetZIndex(whiteOverlay, 1);
            Panel.SetZIndex(incoming, 2);

            outgoing.Opacity = 1.0;
            incoming.Opacity = 0.0;

            var fadeOut = new DoubleAnimation(1.0, 0.0, new Duration(halfTime));
            Storyboard.SetTarget(fadeOut, outgoing);
            Storyboard.SetTargetProperty(fadeOut, new PropertyPath(UIElement.OpacityProperty));

            var showWhite = new DoubleAnimation(0.0, 1.0, new Duration(halfTime));
            Storyboard.SetTarget(showWhite, whiteOverlay);
            Storyboard.SetTargetProperty(showWhite, new PropertyPath(UIElement.OpacityProperty));

            var hideWhite = new DoubleAnimation(1.0, 0.0, new Duration(halfTime));
            Storyboard.SetTarget(hideWhite, whiteOverlay);
            Storyboard.SetTargetProperty(hideWhite, new PropertyPath(UIElement.OpacityProperty));
            hideWhite.BeginTime = halfTime;

            var fadeIn = new DoubleAnimation(0.0, 1.0, new Duration(halfTime));
            Storyboard.SetTarget(fadeIn, incoming);
            Storyboard.SetTargetProperty(fadeIn, new PropertyPath(UIElement.OpacityProperty));
            fadeIn.BeginTime = halfTime;

            sb.Children.Add(fadeOut);
            sb.Children.Add(showWhite);
            sb.Children.Add(hideWhite);
            sb.Children.Add(fadeIn);

            sb.Completed += (s, e) =>
            {
                if (parent != null && parent.Children.Contains(whiteOverlay))
                    parent.Children.Remove(whiteOverlay);
            };

            return sb;
        }

        private Storyboard BuildCrossFade(FrameworkElement outgoing, FrameworkElement incoming, double duration)
        {
            var sb = new Storyboard(); sb.Duration = new Duration(TimeSpan.FromSeconds(duration));
            var time = TimeSpan.FromSeconds(duration);

            outgoing.Opacity = 1.0;
            incoming.Opacity = 0.0;
            Panel.SetZIndex(incoming, 1);

            var fadeOut = new DoubleAnimation(1.0, 0.0, new Duration(time));
            Storyboard.SetTarget(fadeOut, outgoing);
            Storyboard.SetTargetProperty(fadeOut, new PropertyPath(UIElement.OpacityProperty));

            var fadeIn = new DoubleAnimation(0.0, 1.0, new Duration(time));
            Storyboard.SetTarget(fadeIn, incoming);
            Storyboard.SetTargetProperty(fadeIn, new PropertyPath(UIElement.OpacityProperty));

            sb.Children.Add(fadeOut);
            sb.Children.Add(fadeIn);
            return sb;
        }
    }
}
