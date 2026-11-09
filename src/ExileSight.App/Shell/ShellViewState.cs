using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace ExileSight.App.Shell;

public static class ShellViewState
{
    public static ComboBox? LeagueBox(DependencyObject? root, string? displayedPage) => displayedPage == "Home"
        ? Find<ComboBox>(root).FirstOrDefault(box => Equals(box.Tag, "League")) : null;
    internal static IEnumerable<T> Find<T>(DependencyObject? root) where T : DependencyObject
    {
        if (root is null) yield break;
        if (root is T item) yield return item;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var child in Find<T>(VisualTreeHelper.GetChild(root, i))) yield return child;
    }
    internal static Dictionary<string, (double X, double Y)> Capture(DependencyObject? root) =>
        Find<ScrollViewer>(root).Where(s => s.Name is "PageScroll" or "SessionChart")
            .ToDictionary(s => s.Name, s => (s.HorizontalOffset, s.VerticalOffset));
    internal static void Restore(DependencyObject root, IReadOnlyDictionary<string, (double X, double Y)> offsets)
    {
        foreach (var scroll in Find<ScrollViewer>(root))
            if (offsets.TryGetValue(scroll.Name, out var offset))
            { scroll.ScrollToHorizontalOffset(offset.X); scroll.ScrollToVerticalOffset(offset.Y); }
    }
}
