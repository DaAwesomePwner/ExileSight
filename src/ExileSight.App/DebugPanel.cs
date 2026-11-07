using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ExileSight.Core.Diagnostics;

namespace ExileSight.App;

public sealed class DebugPanel : Window
{
    private readonly IDebugSection[] sections;
    private readonly StackPanel body = new() { Margin = new Thickness(16) };
    private readonly DispatcherTimer timer;

    public DebugPanel(IEnumerable<IDebugSection> sections)
    {
        this.sections = sections.ToArray();
        Title = "ExileSight diagnostics";
        Width = 640;
        Height = 480;
        Content = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        timer.Tick += Refresh;
        Loaded += (_, _) => { Refresh(this, EventArgs.Empty); timer.Start(); };
        Closed += (_, _) => { timer.Stop(); timer.Tick -= Refresh; };
    }

    private void Refresh(object? sender, EventArgs e)
    {
        body.Children.Clear();
        body.Children.Add(new TextBlock
        {
            Text = "This product isn't affiliated with or endorsed by Grinding Gear Games in any way.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 16)
        });
        foreach (var section in sections)
        {
            var snapshot = DebugSnapshot.Capture(section);
            body.Children.Add(new TextBlock { Text = snapshot.Title, FontWeight = FontWeights.Bold });
            foreach (var pair in snapshot.Rows)
                body.Children.Add(new TextBlock { Text = $"{pair.Key}: {pair.Value}", TextWrapping = TextWrapping.Wrap });
        }
    }
}
