using Xunit;
#if SNAPSHOT_WINDOWS
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using ExileSight.App.Features.Guide;
using ExileSight.App.Features.Timers;
using ExileSight.App.Features.Events;
using ExileSight.App.Ui;
using ExileSight.Features.Guide;
using ExileSight.Features.Timers;
#endif

namespace ExileSight.App.Snapshots.Tests;

public sealed class AmbientHudSnapshotTests
{
    [WindowsOnlyFact]
    public void GuideMidAct()
    {
#if SNAPSHOT_WINDOWS
        Render("guide-mid-act", t => GuideCardBuilder.Build(Guide(), t), 340, 140);
#endif
    }

    [WindowsOnlyFact]
    public void GuideNotice()
    {
#if SNAPSHOT_WINDOWS
        Render("guide-notice", t => GuideCardBuilder.Build(Guide() with
            { Notice = "Timing uncertain: log gap/source change." }, t), 340, 180);
#endif
    }

    [WindowsOnlyFact]
    public void GuideComplete()
    {
#if SNAPSHOT_WINDOWS
        Render("guide-complete", t => GuideCardBuilder.Build(Guide() with
            { Act = 10, Step = 8, StepCount = 8, Progress = 1, Complete = true,
                Lines = ["Return to town"], NextLine = null, Notice = "Guide complete" }, t), 340, 140);
#endif
    }

    [WindowsOnlyFact]
    public void TimersNormal()
    {
#if SNAPSHOT_WINDOWS
        Render("timers-normal", t => TimersCardBuilder.Build([
            new("Garden", TimerKind.Countdown, TimeSpan.FromMinutes(4), TimeSpan.FromMinutes(10))], t), 300, 80);
#endif
    }

    [WindowsOnlyFact]
    public void TimersUnderOneMinute()
    {
#if SNAPSHOT_WINDOWS
        Render("timers-urgent", t => TimersCardBuilder.Build([
            new("Contract", TimerKind.Countdown, TimeSpan.FromSeconds(35), TimeSpan.FromMinutes(3))], t), 300, 80);
#endif
    }

    [WindowsOnlyFact]
    public void Stopwatch()
    {
#if SNAPSHOT_WINDOWS
        Render("timers-stopwatch", t => TimersCardBuilder.Build([
            new("Stopwatch", TimerKind.Stopwatch, TimeSpan.FromSeconds(195), TimeSpan.Zero)], t), 300, 80);
#endif
    }

    [WindowsOnlyFact]
    public void TimersFitCompactRegion()
    {
#if SNAPSHOT_WINDOWS
        Render("timers-capacity", t =>
        {
            var rows = Enumerable.Range(1, 6).Select(i => new TimerCardRow(
                $"Timer {i}: a long user-provided timer name that must fit the compact display",
                TimerKind.Countdown, TimeSpan.FromHours(30), TimeSpan.FromHours(48))).ToArray();
            var card = TimersCardBuilder.Build(rows, t);
            card.Measure(new Size(300, double.PositiveInfinity));
            Assert.True(card.DesiredSize.Height <= 220);
            Assert.Equal(300, card.Width);
            card.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Assert.Equal(300, card.DesiredSize.Width);
            return card;
        }, 300, 220);
#endif
    }

    [WindowsOnlyFact]
    public void GuideLongPageFitsRegion()
    {
#if SNAPSHOT_WINDOWS
        Render("guide-long-page", t =>
        {
            var card = GuideCardBuilder.Build(Guide() with
            {
                Lines = Enumerable.Repeat(new string('W', 2048) + "\n" + new string('W', 2047), 100).ToArray(),
                NextLine = new string('N', 4096), Notice = new string('X', 4096)
            }, t);
            card.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Assert.Equal(340, card.DesiredSize.Width);
            Assert.True(card.DesiredSize.Height <= 440);
            var body = Assert.IsType<StackPanel>(Assert.IsType<Border>(card).Child);
            Assert.Contains(body.Children.OfType<TextBlock>(), text => text.Text == "+88 more lines");
            Assert.Equal(GuideCardBuilder.MaximumLines,
                body.Children.OfType<TextBlock>().Count(text => text.FontSize == 14));
            return card;
        }, 340, 440);
#endif
    }

    [WindowsOnlyFact]
    public void Toast()
    {
#if SNAPSHOT_WINDOWS
        Render("event-toast", t => EventToastBuilder.Build(new("A test encounter is available", "Area event"), t), 340, 80);
#endif
    }

#if SNAPSHOT_WINDOWS
    private static GuideSnapshot Guide() => new(true, 2, 3, 9, TimeSpan.FromSeconds(520),
        TimeSpan.FromSeconds(2010), 1d / 3, ["Travel to the next area", "Find the waypoint"],
        "Return to town for your reward", null, false);

    private static void Render(string name, Func<CardTheme, FrameworkElement> build, int width, int height)
    {
        var path = SnapshotRenderer.Render(() => build(CardTheme.Default), name, new Size(width, height));
        Assert.True(File.Exists(path));
        using var stream = File.OpenRead(path);
        var decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        var frame = Assert.Single(decoder.Frames);
        Assert.Equal(width, frame.PixelWidth); Assert.Equal(height, frame.PixelHeight);
    }
#endif
}
