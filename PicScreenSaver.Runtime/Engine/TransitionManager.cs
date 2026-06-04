using System;
using System.Collections.Generic;
using System.Linq;
using PicScreenSaver.Runtime.Engine.Transitions;

namespace PicScreenSaver.Runtime.Engine
{
    public class TransitionManager
    {
        private readonly Queue<ITransition> _queue = new Queue<ITransition>();
        private readonly List<ITransition> _selected;
        private readonly Random _random = new Random();

        public TransitionManager(string[] selectedEffectIds)
        {
            _selected = GetTransitionsByIds(selectedEffectIds);
            Refill();
        }

        public ITransition Next()
        {
            if (_queue.Count == 0)
                Refill();
            return _queue.Dequeue();
        }

        public int SelectedCount => _selected.Count;

        private void Refill()
        {
            var shuffled = _selected.OrderBy(_ => _random.Next()).ToList();
            foreach (var effect in shuffled)
                _queue.Enqueue(effect);
        }

        private static List<ITransition> GetTransitionsByIds(string[] ids)
        {
            var all = GetAllTransitions();
            var result = new List<ITransition>();

            if (ids == null || ids.Length == 0)
            {
                result.Add(FadeTransition.Fade);
                result.Add(SlideTransition.SlideLeft);
                return result;
            }

            foreach (var id in ids)
            {
                var match = all.Find(t => t.Id == id);
                if (match != null)
                    result.Add(match);
            }

            if (result.Count == 0)
            {
                result.Add(FadeTransition.Fade);
                result.Add(SlideTransition.SlideLeft);
            }

            return result;
        }

        public static List<ITransition> GetAllTransitions()
        {
            var list = new List<ITransition>();
            list.AddRange(new ITransition[]
            {
                FadeTransition.Fade,
                FadeTransition.FadeBlack,
                FadeTransition.CrossFade,
                SlideTransition.SlideLeft,
                SlideTransition.SlideRight,
                SlideTransition.SlideUp,
                SlideTransition.SlideDown,
                WipeTransition.WipeLeft,
                WipeTransition.WipeRight,
                WipeTransition.WipeUp,
                WipeTransition.WipeDown,
                SpecialTransition.FlipHorizontal,
                SpecialTransition.FlipVertical,
                SpecialTransition.PushLeft,
                SpecialTransition.PushUp,
            });
            return list;
        }
    }
}
