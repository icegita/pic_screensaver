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
            Id = id; Name = name; Description = description;
        }

        public static readonly BlindsTransition BlindsH = new BlindsTransition(
            "BlindsH", "BlindsH", "水平百叶窗式揭开");

        public static readonly BlindsTransition BlindsV = new BlindsTransition(
            "BlindsV", "BlindsV", "垂直百叶窗式揭开");

        public Storyboard Build(FrameworkElement outgoing, FrameworkElement incoming, double duration)
        {
            bool horizontal = (Id == "BlindsH");
            return BuildBlinds(outgoing, incoming, duration, horizontal);
        }

        private Storyboard BuildBlinds(FrameworkElement outgoing, FrameworkElement incoming,
            double duration, bool horizontal)
        {
            var sb = new Storyboard();
            sb.Duration = new Duration(TimeSpan.FromSeconds(duration));

            double w = outgoing.ActualWidth;
            double h = outgoing.ActualHeight;
            const int count = 10;

            incoming.Opacity = 1.0;
            outgoing.Opacity = 1.0;
            Panel.SetZIndex(incoming, 1);

            // 优化：预先分配好所有条带的 RectangleGeometry，每帧只修改 Rect，不 new 对象
            var group = new GeometryGroup();
            var strips = new RectangleGeometry[count];
            for (int i = 0; i < count; i++)
            {
                strips[i] = new RectangleGeometry(new Rect(0, 0, 0, 0));
                group.Children.Add(strips[i]);
            }

            var anim = new BlindsGeometryAnimation(w, h, count, horizontal,
                TimeSpan.FromSeconds(duration), group, strips);
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

        private readonly double _w, _h;
        private readonly int _count;
        private readonly bool _horizontal;
        private readonly TimeSpan _duration;

        // 优化：复用同一批 RectangleGeometry 对象，每帧只改 Rect
        private readonly GeometryGroup _group;
        private readonly RectangleGeometry[] _strips;

        public BlindsGeometryAnimation(double w, double h, int count, bool horizontal,
            TimeSpan duration, GeometryGroup group, RectangleGeometry[] strips)
        {
            _w = w; _h = h; _count = count; _horizontal = horizontal;
            _duration = duration; _group = group; _strips = strips;
        }

        protected override Freezable CreateInstanceCore()
        {
            // 注意：CreateInstanceCore 要求返回新实例，但动画实际运行时走 GetCurrentValue
            var newGroup = new GeometryGroup();
            var newStrips = new RectangleGeometry[_count];
            for (int i = 0; i < _count; i++)
            {
                newStrips[i] = new RectangleGeometry(new Rect(0, 0, 0, 0));
                newGroup.Children.Add(newStrips[i]);
            }
            return new BlindsGeometryAnimation(_w, _h, _count, _horizontal, _duration, newGroup, newStrips);
        }

        public override object GetCurrentValue(object defaultOriginValue,
            object defaultDestinationValue, AnimationClock clock)
        {
            if (clock?.CurrentProgress == null)
                return _group;

            double t = clock.CurrentProgress.Value;

            if (_horizontal)
            {
                double stripH = _h / _count;
                for (int i = 0; i < _count; i++)
                {
                    double sh = stripH * t;
                    // 直接修改已有 RectangleGeometry，不 new
                    _strips[i].Rect = sh > 0.5
                        ? new Rect(0, i * stripH, _w, sh)
                        : new Rect(0, 0, 0, 0);
                }
            }
            else
            {
                double stripW = _w / _count;
                for (int i = 0; i < _count; i++)
                {
                    double sw = stripW * t;
                    _strips[i].Rect = sw > 0.5
                        ? new Rect(i * stripW, 0, sw, _h)
                        : new Rect(0, 0, 0, 0);
                }
            }

            return _group;
        }
    }
}