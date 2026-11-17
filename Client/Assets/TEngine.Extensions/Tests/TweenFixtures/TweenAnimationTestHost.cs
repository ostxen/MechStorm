// 此夹具必须在运行时测试程序集中，Unity 才能将其作为组件实例化。
using System.Collections.Generic;
using TEngine.Extensions.Tween;
using UnityEngine;

namespace TEngine.Extensions.Tests.TweenEditor
{
    public sealed class TweenAnimationTestHost : MonoBehaviour
    {
        public TweenAnimation First = new TweenAnimation();
        public TweenAnimation Second = new TweenAnimation();
        public List<TweenAnimation> Animations = new List<TweenAnimation>();
        public int Unrelated;

        private void OnDisable()
        {
            First.DOKill();
            Second.DOKill();
            foreach (var animation in Animations) animation?.DOKill();
        }
        private void OnDestroy()
        {
            First.Dispose();
            Second.Dispose();
            foreach (var animation in Animations) animation?.Dispose();
        }
    }
}
