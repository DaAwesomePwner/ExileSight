using ExileSight.Core.Input;
using ExileSight.Core.Settings;
using ExileSight.Platform.Abstractions.Input;
using Xunit;

namespace ExileSight.Core.Tests.Input;

public sealed class BindingMapperTests
{
    private static PersistedBinding Copy(string id = "copy", int key = 118) =>
        new() { Id = id, Key = key, Action = ActionKind.CopyItem };

    public static IEnumerable<object[]> InvalidBindings()
    {
        foreach (var key in new[] { -1, 0, 1, 2, 3, 255, 256, int.MaxValue })
            yield return [Copy(key: key), "Invalid VK"];
        foreach (var modifiers in new[] { -1, 16, 32 })
        {
            var b = Copy(); b.Modifiers = (KeyModifiers)modifiers;
            yield return [b, "Invalid modifiers"];
        }
        foreach (var context in new string?[] { null, "", " ", "unknown" })
        {
            var b = Copy(); b.Context = context!;
            yield return [b, "Unknown context"];
        }
        foreach (var text in new string?[] { null, "", " ", "/a\n/b", "/a\r/b", "/a\0", "/a\u2028/b", "/a\u2029/b" })
        {
            var b = Copy(); b.Action = ActionKind.SendChatCommand; b.Text = text;
            yield return [b, "Chat text must be nonempty and single line"];
        }
        var both = Copy(); both.FeatureCommand = "overlay.toggle";
        yield return [both, "Choose an action or a feature command"];
        foreach (var command in new string?[] { null, "", " " })
        {
            var b = Copy(); b.Action = null; b.FeatureCommand = command;
            yield return [b, "Choose an action or a feature command"];
        }
        var unknownCommand = Copy(); unknownCommand.Action = null; unknownCommand.FeatureCommand = "unknown";
        yield return [unknownCommand, "Unknown feature command"];
        var unknownAction = Copy(); unknownAction.Action = (ActionKind)42;
        yield return [unknownAction, "Unknown action"];
        yield return [Copy(id: ""), "ID required"];
    }

    [Theory]
    [MemberData(nameof(InvalidBindings))]
    public void SemanticErrorsRejectOnlyTheInvalidBinding(PersistedBinding invalid, string reason)
    {
        var mapped = BindingMapper.Map([invalid, Copy("valid", 119)], new(), []);
        Assert.Equal("valid", Assert.Single(mapped.Bindings).Id);
        Assert.Contains(reason, Assert.Single(mapped.Rejected));
    }

    [Fact]
    public void NullEntryIsReportedDefensively()
    {
        var mapped = BindingMapper.Map([null, Copy()], new(), []);
        Assert.Single(mapped.Bindings);
        Assert.Equal("<null>: Binding required", Assert.Single(mapped.Rejected));
    }

    [Fact]
    public void DisabledBindingsDoNotReserveIdsOrChordsAndRetainReasons()
    {
        var disabled = Copy(); disabled.Enabled = false;
        var invalid = Copy("invalid", -1); invalid.Enabled = false;
        var mapped = BindingMapper.Map([disabled, invalid, Copy()], new(), []);
        Assert.Single(mapped.Bindings);
        Assert.Equal("copy: Disabled in settings", mapped.Rejected[0]);
        Assert.Contains("Disabled in settings: Invalid VK", mapped.Rejected[1]);
        Assert.False(disabled.Enabled);
        Assert.Equal(-1, invalid.Key);
    }

    [Fact]
    public void DuplicateIdsAreCaseInsensitive()
    {
        var mapped = BindingMapper.Map([Copy(), Copy("COPY", 119)], new(), []);
        Assert.Single(mapped.Bindings);
        Assert.Equal("COPY: Duplicate ID", Assert.Single(mapped.Rejected));
    }

    [Fact]
    public void RejectedChordDoesNotReserveItsId()
    {
        var mapped = BindingMapper.Map([Copy(), Copy("second"), Copy("second", 119)], new(), []);
        Assert.Equal(2, mapped.Bindings.Count);
        Assert.Equal("second: Duplicate key and modifiers in context", Assert.Single(mapped.Rejected));
    }

    [Fact]
    public void DifferentModifiersCanShareAKey()
    {
        var modified = Copy("modified"); modified.Modifiers = KeyModifiers.Control;
        var mapped = BindingMapper.Map([Copy(), modified], new(), []);
        Assert.Equal(2, mapped.Bindings.Count);
        Assert.Empty(mapped.Rejected);
    }
    [Theory]
    [InlineData(0x10, KeyModifiers.None)]
    [InlineData(0x11, KeyModifiers.None)]
    [InlineData(0x12, KeyModifiers.None)]
    [InlineData(0xA0, KeyModifiers.Shift)]
    [InlineData(0xA1, KeyModifiers.Shift)]
    [InlineData(0xA2, KeyModifiers.Control)]
    [InlineData(0xA3, KeyModifiers.Control)]
    [InlineData(0xA4, KeyModifiers.Alt)]
    [InlineData(0xA5, KeyModifiers.Alt)]
    [InlineData(0x5B, KeyModifiers.Windows)]
    [InlineData(0x5C, KeyModifiers.Windows)]
    public void AmbiguousOrSelfModifiedKeysAreRejected(int key, KeyModifiers modifiers)
    {
        var binding = Copy(key: key); binding.Modifiers = modifiers;
        var result = BindingMapper.Map([binding], new(), []);
        Assert.Empty(result.Bindings);
        Assert.Single(result.Rejected);
    }

    [Theory]
    [InlineData(400, true)]
    [InlineData(401, false)]
    public void ChatHasConservativeFourHundredCharacterLimit(int length, bool accepted)
    {
        var binding = Copy(); binding.Action = ActionKind.SendChatCommand;
        binding.Text = new string('a', length);
        var result = BindingMapper.Map([binding], new(), []);
        Assert.Equal(accepted ? 1 : 0, result.Bindings.Count);
        if (!accepted) Assert.Contains("400 characters", Assert.Single(result.Rejected));
    }
}
