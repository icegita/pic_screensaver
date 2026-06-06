using System.Windows;
using System.Windows.Media.Animation;

namespace PicScreenSaver.Runtime.Engine.Transitions
{
    public interface ITransition
    {
        string Id { get; }
        string Name { get; }
        string Description { get; }
        Storyboard Build(FrameworkElement outgoing, FrameworkElement incoming, double duration);
    }
}
