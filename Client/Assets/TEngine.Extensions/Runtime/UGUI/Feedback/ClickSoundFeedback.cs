using TEngine.Extensions.UGUI.Feedback;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TEngine.Extensions.UGUI
{
    [AddComponentMenu("TEngine/UI/Click Sound Feedback")]
    public sealed class ClickSoundFeedback : MonoBehaviour, IPointerClickHandler
    {
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData != null &&
                eventData.button == PointerEventData.InputButton.Left)
            {
                UIFeedback.Raise(this, UIAudioCue.Click);
            }
        }
    }
}
