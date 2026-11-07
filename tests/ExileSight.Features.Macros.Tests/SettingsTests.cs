using System.Text.Json;
using ExileSight.Features.Macros;
using Xunit;

namespace ExileSight.Features.Macros.Tests;

public sealed class SettingsTests
{
    [Theory]
    [InlineData("hello", true)]
    [InlineData("/hideout", true)]
    [InlineData("", true)]
    [InlineData("a\nb", false)]
    [InlineData("a\rb", false)]
    [InlineData("a\tb", false)]
    [InlineData("a\u2028b", false)]
    [InlineData("a\0b", false)]
    [InlineData(null, false)]
    public void ValidatesSingleLine(string? value, bool valid) =>
        Assert.Equal(valid, MacroSettings.ValidText(value));
    [Fact]
    public void ExactLengthAndSlotsAndSlashDestinations()
    {
        Assert.True(MacroSettings.ValidText(new string('a', 400)));
        Assert.False(MacroSettings.ValidText(new string('a', 401)));
        var settings = MacroSettings.Default;
        settings.Destinations[0] = "hello";
        Assert.False(settings.IsValid);
        Assert.False(new MacroSettings([], []).IsValid);
        Assert.False(new MacroSettings(null!, null!).IsValid);
    }
    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{\"Commands\":[null],\"Destinations\":null}")]
    public void CorruptSettingsDisableSlots(string json)
    {
        var settings = MacroSettings.Read(JsonSerializer.Deserialize<JsonElement>(json));
        Assert.All(settings.Commands.Concat(settings.Destinations), s => Assert.Equal("", s));
    }
}
