using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace PicScreenSaver.Runtime.Engine.Transitions
{
    public class SpecialTransition : ITransition
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }

        private SpecialTransition(string id, string name, string description)
        {
            Id = id;
            Name = name;
            Description = description;
        }

        public static readonly SpecialTransition FlipHorizontal = new SpecialTransition(
            "FlipHorizontal", "FlipHorizontal", "以 Y 轴为中心水平翻转切换");

        public static readonly SpecialTransition FlipVertical = new SpecialTransition(
            "FlipVertical", "FlipVertical", "以 X 轴为中心垂直翻转切换");

        public static readonly SpecialTransition PushLeft = new SpecialTransition(
            "PushLeft", "PushLeft", "新旧图同步向左平移（翻页感）");

        public static readonly SpecialTransition PushUp = new SpecialTransition(
            "PushUp", "PushUp", "新旧图同步向上平移（翻页感）");

        public Storyboard Build(FrameworkElement outgoing, FrameworkElement incoming, double duration)
        {
            switch (Id)
            {
                case "FlipHorizontal": return BuildFlipHorizontal(outgoing, incoming, duration);
                case "FlipVertical": return BuildFlipVertical(outgoing, incoming, duration);
                case "PushLeft": return BuildPushLeft(outgoing, incoming, duration);
                case "PushUp": return BuildPushUp(outgoing, incoming, duration);
                default: return BuildPushLeft(outgoing, incoming, duration);
            }
        }

        private Storyboard BuildFlipHorizontal(FrameworkElement outgoing, FrameworkElement incoming, double duration)
        {
            var sb = new Storyboard();
            var halfTime = TimeSpan.FromSeconds(duration / 2.0);

            outgoing.RenderTransformOrigin = new Point(0.5, 0.5);
            outgoing.RenderTransform = new ScaleTransform(1, 1);
            incoming.RenderTransformOrigin = new Point(0.5, 0.5);
            incoming.RenderTransform = new ScaleTransform(1, 1);
            incoming.Opacity = 0.0;
            outgoing.Opacity = 1.0;
            Panel.SetZIndex(outgoing, 2);
            Panel.SetZIndex(incoming, 1);

            var flipOut = new DoubleAnimation(1, 0, new Duration(halfTime));
            Storyboard.SetTarget(flipOut, outgoing);
            Storyboard.SetTargetProperty(flipOut, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleX)"));

            var flipIn = new DoubleAnimation(0, 1, new Duration(halfTime));
            Storyboard.SetTarget(flipIn, incoming);
            Storyboard.SetTargetProperty(flipIn, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleX)"));
            flipIn.BeginTime = halfTime;

            var showIncoming = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(1)));
            Storyboard.SetTarget(showIncoming, incoming);
            Storyboard.SetTargetProperty(showIncoming, new PropertyPath(UIElement.OpacityProperty));
            showIncoming.BeginTime = halfTime;

            sb.Children.Add(flipOut);
            sb.Children.Add(flipIn);
            sb.Children.Add(showIncoming);
            return sb;
        }

        private Storyboard BuildFlipVertical(FrameworkElement outgoing, FrameworkElement incoming, double duration)
        {
            var sb = new Storyboard();
            var halfTime = TimeSpan.FromSeconds(duration / 2.0);

            outgoing.RenderTransformOrigin = new Point(0.5, 0.5);
            outgoing.RenderTransform = new ScaleTransform(1, 1);
            incoming.RenderTransformOrigin = new Point(0.5, 0.5);
            incoming.RenderTransform = new ScaleTransform(1, 1);
            incoming.Opacity = 0.0;
            outgoing.Opacity = 1.0;
            Panel.SetZIndex(outgoing, 2);
            Panel.SetZIndex(incoming, 1);

            var flipOut = new DoubleAnimation(1, 0, new Duration(halfTime));
            Storyboard.SetTarget(flipOut, outgoing);
            Storyboard.SetTargetProperty(flipOut, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleY)"));

            var flipIn = new DoubleAnimation(0, 1, new Duration(halfTime));
            Storyboard.SetTarget(flipIn, incoming);
            Storyboard.SetTargetProperty(flipIn, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleY)"));
            flipIn.BeginTime = halfTime;

            var showIncoming = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(1)));
            Storyboard.SetTarget(showIncoming, incoming);
            Storyboard.SetTargetProperty(showIncoming, new PropertyPath(UIElement.OpacityProperty));
            showIncoming.BeginTime = halfTime;

            sb.Children.Add(flipOut);
            sb.Children.Add(flipIn);
            sb.Children.Add(showIncoming);
            return sb;
        }

        private Storyboard BuildPushLeft(FrameworkElement outgoing, FrameworkElement incoming, double duration)
        {
            var sb = new Storyboard();
            var time = TimeSpan.FromSeconds(duration);
            double w = SystemParameters.WorkArea.Width;

            outgoing.RenderTransform = new TranslateTransform(0, 0);
            incoming.RenderTransform = new TranslateTransform(w, 0);
            outgoing.Opacity = 1.0;
            incoming.Opacity = 1.0;
            Panel.SetZIndex(incoming, 1);

            var moveOut = new DoubleAnimation(0, -w, new Duration(time));
            Storyboard.SetTarget(moveOut, outgoing);
            Storyboard.SetTargetProperty(moveOut, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));

            var moveIn = new DoubleAnimation(w, 0, new Duration(time));
            Storyboard.SetTarget(moveIn, incoming);
            Storyboard.SetTargetProperty(moveIn, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));

            sb.Children.Add(moveOut);
            sb.Children.Add(moveIn);
            return sb;
        }

        private Storyboard BuildPushUp(FrameworkElement outgoing, FrameworkElement incoming, double duration)
        {
            var sb = new Storyboard();
            var time = TimeSpan.FromSeconds(duration);
            double h = SystemParameters.WorkArea.Height;

            outgoing.RenderTransform = new TranslateTransform(0, 0);
            incoming.RenderTransform = new TranslateTransform(0, h);
            outgoing.Opacity = 1.0;
            incoming.Opacity = 1.0;
            Panel.SetZIndex(incoming, 1);

            var moveOut = new DoubleAnimation(0, -h, new Duration(time));
            Storyboard.SetTarget(moveOut, outgoing);
            Storyboard.SetTargetProperty(moveOut, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.Y)"));

            var moveIn = new DoubleAnimation(h, 0, new Duration(time));
            Storyboard.SetTarget(moveIn, incoming);
            Storyboard.SetTargetProperty(moveIn, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.Y)"));

            sb.Children.Add(moveOut);
            sb.Children.Add(moveIn);
            return sb;
        }
    }
}
