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
            Id = id; Name = name; Description = description;
        }

        public static readonly CheckerboardTransition Checkerboard = new CheckerboardTransition(
            "Checkerboard", "Checkerboard", "棋盘格小块逐个展开揭示新图");

        public Storyboard Build(FrameworkElement outgoing, FrameworkElement incoming, double duration)
        {
            var sb = new Storyboard();
            sb.Duration = new Duration(TimeSpan.FromSeconds(duration));

            double w = outgoing.ActualWidth;
            double h = outgoing.ActualHeight;
            const int cols = 8, rows = 6;

            incoming.Opacity = 1.0;
            outgoing.Opacity = 1.0;
            Panel.SetZIndex(incoming, 1);

            // 优化：预分配所有格子的 RectangleGeometry
            var group = new GeometryGroup();
            var cells = new RectangleGeometry[rows * cols];
            for (int i = 0; i < cells.Length; i++)
            {
                cells[i] = new RectangleGeometry(new Rect(0, 0, 0, 0));
                group.Children.Add(cells[i]);
            }

            var anim = new CheckerboardGeometryAnimation(w, h, cols, rows,
                TimeSpan.FromSeconds(duration), group, cells);
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

        private readonly double _w, _h;
        private readonly int _cols, _rows;
        private readonly TimeSpan _duration;
        private readonly GeometryGroup _group;
        private readonly RectangleGeometry[] _cells;

        // 预计算延迟分母，避免每帧重复计算
        private readonly double _delayDivisor;

        public CheckerboardGeometryAnimation(double w, double h, int cols, int rows,
            TimeSpan duration, GeometryGroup group, RectangleGeometry[] cells)
        {
            _w = w; _h = h; _cols = cols; _rows = rows;
            _duration = duration; _group = group; _cells = cells;
            _delayDivisor = 1.0 - 0.04 * (rows + cols - 2);
        }

        protected override Freezable CreateInstanceCore()
        {
            var newGroup = new GeometryGroup();
            var newCells = new RectangleGeometry[_rows * _cols];
            for (int i = 0; i < newCells.Length; i++)
            {
                newCells[i] = new RectangleGeometry(new Rect(0, 0, 0, 0));
                newGroup.Children.Add(newCells[i]);
            }
            return new CheckerboardGeometryAnimation(_w, _h, _cols, _rows, _duration, newGroup, newCells);
        }

        public override object GetCurrentValue(object defaultOriginValue,
            object defaultDestinationValue, AnimationClock clock)
        {
            if (clock?.CurrentProgress == null)
                return _group;

            double t = clock.CurrentProgress.Value;
            double cw = _w / _cols;
            double ch = _h / _rows;

            for (int r = 0; r < _rows; r++)
            {
                for (int c = 0; c < _cols; c++)
                {
                    double delay = (r + c) * 0.04;
                    double cellT = (t - delay) / _delayDivisor;

                    int idx = r * _cols + c;
                    if (cellT <= 0)
                    {
                        _cells[idx].Rect = new Rect(0, 0, 0, 0);
                        continue;
                    }
                    if (cellT > 1.0) cellT = 1.0;

                    double tw = cw * cellT;
                    double th = ch * cellT;
                    _cells[idx].Rect = (tw > 0.5 && th > 0.5)
                        ? new Rect(c * cw, r * ch, tw, th)
                        : new Rect(0, 0, 0, 0);
                }
            }

            return _group;
        }
    }
}