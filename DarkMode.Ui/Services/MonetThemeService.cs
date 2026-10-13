using System.Windows.Media;
using DarkMode.Core.Interfaces.Theme;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.Logging;

namespace DarkMode.Ui.Services;

/// <summary>
/// Turns the Material You palette (seed color taken from the wallpaper) into the
/// MaterialDesign theme of the application.
/// </summary>
public class MonetThemeService
{
    private readonly IMonetColorService _monetColorService;
    private readonly ILogger<MonetThemeService> _logger;
    private readonly PaletteHelper _paletteHelper = new();

    /// <summary>Surface colour of the theme currently applied (for translucent windows).</summary>
    public Color SurfaceColor { get; private set; } = Color.FromRgb(0x1A, 0x1A, 0x1A);

    public bool IsDark { get; private set; }

    /// <summary>Raised after a theme was applied, so open windows can re-tint themselves.</summary>
    public event Action<Color, bool>? ThemeApplied;

    public MonetThemeService(IMonetColorService monetColorService, ILogger<MonetThemeService> logger)
    {
        _monetColorService = monetColorService;
        _logger = logger;
    }

    /// <summary>Applies the wallpaper palette; <paramref name="dark"/> selects the dark scheme.</summary>
    public void Apply(bool dark)
    {
        var palette = _monetColorService.FromWallpaper() ?? _monetColorService.FromSeed(0xFF6750A4);

        var primary = FromArgb(dark ? palette.DarkPrimary : palette.LightPrimary);
        var secondary = FromArgb(dark ? palette.DarkSecondary : palette.LightSecondary);

        var theme = Theme.Create(dark ? BaseTheme.Dark : BaseTheme.Light, primary, secondary);
        _paletteHelper.SetTheme(theme);

        IsDark = dark;
        SurfaceColor = FromArgb(dark ? palette.DarkSurface : palette.LightSurface);

        _logger.LogInformation("莫奈主题已应用：{Scheme} 主色 {Primary} 次色 {Secondary}（种子 #{Seed:X8}）",
            dark ? "深色" : "浅色", primary, secondary, palette.Seed);

        ThemeApplied?.Invoke(SurfaceColor, dark);
    }

    private static Color FromArgb(uint argb) => Color.FromArgb(
        (byte)((argb >> 24) & 0xFF),
        (byte)((argb >> 16) & 0xFF),
        (byte)((argb >> 8) & 0xFF),
        (byte)(argb & 0xFF));
}
