using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace PicScreenSaver.Runtime.Engine.Transitions
{
    public class RotateTransition : ITransition
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }

        private RotateTransition(string id, string name, string description)
        {
            Id = id;
            Name = name;
            Description = description;
        }

        public static readonly RotateTransition RotateCW = new RotateTransition(
            "RotateCW", "RotateCW", "新图顺时针旋转切入");

        public static readonly RotateTransition RotateCCW = new RotateTransition(
            "RotateCCW", "RotateCCW", "新图逆时针旋转切入");

        public Storyboard Build(FrameworkElement outgoing, FrameworkElement incoming, double duration)
        {
            switch (Id)
            {
                case "RotateCW": return BuildRotateCW(outgoing, incoming, duration);
                case "RotateCCW": return BuildRotateCCW(outgoing, incoming, duration);
                default: return BuildRotateCW(outgoing, incoming, duration);
            }
        }

        private Storyboard BuildRotateCW(FrameworkElement outgoing, FrameworkElement incoming, double duration)
        {
            var sb = new Storyboard(); sb.Duration = new Duration(TimeSpan.FromSeconds(duration));
            var time = TimeSpan.FromSeconds(duration);

            incoming.RenderTransformOrigin = new Point(0.5, 0.5);
            incoming.RenderTransform = new RotateTransform(-15);
            incoming.Opacity = 0.0;
            outgoing.Opacity = 1.0;
            Panel.SetZIndex(incoming, 1);

            var rotate = new DoubleAnimation(-15, 0, new Duration(time));
            Storyboard.SetTarget(rotate, incoming);
            Storyboard.SetTargetProperty(rotate, new PropertyPath("(UIElement.RenderTransform).(RotateTransform.Angle)"));

            var fadeIn = new DoubleAnimation(0.0, 1.0, new Duration(time));
            Storyboard.SetTarget(fadeIn, incoming);
            Storyboard.SetTargetProperty(fadeIn, new PropertyPath(UIElement.OpacityProperty));

            var fadeOut = new DoubleAnimation(1.0, 0.0, new Duration(time));
            Storyboard.SetTarget(fadeOut, outgoing);
            Storyboard.SetTargetProperty(fadeOut, new PropertyPath(UIElement.OpacityProperty));

            sb.Children.Add(rotate);
            sb.Children.Add(fadeIn);
            sb.Children.Add(fadeOut);
            return sb;
        }

        private Storyboard BuildRotateCCW(FrameworkElement outgoing, FrameworkElement incoming, double duration)
        {
            var sb = new Storyboard(); sb.Duration = new Duration(TimeSpan.FromSeconds(duration));
            var time = TimeSpan.FromSeconds(duration);

            incoming.RenderTransformOrigin = new Point(0.5, 0.5);
            incoming.RenderTransform = new RotateTransform(15);
            incoming.Opacity = 0.0;
            outgoing.Opacity = 1.0;
            Panel.SetZIndex(incoming, 1);

            var rotate = new DoubleAnimation(15, 0, new Duration(time));
            Storyboard.SetTarget(rotate, incoming);
            Storyboard.SetTargetProperty(rotate, new PropertyPath("(UIElement.RenderTransform).(RotateTransform.Angle)"));

            var fadeIn = new DoubleAnimation(0.0, 1.0, new Duration(time));
            Storyboard.SetTarget(fadeIn, incoming);
            Storyboard.SetTargetProperty(fadeIn, new PropertyPath(UIElement.OpacityProperty));

            var fadeOut = new DoubleAnimation(1.0, 0.0, new Duration(time));
            Storyboard.SetTarget(fadeOut, outgoing);
            Storyboard.SetTargetProperty(fadeOut, new PropertyPath(UIElement.OpacityProperty));

            sb.Children.Add(rotate);
            sb.Children.Add(fadeIn);
            sb.Children.Add(fadeOut);
            return sb;
        }
    }
}
