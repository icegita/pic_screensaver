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

        public static readonly SpecialTransition PushLeft = new SpecialTransition(
            "PushLeft", "PushLeft", "新旧图同步向左平移（翻页感）");

        public static readonly SpecialTransition PushUp = new SpecialTransition(
            "PushUp", "PushUp", "新旧图同步向上平移（翻页感）");

        public static readonly SpecialTransition PushRight = new SpecialTransition(
            "PushRight", "PushRight", "新旧图同步向右平移（翻页感）");

        public static readonly SpecialTransition PushDown = new SpecialTransition(
            "PushDown", "PushDown", "新旧图同步向下平移（翻页感）");

        public Storyboard Build(FrameworkElement outgoing, FrameworkElement incoming, double duration)
        {
            switch (Id)
            {
                case "PushLeft": return BuildPushLeft(outgoing, incoming, duration);
                case "PushUp": return BuildPushUp(outgoing, incoming, duration);
                case "PushRight": return BuildPushRight(outgoing, incoming, duration);
                case "PushDown": return BuildPushDown(outgoing, incoming, duration);
                default: return BuildPushLeft(outgoing, incoming, duration);
            }
        }

        private Storyboard BuildPushLeft(FrameworkElement outgoing, FrameworkElement incoming, double duration)
        {
            var sb = new Storyboard(); sb.Duration = new Duration(TimeSpan.FromSeconds(duration));
            var time = TimeSpan.FromSeconds(duration);
            double w = outgoing.ActualWidth;

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
            var sb = new Storyboard(); sb.Duration = new Duration(TimeSpan.FromSeconds(duration));
            var time = TimeSpan.FromSeconds(duration);
            double h = outgoing.ActualHeight;

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

        private Storyboard BuildPushRight(FrameworkElement outgoing, FrameworkElement incoming, double duration)
        {
            var sb = new Storyboard(); sb.Duration = new Duration(TimeSpan.FromSeconds(duration));
            var time = TimeSpan.FromSeconds(duration);
            double w = outgoing.ActualWidth;

            outgoing.RenderTransform = new TranslateTransform(0, 0);
            incoming.RenderTransform = new TranslateTransform(-w, 0);
            outgoing.Opacity = 1.0;
            incoming.Opacity = 1.0;
            Panel.SetZIndex(incoming, 1);

            var moveOut = new DoubleAnimation(0, w, new Duration(time));
            Storyboard.SetTarget(moveOut, outgoing);
            Storyboard.SetTargetProperty(moveOut, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));

            var moveIn = new DoubleAnimation(-w, 0, new Duration(time));
            Storyboard.SetTarget(moveIn, incoming);
            Storyboard.SetTargetProperty(moveIn, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));

            sb.Children.Add(moveOut);
            sb.Children.Add(moveIn);
            return sb;
        }

        private Storyboard BuildPushDown(FrameworkElement outgoing, FrameworkElement incoming, double duration)
        {
            var sb = new Storyboard(); sb.Duration = new Duration(TimeSpan.FromSeconds(duration));
            var time = TimeSpan.FromSeconds(duration);
            double h = outgoing.ActualHeight;

            outgoing.RenderTransform = new TranslateTransform(0, 0);
            incoming.RenderTransform = new TranslateTransform(0, -h);
            outgoing.Opacity = 1.0;
            incoming.Opacity = 1.0;
            Panel.SetZIndex(incoming, 1);

            var moveOut = new DoubleAnimation(0, h, new Duration(time));
            Storyboard.SetTarget(moveOut, outgoing);
            Storyboard.SetTargetProperty(moveOut, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.Y)"));

            var moveIn = new DoubleAnimation(-h, 0, new Duration(time));
            Storyboard.SetTarget(moveIn, incoming);
            Storyboard.SetTargetProperty(moveIn, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.Y)"));

            sb.Children.Add(moveOut);
            sb.Children.Add(moveIn);
            return sb;
        }
    }
}
