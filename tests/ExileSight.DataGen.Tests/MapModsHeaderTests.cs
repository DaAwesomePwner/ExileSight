using System.Text;
using ExileSight.DataGen;
using Xunit;

namespace ExileSight.DataGen.Tests;

public sealed class MapModsHeaderTests
{
    [Theory]
    [InlineData("\t;##")]
    [InlineData(" ; comment")]
    [InlineData("")]
    public void CommentedHeaderStartsNewSection(string suffix)
    {
        // Synthetic upstream-format fixture: expedition section followed by unknown mod.
        var input = "[expedition areas]\nID=123\ntype=expedition\ntext=Expedition\n"
            + "[unknown mod]" + suffix + "\nID=000\ntype=unknown\ntext=Unknown\n";
        var result = MapModsConverter.Read(Encoding.UTF8.GetBytes(input));
        Assert.Equal(["unknown mod"], Assert.Single(result.Entries, e => e.Id == "000").Patterns);
        Assert.Equal(["expedition areas"], Assert.Single(result.Entries, e => e.Id == "123").Patterns);
    }
}
