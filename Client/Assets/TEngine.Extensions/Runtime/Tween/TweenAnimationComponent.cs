using LitMotion;
using UnityEngine;

namespace TEngine.Extensions.Tween
{
    [AddComponentMenu("TEngine/Tween Animation")]
    public sealed class TweenAnimationComponent : MonoBehaviour
    {
        public enum EnableAction { None, Replay, PlayForward, ResetToStart }
        public enum DisableAction { Stop, ResetToStart }
        [SerializeField] private EnableAction m_OnEnable;
        [SerializeField] private DisableAction m_OnDisable;
        [SerializeField] private TweenAnimation m_Animation = new TweenAnimation();
        public TweenAnimation Animation => m_Animation;

        private void Awake() => m_Animation.Initialize(this);
        private void OnEnable()
        {
            switch (m_OnEnable)
            {
                case EnableAction.Replay: Play(); break;
                case EnableAction.PlayForward: Forward(); break;
                case EnableAction.ResetToStart: ResetToStart(); break;
            }
        }
        private void OnDisable()
        {
            m_Animation.DOKill();
            if (m_OnDisable == DisableAction.ResetToStart && m_Animation.GetValidationError() == null)
                m_Animation.ResetToFromValue();
        }
        private void OnDestroy() => m_Animation.Dispose();

        public void Play() => m_Animation.DOPlay();
        public void Trigger() => m_Animation.Trigger();
        public void Forward() => m_Animation.PlayForward();
        public void Backward() => m_Animation.PlayBackward();
        public void ResetToStart()
        {
            m_Animation.DOKill();
            m_Animation.ResetToFromValue();
        }
        public MotionHandle PlayForward() => m_Animation.PlayForward();
        public MotionHandle PlayBackward() => m_Animation.PlayBackward();
        public void SetState(bool expanded) => m_Animation.SetState(expanded);
        public void Stop() => m_Animation.DOKill();
        public void Complete() => m_Animation.DOComplete(true);
    }
}
