using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace PicScreenSaver.Runtime.Engine.Transitions
{
    public class WipeTransition : ITransition
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }

        private WipeTransition(string id, string name, string description)
        {
            Id = id;
            Name = name;
            Description = description;
        }

        public static readonly WipeTransition WipeLeft = new WipeTransition(
            "WipeLeft", "WipeLeft", "遮罩从左向右展开，逐渐露出新图");

        public static readonly WipeTransition WipeRight = new WipeTransition(
            "WipeRight", "WipeRight", "遮罩从右向左展开，逐渐露出新图");

        public static readonly WipeTransition WipeUp = new WipeTransition(
            "WipeUp", "WipeUp", "遮罩从上向下展开，逐渐露出新图");

        public static readonly WipeTransition WipeDown = new WipeTransition(
            "WipeDown", "WipeDown", "遮罩从下向上展开，逐渐露出新图");

        public Storyboard Build(FrameworkElement outgoing, FrameworkElement incoming, double duration)
        {
            switch (Id)
            {
                case "WipeLeft": return BuildWipeLeft(outgoing, incoming, duration);
                case "WipeRight": return BuildWipeRight(outgoing, incoming, duration);
                case "WipeUp": return BuildWipeUp(outgoing, incoming, duration);
                case "WipeDown": return BuildWipeDown(outgoing, incoming, duration);
                default: return BuildWipeLeft(outgoing, incoming, duration);
            }
        }

        private Storyboard BuildWipeLeft(FrameworkElement outgoing, FrameworkElement incoming, double duration)
        {
            var sb = new Storyboard();
            var time = TimeSpan.FromSeconds(duration);
            double w = SystemParameters.WorkArea.Width;
            double h = SystemParameters.WorkArea.Height;

            incoming.Clip = new RectangleGeometry(new Rect(0, 0, 0, h));
            incoming.Opacity = 1.0;
            outgoing.Opacity = 1.0;
            Panel.SetZIndex(incoming, 1);

            var wipeAnim = new RectangleGeometryAnimation(
                new Rect(0, 0, 0, h),
                new Rect(0, 0, w, h),
                time);
            Storyboard.SetTarget(wipeAnim, incoming);
            Storyboard.SetTargetProperty(wipeAnim, new PropertyPath(UIElement.ClipProperty));

            sb.Children.Add(wipeAnim);
            return sb;
        }

        private Storyboard BuildWipeRight(FrameworkElement outgoing, FrameworkElement incoming, double duration)
        {
            var sb = new Storyboard();
            var time = TimeSpan.FromSeconds(duration);
            double w = SystemParameters.WorkArea.Width;
            double h = SystemParameters.WorkArea.Height;

            incoming.Clip = new RectangleGeometry(new Rect(w, 0, 0, h));
            incoming.Opacity = 1.0;
            outgoing.Opacity = 1.0;
            Panel.SetZIndex(incoming, 1);

            var wipeAnim = new RectangleGeometryAnimation(
                new Rect(w, 0, 0, h),
                new Rect(0, 0, w, h),
                time);
            Storyboard.SetTarget(wipeAnim, incoming);
            Storyboard.SetTargetProperty(wipeAnim, new PropertyPath(UIElement.ClipProperty));

            sb.Children.Add(wipeAnim);
            return sb;
        }

        private Storyboard BuildWipeUp(FrameworkElement outgoing, FrameworkElement incoming, double duration)
        {
            var sb = new Storyboard();
            var time = TimeSpan.FromSeconds(duration);
            double w = SystemParameters.WorkArea.Width;
            double h = SystemParameters.WorkArea.Height;

            incoming.Clip = new RectangleGeometry(new Rect(0, 0, w, 0));
            incoming.Opacity = 1.0;
            outgoing.Opacity = 1.0;
            Panel.SetZIndex(incoming, 1);

            var wipeAnim = new RectangleGeometryAnimation(
                new Rect(0, 0, w, 0),
                new Rect(0, 0, w, h),
                time);
            Storyboard.SetTarget(wipeAnim, incoming);
            Storyboard.SetTargetProperty(wipeAnim, new PropertyPath(UIElement.ClipProperty));

            sb.Children.Add(wipeAnim);
            return sb;
        }

        private Storyboard BuildWipeDown(FrameworkElement outgoing, FrameworkElement incoming, double duration)
        {
            var sb = new Storyboard();
            var time = TimeSpan.FromSeconds(duration);
            double w = SystemParameters.WorkArea.Width;
            double h = SystemParameters.WorkArea.Height;

            incoming.Clip = new RectangleGeometry(new Rect(0, h, w, 0));
            incoming.Opacity = 1.0;
            outgoing.Opacity = 1.0;
            Panel.SetZIndex(incoming, 1);

            var wipeAnim = new RectangleGeometryAnimation(
                new Rect(0, h, w, 0),
                new Rect(0, 0, w, h),
                time);
            Storyboard.SetTarget(wipeAnim, incoming);
            Storyboard.SetTargetProperty(wipeAnim, new PropertyPath(UIElement.ClipProperty));

            sb.Children.Add(wipeAnim);
            return sb;
        }
    }

    public class RectangleGeometryAnimation : AnimationTimeline
    {
        public override Type TargetPropertyType => typeof(RectangleGeometry);

        private Rect _from;
        private Rect _to;
        private TimeSpan _duration;

        public RectangleGeometryAnimation(Rect from, Rect to, TimeSpan duration)
        {
            _from = from;
            _to = to;
            _duration = duration;
        }

        protected override Freezable CreateInstanceCore()
        {
            return new RectangleGeometryAnimation(_from, _to, _duration);
        }

        public override object GetCurrentValue(object defaultOriginValue, object defaultDestinationValue, AnimationClock clock)
        {
            if (clock == null || clock.CurrentProgress == null)
                return new RectangleGeometry(_from);

            double t = clock.CurrentProgress.Value;
            double x = _from.X + (_to.X - _from.X) * t;
            double y = _from.Y + (_to.Y - _from.Y) * t;
            double width = _from.Width + (_to.Width - _from.Width) * t;
            double height = _from.Height + (_to.Height - _from.Height) * t;

            return new RectangleGeometry(new Rect(x, y, width, height));
        }
    }
}
