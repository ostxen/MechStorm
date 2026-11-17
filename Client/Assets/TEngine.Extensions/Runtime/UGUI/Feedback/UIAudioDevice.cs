using System;

namespace TEngine.Extensions.UGUI.Feedback
{
    [Flags]
    public enum UIAudioDevice
    {
        None = 0,
        Touch = 1 << 0,
        KeyboardMouse = 1 << 1,
        Gamepad = 1 << 2,
        Joystick = 1 << 3,
        All = Touch | KeyboardMouse | Gamepad | Joystick
    }
}
