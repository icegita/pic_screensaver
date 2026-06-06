using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace PicScreenSaver.Runtime.Engine.Transitions
{
    public class CheckerboardTransition : ITransition
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }

        private CheckerboardTransition(string id, string name, string description)
        {
            Id = id;
            Name = name;
            Description = description;
        }

        public static readonly CheckerboardTransition Checkerboard = new CheckerboardTransition(
            "Checkerboard", "Checkerboard", "棋盘格小块逐个展开揭示新图");

        public Storyboard Build(FrameworkElement outgoing, FrameworkElement incoming, double duration)
        {
            var sb = new Storyboard(); sb.Duration = new Duration(TimeSpan.FromSeconds(duration));
            var time = TimeSpan.FromSeconds(duration);
            double w = outgoing.ActualWidth;
            double h = outgoing.ActualHeight;
            int cols = 8, rows = 6;

            incoming.Opacity = 1.0;
            outgoing.Opacity = 1.0;
            Panel.SetZIndex(incoming, 1);

            var anim = new CheckerboardGeometryAnimation(w, h, cols, rows, time);
            Storyboard.SetTarget(anim, incoming);
            Storyboard.SetTargetProperty(anim, new PropertyPath(UIElement.ClipProperty));
            sb.Children.Add(anim);

            sb.Completed += (s, e) => { incoming.Clip = null; };
            return sb;
        }
    }

    public class CheckerboardGeometryAnimation : AnimationTimeline
    {
        public override Type TargetPropertyType => typeof(Geometry);

        private double _w, _h;
        private int _cols, _rows;
        private TimeSpan _duration;

        public CheckerboardGeometryAnimation(double w, double h, int cols, int rows, TimeSpan duration)
        {
            _w = w; _h = h; _cols = cols; _rows = rows; _duration = duration;
        }

        protected override Freezable CreateInstanceCore()
        {
            return new CheckerboardGeometryAnimation(_w, _h, _cols, _rows, _duration);
        }

        public override object GetCurrentValue(object defaultOriginValue, object defaultDestinationValue, AnimationClock clock)
        {
            if (clock == null || clock.CurrentProgress == null)
                return new RectangleGeometry(new Rect(0, 0, 0, 0));

            double t = clock.CurrentProgress.Value;
            double cw = _w / _cols, ch = _h / _rows;
            var group = new GeometryGroup();

            for (int r = 0; r < _rows; r++)
            {
                for (int c = 0; c < _cols; c++)
                {
                    double delay = (r + c) * 0.04;
                    double cellT = (t - delay) / (1.0 - 0.04 * (_rows + _cols - 2));
                    if (cellT <= 0) continue;
                    if (cellT > 1.0) cellT = 1.0;

                    double tw = cw * cellT;
                    double th = ch * cellT;
                    if (tw > 0.5 && th > 0.5)
                        group.Children.Add(new RectangleGeometry(new Rect(c * cw, r * ch, tw, th)));
                }
            }

            if (group.Children.Count == 0)
                return new RectangleGeometry(new Rect(0, 0, 0, 0));

            return group;
        }
    }
}
