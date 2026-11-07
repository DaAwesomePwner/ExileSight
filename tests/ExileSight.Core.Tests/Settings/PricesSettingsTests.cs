using System.Text.Json;
using ExileSight.Core.Settings;
using Xunit;

namespace ExileSight.Core.Tests.Settings;

public sealed class PricesSettingsTests
{
    [Fact]
    public void OldDocumentsReceiveDefaultsAndUnknownPriceFieldsSurvive()
    {
        var old = JsonSerializer.Deserialize("{}", SettingsJsonContext.Default.SettingsDocument)!;
        Assert.Null(old.Prices.League);
        Assert.Equal(60, old.Prices.RefreshMinutes);
        Assert.Equal(1, old.SchemaVersion);
        var settings = JsonSerializer.Deserialize(
            """{"prices":{"league":"Example","refreshMinutes":2,"future":42}}""",
            SettingsJsonContext.Default.SettingsDocument)!;
        Assert.Equal(TimeSpan.FromHours(1), settings.Prices.RefreshInterval);
        var roundTrip = JsonSerializer.Deserialize(JsonSerializer.Serialize(settings, SettingsJsonContext.Default.SettingsDocument),
            SettingsJsonContext.Default.SettingsDocument)!;
        Assert.Equal("Example", roundTrip.Prices.League);
        Assert.Equal(42, roundTrip.Prices.ExtensionData["future"].GetInt32());
    }
}
