namespace ExileSight.Core.Input;

public static class ClipboardText
{
    public static string Decode(ReadOnlySpan<char> text)
    {
        var terminator = text.IndexOf('\0');
        if (terminator < 0) throw new InvalidOperationException("Clipboard text lacks a terminator");
        return new string(text[..terminator]);
    }
}
