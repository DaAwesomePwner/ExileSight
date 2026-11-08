using ExileSight.Core.Settings;
using ExileSight.Features.MapInfo;
namespace ExileSight.App.Shell.Pages;

public sealed record OverlayPageModel(OverlaySettings Settings, MapInfoSettings Thresholds, double AspectRatio = 16d / 9);
