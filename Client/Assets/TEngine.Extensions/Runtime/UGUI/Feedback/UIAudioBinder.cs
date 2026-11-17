using System;
using UnityEngine;

namespace TEngine.Extensions.UGUI.Feedback
{
    [DisallowMultipleComponent]
    [AddComponentMenu("TEngine/UI/Audio Binder")]
    [DefaultExecutionOrder(-500)]
    public sealed class UIAudioBinder : MonoBehaviour
    {
        [SerializeField] private UIAudioProfile profile;
        private IDisposable _binding;

        public UIAudioProfile Profile
        {
            get => profile;
            set
            {
                if (profile == value)
                {
                    return;
                }

                if (isActiveAndEnabled)
                {
                    _binding?.Dispose();
                    _binding = null;
                }

                profile = value;
                if (isActiveAndEnabled)
                {
                    _binding = UIAudioRouter.Bind(profile);
                }
            }
        }

        private void OnEnable()
        {
            _binding = UIAudioRouter.Bind(profile);
        }

        private void OnDisable()
        {
            _binding?.Dispose();
            _binding = null;
        }
    }
}
