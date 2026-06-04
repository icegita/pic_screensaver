using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace PicScreenSaver.Runtime.Engine.Transitions
{
    public class ZoomTransition : ITransition
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }

        private ZoomTransition(string id, string name, string description)
        {
            Id = id;
            Name = name;
            Description = description;
        }

        public static readonly ZoomTransition ZoomIn = new ZoomTransition(
            "ZoomIn", "ZoomIn", "画面从 95% 缓慢放大至 100%");

        public static readonly ZoomTransition ZoomOut = new ZoomTransition(
            "ZoomOut", "ZoomOut", "画面从 105% 缓慢缩小至 100%");

        public static readonly ZoomTransition ZoomInFade = new ZoomTransition(
            "ZoomInFade", "ZoomInFade", "放大同时淡入");

        public static readonly ZoomTransition ZoomOutFade = new ZoomTransition(
            "ZoomOutFade", "ZoomOutFade", "缩小同时淡出");

        public Storyboard Build(FrameworkElement outgoing, FrameworkElement incoming, double duration)
        {
            switch (Id)
            {
                case "ZoomIn": return BuildZoomIn(outgoing, incoming, duration);
                case "ZoomOut": return BuildZoomOut(outgoing, incoming, duration);
                case "ZoomInFade": return BuildZoomInFade(outgoing, incoming, duration);
                case "ZoomOutFade": return BuildZoomOutFade(outgoing, incoming, duration);
                default: return BuildZoomIn(outgoing, incoming, duration);
            }
        }

        private Storyboard BuildZoomIn(FrameworkElement outgoing, FrameworkElement incoming, double duration)
        {
            var sb = new Storyboard();
            var time = TimeSpan.FromSeconds(duration);

            outgoing.RenderTransform = new ScaleTransform(0.95, 0.95);
            outgoing.RenderTransformOrigin = new Point(0.5, 0.5);
            outgoing.Opacity = 1.0;
            incoming.Opacity = 0.0;
            Panel.SetZIndex(outgoing, 1);

            var scaleOut = new DoubleAnimation(0.95, 1.0, new Duration(time));
            Storyboard.SetTarget(scaleOut, outgoing);
            Storyboard.SetTargetProperty(scaleOut, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleX)"));

            var scaleOutY = new DoubleAnimation(0.95, 1.0, new Duration(time));
            Storyboard.SetTarget(scaleOutY, outgoing);
            Storyboard.SetTargetProperty(scaleOutY, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleY)"));

            sb.Children.Add(scaleOut);
            sb.Children.Add(scaleOutY);
            return sb;
        }

        private Storyboard BuildZoomOut(FrameworkElement outgoing, FrameworkElement incoming, double duration)
        {
            var sb = new Storyboard();
            var time = TimeSpan.FromSeconds(duration);

            outgoing.RenderTransform = new ScaleTransform(1.05, 1.05);
            outgoing.RenderTransformOrigin = new Point(0.5, 0.5);
            outgoing.Opacity = 1.0;
            incoming.Opacity = 0.0;
            Panel.SetZIndex(outgoing, 1);

            var scaleOut = new DoubleAnimation(1.05, 1.0, new Duration(time));
            Storyboard.SetTarget(scaleOut, outgoing);
            Storyboard.SetTargetProperty(scaleOut, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleX)"));

            var scaleOutY = new DoubleAnimation(1.05, 1.0, new Duration(time));
            Storyboard.SetTarget(scaleOutY, outgoing);
            Storyboard.SetTargetProperty(scaleOutY, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleY)"));

            sb.Children.Add(scaleOut);
            sb.Children.Add(scaleOutY);
            return sb;
        }

        private Storyboard BuildZoomInFade(FrameworkElement outgoing, FrameworkElement incoming, double duration)
        {
            var sb = new Storyboard();
            var time = TimeSpan.FromSeconds(duration);

            incoming.RenderTransform = new ScaleTransform(0.95, 0.95);
            incoming.RenderTransformOrigin = new Point(0.5, 0.5);
            outgoing.Opacity = 1.0;
            incoming.Opacity = 0.0;
            Panel.SetZIndex(incoming, 1);

            var scaleIn = new DoubleAnimation(0.95, 1.0, new Duration(time));
            Storyboard.SetTarget(scaleIn, incoming);
            Storyboard.SetTargetProperty(scaleIn, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleX)"));

            var scaleInY = new DoubleAnimation(0.95, 1.0, new Duration(time));
            Storyboard.SetTarget(scaleInY, incoming);
            Storyboard.SetTargetProperty(scaleInY, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleY)"));

            var fadeIn = new DoubleAnimation(0.0, 1.0, new Duration(time));
            Storyboard.SetTarget(fadeIn, incoming);
            Storyboard.SetTargetProperty(fadeIn, new PropertyPath(UIElement.OpacityProperty));

            var fadeOut = new DoubleAnimation(1.0, 0.0, new Duration(time));
            Storyboard.SetTarget(fadeOut, outgoing);
            Storyboard.SetTargetProperty(fadeOut, new PropertyPath(UIElement.OpacityProperty));

            sb.Children.Add(scaleIn);
            sb.Children.Add(scaleInY);
            sb.Children.Add(fadeIn);
            sb.Children.Add(fadeOut);
            return sb;
        }

        private Storyboard BuildZoomOutFade(FrameworkElement outgoing, FrameworkElement incoming, double duration)
        {
            var sb = new Storyboard();
            var time = TimeSpan.FromSeconds(duration);

            outgoing.RenderTransform = new ScaleTransform(1.05, 1.05);
            outgoing.RenderTransformOrigin = new Point(0.5, 0.5);
            outgoing.Opacity = 1.0;
            incoming.Opacity = 0.0;
            Panel.SetZIndex(outgoing, 1);

            var scaleOut = new DoubleAnimation(1.05, 1.0, new Duration(time));
            Storyboard.SetTarget(scaleOut, outgoing);
            Storyboard.SetTargetProperty(scaleOut, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleX)"));

            var scaleOutY = new DoubleAnimation(1.05, 1.0, new Duration(time));
            Storyboard.SetTarget(scaleOutY, outgoing);
            Storyboard.SetTargetProperty(scaleOutY, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleY)"));

            var fadeOut = new DoubleAnimation(1.0, 0.0, new Duration(time));
            Storyboard.SetTarget(fadeOut, outgoing);
            Storyboard.SetTargetProperty(fadeOut, new PropertyPath(UIElement.OpacityProperty));

            var fadeIn = new DoubleAnimation(0.0, 1.0, new Duration(time));
            Storyboard.SetTarget(fadeIn, incoming);
            Storyboard.SetTargetProperty(fadeIn, new PropertyPath(UIElement.OpacityProperty));

            sb.Children.Add(scaleOut);
            sb.Children.Add(scaleOutY);
            sb.Children.Add(fadeOut);
            sb.Children.Add(fadeIn);
            return sb;
        }
    }
}
