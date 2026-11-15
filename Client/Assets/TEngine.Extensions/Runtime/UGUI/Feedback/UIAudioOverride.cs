using System;
using UnityEngine;

namespace TEngine.Extensions.UGUI.Feedback
{
    public enum UIAudioOverrideMode
    {
        Fallback,
        Exclusive,
        Silent
    }

    [AddComponentMenu("TEngine/UI/Audio Override")]
    public sealed class UIAudioOverride : MonoBehaviour
    {
        [Serializable]
        public struct Rule
        {
            public UIAudioCue cue;
            public UIAudioDevice devices;
            public AudioClip clip;
        }

        [SerializeField] private UIAudioOverrideMode mode = UIAudioOverrideMode.Fallback;
        [SerializeField] private Rule[] rules;

        public UIAudioOverrideMode Mode => mode;

        public bool TryGetClip(UIAudioCue cue, UIAudioDevice device, out AudioClip clip)
        {
            if (rules != null)
            {
                for (var i = 0; i < rules.Length; i++)
                {
                    var rule = rules[i];
                    if (rule.cue == cue && (rule.devices & device) != 0 && rule.clip != null)
                    {
                        clip = rule.clip;
                        return true;
                    }
                }
            }

            clip = null;
            return false;
        }
    }
}
