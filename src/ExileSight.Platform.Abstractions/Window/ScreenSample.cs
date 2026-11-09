namespace ExileSight.Platform.Abstractions.Window;

/// <summary>Top-down, row-major, 4 bytes per pixel in B,G,R,A order; Bgra.Length == Width*Height*4.</summary>
public sealed record ScreenSample(int Width, int Height, byte[] Bgra);
