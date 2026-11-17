using UnityEngine;

namespace TEngine.Extensions.UGUI.Feedback
{
    public static class UIFeedback
    {
        public static void Raise(Component source, UIAudioCue cue)
        {
            UIAudioRouter.Raise(source, cue);
        }
    }
}
