using System.Text.RegularExpressions;
using Xunit;

namespace ExileSight.Architecture.Tests;

public sealed class OverlayLayoutGuardTests
{
    private const string Layout = @"(?:global\s*::\s*)?(?:ExileSight\s*\.\s*Core\s*\.\s*Overlay\s*\.\s*)?DefaultOverlayLayout\s*\.\s*\w+";
    // These are feature factory registrations, not OverlayHost.Register calls.
    // Everything else named .Register is checked, regardless of the host variable's name.
    private static readonly string[] FactoryReceivers =
    [
        "Vaalasync.ShopRegistration", "Stashninja.StashRegistration", "Timers.TimersRegistration",
        "Events.EventsRegistration", "Guide.GuideRegistration", "Lab.LabRegistration", "MapInfo.MapInfoRegistration",
        "MapTracker.TrackerRegistration", "SearchStrings.SearchStringsRegistration", "Macros.MacrosRegistration"
    ];

    [Fact]
    public void LabSlotDoesNotOverlapOtherFixedRegions()
    {
        var context = new System.Runtime.Loader.AssemblyLoadContext("lab-layout-audit", isCollectible: true);
        try
        {
            var project = Path.Combine(Repository.Root, "src/ExileSight.Core/ExileSight.Core.csproj");
            var type = context.LoadFromAssemblyPath(AssemblyInventory.Output(project))
                .GetType("ExileSight.Core.Overlay.DefaultOverlayLayout", throwOnError: true)!;
            var lab = type.GetProperty("Lab")!.GetValue(null)!;
            double Value(object rect, string property) => (double)rect.GetType().GetProperty(property)!.GetValue(rect)!;
            foreach (var compact in new[] { false, true })
            {
                var regions = ((System.Collections.IEnumerable)type.GetMethod("FixedRegions")!
                    .Invoke(null, [compact])!).Cast<object>().ToArray();
                Assert.Single(regions, r => r.Equals(lab));
                foreach (var other in regions.Where(r => !r.Equals(lab)))
                    Assert.True(Value(lab, "X") + Value(lab, "Width") <= Value(other, "X") ||
                        Value(other, "X") + Value(other, "Width") <= Value(lab, "X") ||
                        Value(lab, "Y") + Value(lab, "Height") <= Value(other, "Y") ||
                        Value(other, "Y") + Value(other, "Height") <= Value(lab, "Y"));
            }
        }
        finally { context.Unload(); }
    }

    [Fact]
    public void AppUsesCentralOverlayLayout()
    {
        // Fixed rectangles belong in Core/Overlay/DefaultOverlayLayout.cs.
        // No cursor-popup exceptions exist. Any future exception must identify the exact
        // construction and prove that its coordinates derive dynamically from the cursor.
        var offenders = Repository.Files("*.cs")
            .Where(path => Repository.Relative(path).StartsWith("src/ExileSight.App/", StringComparison.Ordinal))
            .Where(path => ViolatesLayout(File.ReadAllText(path)))
            .Select(Repository.Relative).ToArray();
        Assert.Empty(offenders);
    }

    [Theory]
    [InlineData("host.Register(content, new(1, 2, 3, 4));")]
    [InlineData("anything.Register(content, rectangle);")]
    [InlineData("GetHost().Register(content, new(1, 2, 3, 4));")]
    [InlineData("host?.Register(content, new(1, 2, 3, 4));")]
    [InlineData("host.Register(content, new OverlayRect(1, 2, 3, 4));")]
    [InlineData("var rect = new ExileSight.Core.Overlay.OverlayRect(1, 2, 3, 4);")]
    [InlineData("var rect = new global::ExileSight.Core.Overlay.OverlayRect(1, 2, 3, 4);")]
    [InlineData("host.Register(content, DefaultOverlayLayout.Guide with { X = 1 });")]
    [InlineData("var rect = (DefaultOverlayLayout.Guide) with { X = 1 };")]
    [InlineData("var rect = global::ExileSight.Core.Overlay.DefaultOverlayLayout.Guide with { X = 1 };")]
    [InlineData("host.Register(design: new(1, 2, 3, 4), content: content);")]
    [InlineData("host.Register(content, Mutate(DefaultOverlayLayout.Guide));")]
    [InlineData("host.Register(content, useGuide ? DefaultOverlayLayout.Guide : DefaultOverlayLayout.Timers);")]
    [InlineData("host.Register(new Border { Padding = new Thickness(1, 2, 3, 4), Child = content }, new(1, 2, 3, 4));")]
    public void RejectsNoncanonicalLayouts(string source) => Assert.True(ViolatesLayout(source));

    [Theory]
    [InlineData("host.Register(content, DefaultOverlayLayout.Guide);")]
    [InlineData("host.Register(content, global::ExileSight.Core.Overlay.DefaultOverlayLayout.Guide);")]
    [InlineData("host.Register(design: DefaultOverlayLayout.Guide, content: content);")]
    [InlineData("host.Register(new Border { Padding = new Thickness(1, 2, 3, 4), Child = content },\n DefaultOverlayLayout.Guide, clickThrough: false);")]
    [InlineData("host.Register(content, /* placement */ DefaultOverlayLayout.Guide);")]
    [InlineData("// host.Register(content, new(1, 2, 3, 4));")]
    [InlineData("var text = \"new OverlayRect(1, 2, 3, 4)\";")]
    [InlineData("Timers.TimersRegistration.Register(services, views);")]
    public void AcceptsCentralLayoutsAndIgnoresNoncode(string source) => Assert.False(ViolatesLayout(source));

    private static bool ViolatesLayout(string source)
    {
        var code = BoundaryScanner.Code(source);
        if (Regex.IsMatch(code, @"\bnew\s+(?:global\s*::\s*)?(?:\w+\s*\.\s*)*OverlayRect\s*\(") ||
            Regex.IsMatch(code, Layout + @"\s*\)*\s*with\b")) return true;
        foreach (Match call in Regex.Matches(code,
                     @"(?:(?<receiver>\w+(?:\s*(?:\.|::)\s*\w+)*)\s*\??\s*)?\.\s*Register\s*\("))
        {
            var args = Arguments(code, call.Index + call.Length);
            var receiver = Regex.Replace(call.Groups["receiver"].Value, @"\s", "");
            if (FactoryReceivers.Contains(receiver) && args.SequenceEqual(["services", "views"])) continue;
            var named = args.FirstOrDefault(arg => Regex.IsMatch(arg, @"^design\s*:"));
            var layout = named is null ? args.ElementAtOrDefault(1) :
                Regex.Replace(named, @"^design\s*:\s*", "");
            if (layout is null || !Regex.IsMatch(layout, @"\A" + Layout + @"\z")) return true;
        }
        return false;
    }

    // Split only top-level arguments: nested constructors, initializers and invocations
    // must not disguise the design argument. Literal/comment contents were removed above.
    private static List<string> Arguments(string code, int start)
    {
        var args = new List<string>();
        var nesting = new Stack<char>();
        for (var i = start; i < code.Length; i++)
        {
            var c = code[i];
            if (c == ')' && nesting.Count == 0)
            {
                args.Add(code[start..i].Trim());
                return args;
            }
            if (c == ',' && nesting.Count == 0)
            {
                args.Add(code[start..i].Trim());
                start = i + 1;
            }
            else if (c is '(' or '[' or '{' or '<') nesting.Push(c);
            else if (c is ')' or ']' or '}' || c == '>' && nesting.TryPeek(out var top) && top == '<')
            {
                if (nesting.Count == 0) return [];
                nesting.Pop();
            }
        }
        return []; // Incomplete calls fail closed.
    }
}
