using System;
using System.Collections.Generic;
using UnityEngine;

namespace TEngine.Extensions.UGUI.Feedback
{
    public static class UIAudioRouter
    {
        private static readonly List<ProfileBinding> Bindings = new();

        public static Func<UIAudioDevice> DeviceResolver { get; set; }
        public static Func<bool> ProgrammaticInputDetector { get; set; }
        public static Action<AudioClip> AudioPlayer { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Bindings.Clear();
            DeviceResolver = null;
            ProgrammaticInputDetector = null;
            AudioPlayer = null;
        }

        public static IDisposable Bind(UIAudioProfile profile)
        {
            if (profile == null)
            {
                return null;
            }

            var binding = new ProfileBinding(profile);
            Bindings.Add(binding);
            return binding;
        }

        public static void Unbind(UIAudioProfile profile)
        {
            for (var i = Bindings.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(Bindings[i].Profile, profile))
                {
                    Bindings[i].Dispose();
                    break;
                }
            }
        }

        public static void Raise(Component source, UIAudioCue cue)
        {
            var profile = Bindings.Count > 0
                ? Bindings[Bindings.Count - 1].Profile
                : null;
            if (profile == null)
            {
                return;
            }

            if (ProgrammaticInputDetector?.Invoke() == true)
            {
                return;
            }

            var device = DeviceResolver?.Invoke() ?? UIAudioDevice.KeyboardMouse;
            if (source != null &&
                source.TryGetComponent<UIAudioOverride>(out var audioOverride))
            {
                if (audioOverride.Mode == UIAudioOverrideMode.Silent)
                {
                    return;
                }

                if (audioOverride.TryGetClip(cue, device, out var overrideClip))
                {
                    Play(overrideClip);
                    return;
                }

                if (audioOverride.Mode == UIAudioOverrideMode.Exclusive)
                {
                    return;
                }
            }

            if (profile.TryGetClip(cue, device, out var clip))
            {
                Play(clip);
            }
        }

        private static void Play(AudioClip clip)
        {
            if (clip != null)
            {
                AudioPlayer?.Invoke(clip);
            }
        }

        private sealed class ProfileBinding : IDisposable
        {
            public ProfileBinding(UIAudioProfile profile)
            {
                Profile = profile;
            }

            public UIAudioProfile Profile { get; private set; }

            public void Dispose()
            {
                if (Profile == null)
                {
                    return;
                }

                Bindings.Remove(this);
                Profile = null;
            }
        }
    }
}
