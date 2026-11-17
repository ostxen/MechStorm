using System;
using UnityEngine;

namespace TEngine.Extensions.UGUI.Feedback
{
    [CreateAssetMenu(menuName = "TEngine Extensions/UI Audio Profile", fileName = "UIAudioProfile")]
    public sealed class UIAudioProfile : ScriptableObject
    {
        [Serializable]
        public struct Rule
        {
            public UIAudioCue cue;
            public UIAudioDevice devices;
            public AudioClip clip;
        }

        [SerializeField] private Rule[] rules;

        public Rule[] Rules => rules;

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
