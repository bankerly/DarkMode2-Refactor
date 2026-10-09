namespace DarkMode.Core.Interfaces.Theme;

/// <summary>
/// Material You (Monet) palette derived from a seed color or from the current wallpaper.
/// Tones follow the MD3 spec: light scheme uses tone 40, dark scheme tone 80.
/// </summary>
public sealed record MonetPalette(
    uint Seed,
    uint LightPrimary, uint LightSecondary, uint LightTertiary, uint LightSurface,
    uint DarkPrimary, uint DarkSecondary, uint DarkTertiary, uint DarkSurface);

public interface IMonetColorService
{
    /// <summary>Builds a palette from an explicit ARGB seed color.</summary>
    MonetPalette FromSeed(uint seedArgb);

    /// <summary>
    /// Extracts the dominant color of the current desktop wallpaper and builds a palette.
    /// Returns null when no wallpaper file can be read.
    /// </summary>
    MonetPalette? FromWallpaper();
}
