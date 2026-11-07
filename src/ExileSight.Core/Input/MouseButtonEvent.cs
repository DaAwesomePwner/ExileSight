namespace ExileSight.Core.Input;

public static class MouseButtonEvent
{
    public static PhysicalKey? Decode(uint message, uint data, bool injected,
        Platform.Abstractions.Input.KeyModifiers modifiers, long timestamp)
    {
        var key = message switch
        {
            0x207 or 0x208 => 4,
            0x20B or 0x20C => (data >> 16) switch { 1 => 5, 2 => 6, _ => 0 },
            _ => 0
        };
        return key == 0 ? null : new(key, message is 0x207 or 0x20B, injected, modifiers, timestamp);
    }
}
