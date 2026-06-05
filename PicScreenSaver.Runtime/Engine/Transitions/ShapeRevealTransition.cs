using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace PicScreenSaver.Runtime.Engine.Transitions
{
    public class ShapeRevealTransition : ITransition
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }

        private ShapeRevealTransition(string id, string name, string description)
        {
            Id = id;
            Name = name;
            Description = description;
        }

        public static readonly ShapeRevealTransition CircleReveal = new ShapeRevealTransition(
            "CircleReveal", "CircleReveal", "圆形遮罩从中心扩大展开");

        public static readonly ShapeRevealTransition DiamondReveal = new ShapeRevealTransition(
            "DiamondReveal", "DiamondReveal", "菱形遮罩从中心扩大展开");

        public Storyboard Build(FrameworkElement outgoing, FrameworkElement incoming, double duration)
        {
            switch (Id)
            {
                case "CircleReveal": return BuildCircleReveal(outgoing, incoming, duration);
                case "DiamondReveal": return BuildDiamondReveal(outgoing, incoming, duration);
                default: return BuildCircleReveal(outgoing, incoming, duration);
            }
        }

        private Storyboard BuildCircleReveal(FrameworkElement outgoing, FrameworkElement incoming, double duration)
        {
            var sb = new Storyboard(); sb.Duration = new Duration(TimeSpan.FromSeconds(duration));
            var time = TimeSpan.FromSeconds(duration);
            double w = outgoing.ActualWidth;
            double h = outgoing.ActualHeight;
            double maxR = Math.Sqrt(w * w + h * h) / 2.0;
            var center = new Point(w / 2.0, h / 2.0);

            incoming.Clip = new EllipseGeometry(center, 0, 0);
            incoming.Opacity = 1.0;
            outgoing.Opacity = 1.0;
            Panel.SetZIndex(incoming, 1);

            var anim = new EllipseRadiusAnimation(center, 0, 0, maxR, maxR, time);
            Storyboard.SetTarget(anim, incoming);
            Storyboard.SetTargetProperty(anim, new PropertyPath(UIElement.ClipProperty));
            sb.Children.Add(anim);

            sb.Completed += (s, e) => { incoming.Clip = null; };
            return sb;
        }

        private Storyboard BuildDiamondReveal(FrameworkElement outgoing, FrameworkElement incoming, double duration)
        {
            var sb = new Storyboard(); sb.Duration = new Duration(TimeSpan.FromSeconds(duration));
            var time = TimeSpan.FromSeconds(duration);
            double w = outgoing.ActualWidth;
            double h = outgoing.ActualHeight;

            incoming.Clip = new RectangleGeometry(new Rect(w / 2, h / 2, 0, 0));
            incoming.Opacity = 1.0;
            outgoing.Opacity = 1.0;
            Panel.SetZIndex(incoming, 1);

            var anim = new DiamondRevealAnimation(w, h, time);
            Storyboard.SetTarget(anim, incoming);
            Storyboard.SetTargetProperty(anim, new PropertyPath(UIElement.ClipProperty));
            sb.Children.Add(anim);

            sb.Completed += (s, e) => { incoming.Clip = null; };
            return sb;
        }
    }

    public class EllipseRadiusAnimation : AnimationTimeline
    {
        public override Type TargetPropertyType => typeof(Geometry);

        private Point _center;
        private double _fromRX, _fromRY, _toRX, _toRY;
        private TimeSpan _duration;

        public EllipseRadiusAnimation(Point center, double fromRX, double fromRY, double toRX, double toRY, TimeSpan duration)
        {
            _center = center; _fromRX = fromRX; _fromRY = fromRY;
            _toRX = toRX; _toRY = toRY; _duration = duration;
        }

        protected override Freezable CreateInstanceCore()
        {
            return new EllipseRadiusAnimation(_center, _fromRX, _fromRY, _toRX, _toRY, _duration);
        }

        public override object GetCurrentValue(object defaultOriginValue, object defaultDestinationValue, AnimationClock clock)
        {
            if (clock == null || clock.CurrentProgress == null)
                return new EllipseGeometry(_center, _fromRX, _fromRY);
            double t = clock.CurrentProgress.Value;
            return new EllipseGeometry(_center,
                _fromRX + (_toRX - _fromRX) * t,
                _fromRY + (_toRY - _fromRY) * t);
        }
    }

    public class DiamondRevealAnimation : AnimationTimeline
    {
        public override Type TargetPropertyType => typeof(Geometry);

        private double _w, _h;
        private TimeSpan _duration;

        public DiamondRevealAnimation(double w, double h, TimeSpan duration)
        {
            _w = w; _h = h; _duration = duration;
        }

        protected override Freezable CreateInstanceCore()
        {
            return new DiamondRevealAnimation(_w, _h, _duration);
        }

        public override object GetCurrentValue(object defaultOriginValue, object defaultDestinationValue, AnimationClock clock)
        {
            if (clock == null || clock.CurrentProgress == null) return new RectangleGeometry(new Rect(0, 0, 0, 0));
            double t = clock.CurrentProgress.Value;
            double cx = _w / 2.0, cy = _h / 2.0;
            double size = Math.Max(_w, _h) * t;
            var pts = new PointCollection(new[]
            {
                new Point(cx, cy - size),
                new Point(cx + size, cy),
                new Point(cx, cy + size),
                new Point(cx - size, cy),
            });
            var seg = new PathSegmentCollection { new PolyLineSegment(pts, true) };
            var fig = new PathFigure(pts[0], seg, true);
            return new PathGeometry(new[] { fig });
        }
    }
}
