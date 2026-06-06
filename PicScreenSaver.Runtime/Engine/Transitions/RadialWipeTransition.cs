using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace PicScreenSaver.Runtime.Engine.Transitions
{
    public class RadialWipeTransition : ITransition
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }

        private RadialWipeTransition(string id, string name, string description)
        {
            Id = id;
            Name = name;
            Description = description;
        }

        public static readonly RadialWipeTransition RadialWipe = new RadialWipeTransition(
            "RadialWipe", "RadialWipe", "时钟式扇形扫过展开新图");

        public Storyboard Build(FrameworkElement outgoing, FrameworkElement incoming, double duration)
        {
            var sb = new Storyboard(); sb.Duration = new Duration(TimeSpan.FromSeconds(duration));
            var time = TimeSpan.FromSeconds(duration);
            double w = outgoing.ActualWidth;
            double h = outgoing.ActualHeight;
            double cx = w / 2.0, cy = h / 2.0;
            double r = Math.Sqrt(w * w + h * h);

            incoming.Clip = new RectangleGeometry(new Rect(0, 0, 0, 0));
            incoming.Opacity = 1.0;
            outgoing.Opacity = 1.0;
            Panel.SetZIndex(incoming, 1);

            var anim = new RadialSectorAnimation(cx, cy, r, time);
            Storyboard.SetTarget(anim, incoming);
            Storyboard.SetTargetProperty(anim, new PropertyPath(UIElement.ClipProperty));
            sb.Children.Add(anim);

            sb.Completed += (s, e) => { incoming.Clip = null; };
            return sb;
        }
    }

    public class RadialSectorAnimation : AnimationTimeline
    {
        public override Type TargetPropertyType => typeof(Geometry);

        private double _cx, _cy, _r;
        private TimeSpan _duration;

        public RadialSectorAnimation(double cx, double cy, double r, TimeSpan duration)
        {
            _cx = cx; _cy = cy; _r = r; _duration = duration;
        }

        protected override Freezable CreateInstanceCore()
        {
            return new RadialSectorAnimation(_cx, _cy, _r, _duration);
        }

        public override object GetCurrentValue(object defaultOriginValue, object defaultDestinationValue, AnimationClock clock)
        {
            if (clock == null || clock.CurrentProgress == null) return new RectangleGeometry(new Rect(0, 0, 0, 0));
            double t = clock.CurrentProgress.Value;
            if (t <= 0) return new RectangleGeometry(new Rect(0, 0, 0, 0));
            if (t >= 1.0) return new RectangleGeometry(new Rect(0, 0, _cx * 2, _cy * 2));

            double angle = t * 2.0 * Math.PI; // full sweep in radians
            double ex = _cx + _r * Math.Cos(angle);
            double ey = _cy + _r * Math.Sin(angle);

            bool isLarge = angle > Math.PI;

            var startPt = new Point(_cx + _r, _cy); // 0° (3 o'clock)
            var arcPt = new Point(ex, ey);
            var segs = new PathSegmentCollection();
            segs.Add(new ArcSegment(arcPt, new Size(_r, _r), 0, isLarge, SweepDirection.Clockwise, true));
            segs.Add(new LineSegment(new Point(_cx, _cy), true));

            var fig = new PathFigure(startPt, segs, true);
            return new PathGeometry(new[] { fig });
        }
    }
}
