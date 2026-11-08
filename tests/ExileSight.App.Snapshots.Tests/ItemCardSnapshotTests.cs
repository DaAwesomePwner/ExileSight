using Xunit;
#if SNAPSHOT_WINDOWS
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ExileSight.App.Features.ItemInfo;
using ExileSight.App.Ui;
using ExileSight.Core.Features;
using ExileSight.Core.Items;
using ExileSight.Core.Settings;
using ExileSight.Features.ItemInfo;
#endif

namespace ExileSight.App.Snapshots.Tests;

public sealed class ItemCardSnapshotTests
{
    [WindowsOnlyFact] public void RareImplicitAndCrafted() => Render("rare");
    [WindowsOnlyFact] public void HybridWeapon() => Render("weapon");
    [WindowsOnlyFact] public void UniqueWithFixedLine() => Render("unique");
    [WindowsOnlyFact] public void PlainCopy() => Render("plain");
    [WindowsOnlyFact] public void Unidentified() => Render("unidentified");
    [WindowsOnlyFact] public void CompareDropped() => Render("drop-compare");
    [WindowsOnlyFact] public void SlotsDropped() => Render("drop-slots");
    [WindowsOnlyFact] public void MoreMods() => Render("more-mods");
    [WindowsOnlyFact] public void CompactDensity() => Render("compact");
    [WindowsOnlyFact] public void HighContrast() => Render("high-contrast");

    private static void Render(string state)
    {
#if SNAPSHOT_WINDOWS
        var height = 0;
        var path = SnapshotRenderer.Render(() =>
        {
            var theme = CardTheme.For(state == "compact" ? OverlayDensity.Compact : OverlayDensity.Comfortable,
                state == "high-contrast" ? OverlayContrast.HighContrast : OverlayContrast.Standard);
            Assert.Equal(state == "compact" ? 24 : 28, theme.RowHeight);
            if (state == "high-contrast")
            {
                Assert.Same(CardTheme.Default.OverlayForeground, theme.TextSecondary);
                Assert.Same(CardTheme.Default.TextSecondary, theme.TextMuted);
                Assert.Same(CardTheme.Default.TooltipUnknown, theme.OverlayBorder);
            }
            var model = Model(state);
            double Measure(FrameworkElement element)
            { element.Measure(new Size(300, double.PositiveInfinity)); return element.DesiredSize.Height; }
            var cap = double.PositiveInfinity;
            if (state == "drop-compare") cap = Measure(ItemCardView.Build(model, theme)) - 1;
            if (state == "drop-slots") cap = Measure(ItemCardView.Build(model with { Compare = null }, theme)) - 1;
            if (state == "more-mods") cap = 180;
            var view = ItemCardView.Build(model, theme, cap);
            height = (int)Math.Ceiling(Measure(view));
            Assert.True(height <= Math.Ceiling(cap));
            var elements = Descendants(view).ToArray();
            var texts = elements.OfType<TextBlock>().ToArray();
            Assert.All(texts, text => Assert.NotSame(theme.TooltipUnknown, text.Foreground));
            Assert.All(texts, text => Assert.True(((SolidColorBrush)text.Foreground).IsFrozen));
            if (state.StartsWith("drop-", StringComparison.Ordinal) || state == "more-mods")
                Assert.DoesNotContain(elements, e => Equals(e.Tag, "compare"));
            if (state is "drop-slots" or "more-mods") Assert.DoesNotContain(elements, e => Equals(e.Tag, "slots"));
            if (state == "drop-compare") Assert.Contains(elements, e => Equals(e.Tag, "slots"));
            if (state is "drop-compare" or "drop-slots")
                foreach (var row in model.Rows) Assert.Contains(texts, t => t.Text == row.Label);
            if (state == "more-mods")
            {
                var more = Assert.Single(texts, t => t.Text.EndsWith(" more mods", StringComparison.Ordinal));
                var omitted = int.Parse(more.Text.Split(' ')[0].AsSpan(1));
                Assert.Equal(model.Rows.Count - omitted, texts.Count(t => model.Rows.Any(r => r.Label == t.Text)));
            }
            if (state == "unique")
            {
                Assert.Contains(texts, t => t.Text == "imp");
                Assert.Contains(texts, t => t.Text == "·");
                Assert.Equal(2, texts.Count(t => t.Text == "fixed"));
            }
            if (state == "plain") Assert.Contains(texts, t => t.Text.StartsWith("Tiers and rolls need", StringComparison.Ordinal));
            if (state == "unidentified") Assert.DoesNotContain(texts, t => model.Rows.Any(r => t.Text == r.Label));
            return view;
        }, "item-card-" + state);
        Assert.True(File.Exists(path));
        using var stream = File.OpenRead(path);
        var decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        Assert.Equal(300, decoder.Frames[0].PixelWidth);
        Assert.Equal(height, decoder.Frames[0].PixelHeight);
#endif
    }

#if SNAPSHOT_WINDOWS
    private static IEnumerable<FrameworkElement> Descendants(FrameworkElement root)
    {
        yield return root;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            if (VisualTreeHelper.GetChild(root, i) is FrameworkElement child)
                foreach (var element in Descendants(child)) yield return element;
    }

    private static ItemCard Model(string state)
    {
        ItemCardRow Row(string label, string tier = "T2", CardSlotKind slot = CardSlotKind.Prefix,
            Highlight highlight = Highlight.None, int span = 1) =>
            new(slot, label, tier, InfoColor.High, 72, InfoColor.Medium,
                slot == CardSlotKind.Implicit || tier == "cr" ? CardLevelKind.NotApplicable : CardLevelKind.Known,
                62, highlight, tier == "cr", false, 0, span);
        var rows = new[] { Row("Cold res", "imp", CardSlotKind.Implicit),
            Row("+ max life", highlight: Highlight.Desired), Row("Attack speed", highlight: Highlight.Undesired),
            Row("Fire res", "cr", CardSlotKind.Suffix) };
        var card = new ItemCard(new("Coral Ring", null, ItemRarity.Rare, "Rings", 79, "Fixture ring"), false, false,
            new(1, 1, null), new(new(2, 3), new(1, 3)), [new("Energy Shield", 67)], rows, true,
            new([new("left ring", false, [new("Life", DeltaDirection.Increase, 17), new("Fire res", DeltaDirection.Decrease, 9)]),
                 new("right ring", true, [])], "Default profile · Known item stats only."));
        if (state == "weapon") return card with
        { Name = new("Short Bow", null, ItemRarity.Rare, "Bows", 77, "Fixture bow"),
            Rows = [Row("% phys dmg", span: 2), Row("Attack speed", highlight: Highlight.Desired)], Defences = [] };
        if (state == "unique") return card with
        { Name = new("Leather Belt", "Fixture unique", ItemRarity.Unique, "Belts", 75, "Fixture unique"),
            Slots = null, Verdict = new(0, 0, 72), Compare = null, Defences = [],
            Rows = [rows[0] with { Fixed = true, RollPosition = null }, Row("+ max life", "U", CardSlotKind.None) with { LevelKind = CardLevelKind.NotApplicable },
                Row("Fixed property", "·", CardSlotKind.None) with
                { Fixed = true, RollPosition = null, LevelKind = CardLevelKind.NotApplicable }] };
        if (state == "plain") return card with
        { NeedsAdvancedCopy = true, Compare = null, Verdict = null, Slots = new(new(null, 3), new(null, 3)),
            Rows = rows.Skip(1).Select(r => r with { Tier = "T?", TierTone = InfoColor.Unknown,
                RollPosition = null, RollTone = InfoColor.Unknown, Slot = CardSlotKind.Unknown,
                LevelKind = CardLevelKind.Unknown, Crafted = false }).ToArray() };
        if (state == "unidentified") return card with
        { Unidentified = true, Rows = [], Slots = null, Verdict = null, Compare = null };
        if (state == "more-mods") return card with
        { Rows = Enumerable.Range(0, 20).Select(i => Row("Fixture " + i)).ToArray() };
        return card;
    }
#endif
}
