namespace ExileSight.Core.Input;

/// <summary>Native flag/message policy shared by callbacks and portable tests.</summary>
public static class NativeHookEvent
{
    public static bool KeyboardAllowed(uint message, uint flags) =>
        (flags & 0x10) == 0 && message is 0x100 or 0x101 or 0x104 or 0x105;
    public static bool MouseAllowed(uint message, uint flags) =>
        (flags & 1) == 0 && IsMouseButton(message);
    public static bool IsMouseButton(uint message) => message is 0x207 or 0x208 or 0x20B or 0x20C;
}
