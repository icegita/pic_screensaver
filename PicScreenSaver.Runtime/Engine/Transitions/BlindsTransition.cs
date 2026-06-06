using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace PicScreenSaver.Runtime.Engine.Transitions
{
    public class BlindsTransition : ITransition
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }

        private BlindsTransition(string id, string name, string description)
        {
            Id = id;
            Name = name;
            Description = description;
        }

        public static readonly BlindsTransition BlindsH = new BlindsTransition(
            "BlindsH", "BlindsH", "水平百叶窗式揭开");

        public static readonly BlindsTransition BlindsV = new BlindsTransition(
            "BlindsV", "BlindsV", "垂直百叶窗式揭开");

        public Storyboard Build(FrameworkElement outgoing, FrameworkElement incoming, double duration)
        {
            switch (Id)
            {
                case "BlindsH": return BuildBlinds(outgoing, incoming, duration, true);
                case "BlindsV": return BuildBlinds(outgoing, incoming, duration, false);
                default: return BuildBlinds(outgoing, incoming, duration, true);
            }
        }

        private Storyboard BuildBlinds(FrameworkElement outgoing, FrameworkElement incoming, double duration, bool horizontal)
        {
            var sb = new Storyboard(); sb.Duration = new Duration(TimeSpan.FromSeconds(duration));
            var time = TimeSpan.FromSeconds(duration);
            double w = outgoing.ActualWidth;
            double h = outgoing.ActualHeight;
            int count = 10;

            incoming.Opacity = 1.0;
            outgoing.Opacity = 1.0;
            Panel.SetZIndex(incoming, 1);

            var anim = new BlindsGeometryAnimation(w, h, count, horizontal, time);
            Storyboard.SetTarget(anim, incoming);
            Storyboard.SetTargetProperty(anim, new PropertyPath(UIElement.ClipProperty));
            sb.Children.Add(anim);

            sb.Completed += (s, e) => { incoming.Clip = null; };
            return sb;
        }
    }

    public class BlindsGeometryAnimation : AnimationTimeline
    {
        public override Type TargetPropertyType => typeof(Geometry);

        private double _w, _h;
        private int _count;
        private bool _horizontal;
        private TimeSpan _duration;

        public BlindsGeometryAnimation(double w, double h, int count, bool horizontal, TimeSpan duration)
        {
            _w = w; _h = h; _count = count; _horizontal = horizontal; _duration = duration;
        }

        protected override Freezable CreateInstanceCore()
        {
            return new BlindsGeometryAnimation(_w, _h, _count, _horizontal, _duration);
        }

        public override object GetCurrentValue(object defaultOriginValue, object defaultDestinationValue, AnimationClock clock)
        {
            if (clock == null || clock.CurrentProgress == null)
                return new RectangleGeometry(new Rect(0, 0, 0, 0));

            double t = clock.CurrentProgress.Value;
            var group = new GeometryGroup();

            if (_horizontal)
            {
                double stripH = _h / _count;
                for (int i = 0; i < _count; i++)
                {
                    double sh = stripH * t;
                    if (sh > 0.5)
                        group.Children.Add(new RectangleGeometry(new Rect(0, i * stripH, _w, sh)));
                }
            }
            else
            {
                double stripW = _w / _count;
                for (int i = 0; i < _count; i++)
                {
                    double sw = stripW * t;
                    if (sw > 0.5)
                        group.Children.Add(new RectangleGeometry(new Rect(i * stripW, 0, sw, _h)));
                }
            }

            if (group.Children.Count == 0)
                return new RectangleGeometry(new Rect(0, 0, 0, 0));

            return group;
        }
    }
}
