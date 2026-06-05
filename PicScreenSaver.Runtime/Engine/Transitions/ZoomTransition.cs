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

        public static readonly ZoomTransition ZoomInFade = new ZoomTransition(
            "ZoomInFade", "ZoomInFade", "放大同时淡入");

        public static readonly ZoomTransition ZoomOutFade = new ZoomTransition(
            "ZoomOutFade", "ZoomOutFade", "缩小同时淡出");

        public static readonly ZoomTransition ZoomIn = new ZoomTransition(
            "ZoomIn", "ZoomIn", "新图从 1.2x 缩小到 1.0x 切入");

        public static readonly ZoomTransition ZoomOut = new ZoomTransition(
            "ZoomOut", "ZoomOut", "旧图从 1.0x 放大到 1.2x 淡出");

        public static readonly ZoomTransition CrossZoom = new ZoomTransition(
            "CrossZoom", "CrossZoom", "旧图放大淡出的同时新图缩小淡入——电影感切换");

        public Storyboard Build(FrameworkElement outgoing, FrameworkElement incoming, double duration)
        {
            switch (Id)
            {
                case "ZoomInFade": return BuildZoomInFade(outgoing, incoming, duration);
                case "ZoomOutFade": return BuildZoomOutFade(outgoing, incoming, duration);
                case "ZoomIn": return BuildZoomIn(outgoing, incoming, duration);
                case "ZoomOut": return BuildZoomOut(outgoing, incoming, duration);
                case "CrossZoom": return BuildCrossZoom(outgoing, incoming, duration);
                default: return BuildZoomInFade(outgoing, incoming, duration);
            }
        }

        private Storyboard BuildZoomInFade(FrameworkElement outgoing, FrameworkElement incoming, double duration)
        {
            var sb = new Storyboard(); sb.Duration = new Duration(TimeSpan.FromSeconds(duration));
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
            var sb = new Storyboard(); sb.Duration = new Duration(TimeSpan.FromSeconds(duration));
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

        private Storyboard BuildZoomIn(FrameworkElement outgoing, FrameworkElement incoming, double duration)
        {
            var sb = new Storyboard(); sb.Duration = new Duration(TimeSpan.FromSeconds(duration));
            var time = TimeSpan.FromSeconds(duration);

            incoming.RenderTransform = new ScaleTransform(1.2, 1.2);
            incoming.RenderTransformOrigin = new Point(0.5, 0.5);
            incoming.Opacity = 0.0;
            outgoing.Opacity = 1.0;
            Panel.SetZIndex(incoming, 1);

            var scaleX = new DoubleAnimation(1.2, 1.0, new Duration(time));
            Storyboard.SetTarget(scaleX, incoming);
            Storyboard.SetTargetProperty(scaleX, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleX)"));

            var scaleY = new DoubleAnimation(1.2, 1.0, new Duration(time));
            Storyboard.SetTarget(scaleY, incoming);
            Storyboard.SetTargetProperty(scaleY, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleY)"));

            var fadeIn = new DoubleAnimation(0.0, 1.0, new Duration(time));
            Storyboard.SetTarget(fadeIn, incoming);
            Storyboard.SetTargetProperty(fadeIn, new PropertyPath(UIElement.OpacityProperty));

            var fadeOut = new DoubleAnimation(1.0, 0.0, new Duration(time));
            Storyboard.SetTarget(fadeOut, outgoing);
            Storyboard.SetTargetProperty(fadeOut, new PropertyPath(UIElement.OpacityProperty));

            sb.Children.Add(scaleX);
            sb.Children.Add(scaleY);
            sb.Children.Add(fadeIn);
            sb.Children.Add(fadeOut);
            return sb;
        }

        private Storyboard BuildZoomOut(FrameworkElement outgoing, FrameworkElement incoming, double duration)
        {
            var sb = new Storyboard(); sb.Duration = new Duration(TimeSpan.FromSeconds(duration));
            var time = TimeSpan.FromSeconds(duration);

            outgoing.RenderTransform = new ScaleTransform(1.0, 1.0);
            outgoing.RenderTransformOrigin = new Point(0.5, 0.5);
            outgoing.Opacity = 1.0;
            incoming.Opacity = 0.0;
            Panel.SetZIndex(outgoing, 1);

            var scaleX = new DoubleAnimation(1.0, 1.2, new Duration(time));
            Storyboard.SetTarget(scaleX, outgoing);
            Storyboard.SetTargetProperty(scaleX, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleX)"));

            var scaleY = new DoubleAnimation(1.0, 1.2, new Duration(time));
            Storyboard.SetTarget(scaleY, outgoing);
            Storyboard.SetTargetProperty(scaleY, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleY)"));

            var fadeOut = new DoubleAnimation(1.0, 0.0, new Duration(time));
            Storyboard.SetTarget(fadeOut, outgoing);
            Storyboard.SetTargetProperty(fadeOut, new PropertyPath(UIElement.OpacityProperty));

            var fadeIn = new DoubleAnimation(0.0, 1.0, new Duration(time));
            Storyboard.SetTarget(fadeIn, incoming);
            Storyboard.SetTargetProperty(fadeIn, new PropertyPath(UIElement.OpacityProperty));

            sb.Children.Add(scaleX);
            sb.Children.Add(scaleY);
            sb.Children.Add(fadeOut);
            sb.Children.Add(fadeIn);
            return sb;
        }

        private Storyboard BuildCrossZoom(FrameworkElement outgoing, FrameworkElement incoming, double duration)
        {
            var sb = new Storyboard(); sb.Duration = new Duration(TimeSpan.FromSeconds(duration));
            var time = TimeSpan.FromSeconds(duration);

            outgoing.RenderTransformOrigin = new Point(0.5, 0.5);
            outgoing.RenderTransform = new ScaleTransform(1.0, 1.0);
            incoming.RenderTransformOrigin = new Point(0.5, 0.5);
            incoming.RenderTransform = new ScaleTransform(1.3, 1.3);
            outgoing.Opacity = 1.0;
            incoming.Opacity = 0.0;
            Panel.SetZIndex(incoming, 1);

            var outScaleX = new DoubleAnimation(1.0, 1.3, new Duration(time));
            Storyboard.SetTarget(outScaleX, outgoing);
            Storyboard.SetTargetProperty(outScaleX, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleX)"));

            var outScaleY = new DoubleAnimation(1.0, 1.3, new Duration(time));
            Storyboard.SetTarget(outScaleY, outgoing);
            Storyboard.SetTargetProperty(outScaleY, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleY)"));

            var inScaleX = new DoubleAnimation(1.3, 1.0, new Duration(time));
            Storyboard.SetTarget(inScaleX, incoming);
            Storyboard.SetTargetProperty(inScaleX, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleX)"));

            var inScaleY = new DoubleAnimation(1.3, 1.0, new Duration(time));
            Storyboard.SetTarget(inScaleY, incoming);
            Storyboard.SetTargetProperty(inScaleY, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleY)"));

            var fadeOut = new DoubleAnimation(1.0, 0.0, new Duration(time));
            Storyboard.SetTarget(fadeOut, outgoing);
            Storyboard.SetTargetProperty(fadeOut, new PropertyPath(UIElement.OpacityProperty));

            var fadeIn = new DoubleAnimation(0.0, 1.0, new Duration(time));
            Storyboard.SetTarget(fadeIn, incoming);
            Storyboard.SetTargetProperty(fadeIn, new PropertyPath(UIElement.OpacityProperty));

            sb.Children.Add(outScaleX);
            sb.Children.Add(outScaleY);
            sb.Children.Add(inScaleX);
            sb.Children.Add(inScaleY);
            sb.Children.Add(fadeOut);
            sb.Children.Add(fadeIn);
            return sb;
        }
    }
}
