using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace PicScreenSaver.Runtime.Engine.Transitions
{
    public class SlideTransition : ITransition
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }

        private SlideTransition(string id, string name, string description)
        {
            Id = id;
            Name = name;
            Description = description;
        }

        public static readonly SlideTransition SlideLeft = new SlideTransition(
            "SlideLeft", "SlideLeft", "旧图静止，新图从右侧滑入");

        public static readonly SlideTransition SlideRight = new SlideTransition(
            "SlideRight", "SlideRight", "旧图静止，新图从左侧滑入");

        public static readonly SlideTransition SlideUp = new SlideTransition(
            "SlideUp", "SlideUp", "旧图静止，新图从下方滑入");

        public static readonly SlideTransition SlideDown = new SlideTransition(
            "SlideDown", "SlideDown", "旧图静止，新图从上方滑入");

        public Storyboard Build(FrameworkElement outgoing, FrameworkElement incoming, double duration)
        {
            switch (Id)
            {
                case "SlideLeft": return BuildSlideLeft(outgoing, incoming, duration);
                case "SlideRight": return BuildSlideRight(outgoing, incoming, duration);
                case "SlideUp": return BuildSlideUp(outgoing, incoming, duration);
                case "SlideDown": return BuildSlideDown(outgoing, incoming, duration);
                default: return BuildSlideLeft(outgoing, incoming, duration);
            }
        }

        private Storyboard BuildSlideLeft(FrameworkElement outgoing, FrameworkElement incoming, double duration)
        {
            var sb = new Storyboard();
            var time = TimeSpan.FromSeconds(duration);
            double w = SystemParameters.WorkArea.Width;

            incoming.RenderTransform = new TranslateTransform(w, 0);
            incoming.Opacity = 1.0;
            outgoing.Opacity = 1.0;
            Panel.SetZIndex(incoming, 1);

            var moveIn = new DoubleAnimation(w, 0, new Duration(time));
            Storyboard.SetTarget(moveIn, incoming);
            Storyboard.SetTargetProperty(moveIn, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));

            sb.Children.Add(moveIn);
            return sb;
        }

        private Storyboard BuildSlideRight(FrameworkElement outgoing, FrameworkElement incoming, double duration)
        {
            var sb = new Storyboard();
            var time = TimeSpan.FromSeconds(duration);
            double w = SystemParameters.WorkArea.Width;

            incoming.RenderTransform = new TranslateTransform(-w, 0);
            incoming.Opacity = 1.0;
            outgoing.Opacity = 1.0;
            Panel.SetZIndex(incoming, 1);

            var moveIn = new DoubleAnimation(-w, 0, new Duration(time));
            Storyboard.SetTarget(moveIn, incoming);
            Storyboard.SetTargetProperty(moveIn, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));

            sb.Children.Add(moveIn);
            return sb;
        }

        private Storyboard BuildSlideUp(FrameworkElement outgoing, FrameworkElement incoming, double duration)
        {
            var sb = new Storyboard();
            var time = TimeSpan.FromSeconds(duration);
            double h = SystemParameters.WorkArea.Height;

            incoming.RenderTransform = new TranslateTransform(0, h);
            incoming.Opacity = 1.0;
            outgoing.Opacity = 1.0;
            Panel.SetZIndex(incoming, 1);

            var moveIn = new DoubleAnimation(h, 0, new Duration(time));
            Storyboard.SetTarget(moveIn, incoming);
            Storyboard.SetTargetProperty(moveIn, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.Y)"));

            sb.Children.Add(moveIn);
            return sb;
        }

        private Storyboard BuildSlideDown(FrameworkElement outgoing, FrameworkElement incoming, double duration)
        {
            var sb = new Storyboard();
            var time = TimeSpan.FromSeconds(duration);
            double h = SystemParameters.WorkArea.Height;

            incoming.RenderTransform = new TranslateTransform(0, -h);
            incoming.Opacity = 1.0;
            outgoing.Opacity = 1.0;
            Panel.SetZIndex(incoming, 1);

            var moveIn = new DoubleAnimation(-h, 0, new Duration(time));
            Storyboard.SetTarget(moveIn, incoming);
            Storyboard.SetTargetProperty(moveIn, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.Y)"));

            sb.Children.Add(moveIn);
            return sb;
        }
    }
}
