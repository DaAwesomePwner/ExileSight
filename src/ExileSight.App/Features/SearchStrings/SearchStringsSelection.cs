using ExileSight.Features.SearchStrings;

namespace ExileSight.App.Features.SearchStrings;

/// <summary>
/// View-only line counters: SearchMenu exposes current text but not its index (duplicate lines are valid).
/// Route vendor and wheel changes through here so the display follows the model's resets and wrapping.
/// </summary>
public sealed class SearchStringsSelection(SearchMenu model)
{
    private int[] indexes = [];
    private int[] counts = [];

    public int Line(int entry) => indexes[entry] + 1;
    public int LineCount(int entry) => counts[entry];

    public void ChooseVendor(string vendor)
    {
        model.ChooseVendor(vendor);
        indexes = new int[model.Entries.Count];
        counts = model.Entries.Select(e => SearchSettings.Compile(e).Length).ToArray();
    }

    public void Cycle(int entry, int direction)
    {
        model.Cycle(entry, direction);
        indexes[entry] = (indexes[entry] + Math.Sign(direction) + counts[entry]) % counts[entry];
    }
}
