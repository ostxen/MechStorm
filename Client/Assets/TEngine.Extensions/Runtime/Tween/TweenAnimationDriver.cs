using System.Collections.Generic;
using UnityEngine;
using static TEngine.Extensions.Tween.TweenAnimation;

namespace TEngine.Extensions.Tween
{
    [AddComponentMenu("")]
    internal sealed class TweenAnimationDriver : MonoBehaviour
    {
        private readonly List<TweenAnimation> m_Animations = new List<TweenAnimation>();
        private readonly List<MonoBehaviour> m_Owners = new List<MonoBehaviour>();

        internal static void Register(TweenAnimation animation, MonoBehaviour owner)
        {
            var driver = owner.GetComponent<TweenAnimationDriver>();
            if (driver == null)
            {
                driver = owner.gameObject.AddComponent<TweenAnimationDriver>();
                driver.hideFlags = HideFlags.HideInInspector;
            }
            if (driver.m_Animations.Contains(animation)) return;
            driver.m_Animations.Add(animation);
            driver.m_Owners.Add(owner);
        }

        internal static void Unregister(TweenAnimation animation, MonoBehaviour owner)
        {
            var driver = owner.GetComponent<TweenAnimationDriver>();
            if (driver == null) return;
            var index = driver.m_Animations.IndexOf(animation);
            if (index < 0) return;
            driver.m_Animations.RemoveAt(index);
            driver.m_Owners.RemoveAt(index);
        }

        private void Update() => Tick(UpdateType.Normal);
        private void LateUpdate() => Tick(UpdateType.Late);
        private void FixedUpdate() => Tick(UpdateType.Fixed);
        private void Tick(UpdateType phase)
        {
            for (var i = m_Animations.Count - 1; i >= 0; i--)
            {
                var owner = m_Owners[i];
                var animation = m_Animations[i];
                if (owner == null || !owner.isActiveAndEnabled) animation.DOKill();
                else if (animation.UpdatePhase == phase) animation.MaintainReversibleEndpoint();
                if (owner != null) continue;
                m_Animations.RemoveAt(i);
                m_Owners.RemoveAt(i);
            }
        }
        private void OnDisable() { foreach (var animation in m_Animations) animation.DOKill(); }
        private void OnDestroy() => OnDisable();
    }
}
