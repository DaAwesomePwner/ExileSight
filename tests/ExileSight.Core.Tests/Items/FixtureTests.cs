using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ExileSight.Core.Games;
using ExileSight.Core.Items;
using Xunit;

namespace ExileSight.Core.Tests.Items;

public sealed class FixtureTests
{
    private static readonly JsonSerializerOptions Options = new() { Converters = { new JsonStringEnumConverter() } };
    public static IEnumerable<object[]> Copies()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "ExileSight.slnx"))) root = root.Parent;
        if (root is null) throw new InvalidOperationException("Repository not found");
        return Directory.EnumerateFiles(Path.Combine(root.FullName, "tests/fixtures/poe1/items"), "*.txt", SearchOption.AllDirectories)
            .Where(p => p.EndsWith(".plain.txt") || p.EndsWith(".advanced.txt")).Order().Select(p => new object[] { p });
    }

    [Theory]
    [MemberData(nameof(Copies))]
    public void CopyMatchesIndependentExpectations(string path)
    {
        var result = ItemTextParser.Parse(File.ReadAllText(path), Game.PoE1);
        Assert.DoesNotContain(result.Warnings, w => w.Severity == WarningSeverity.Error);
        Assert.NotNull(result.Item);
        var variant = path.EndsWith(".plain.txt") ? "plain" : "advanced";
        var expectedPath = path[..^(variant.Length + 4)] + "expected.json";
        if (!File.Exists(expectedPath)) return;
        var expected = JsonNode.Parse(File.ReadAllText(expectedPath))!;
        var actual = JsonSerializer.SerializeToNode(result.Item, Options)!;
        actual["Warnings"] = JsonSerializer.SerializeToNode(result.Warnings, Options);
        Compare(expected["common"]!, actual, path);
        if (expected[variant] is { } specific) Compare(specific, actual, path);
    }

    private static void Compare(JsonNode expected, JsonNode? actual, string path)
    {
        if (expected is JsonObject obj)
        {
            Assert.IsType<JsonObject>(actual);
            foreach (var field in obj)
            {
                Assert.True(actual!.AsObject().ContainsKey(field.Key), $"{path}: missing {field.Key}");
                if (field.Value is null) Assert.Null(actual[field.Key]);
                else Compare(field.Value, actual[field.Key], path + "." + field.Key);
            }
        }
        else if (expected is JsonArray array)
        {
            var values = Assert.IsType<JsonArray>(actual);
            Assert.Equal(array.Count, values.Count);
            for (var i = 0; i < array.Count; i++) Compare(array[i]!, values[i], path + $"[{i}]");
        }
        else Assert.True(JsonNode.DeepEquals(expected, actual), $"{path}: expected {expected}, actual {actual}");
    }
}
