using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DarkMode.Core.Interfaces.Theme;
using MaterialColorUtilities.Palettes;
using MaterialColorUtilities.Schemes;
using MaterialColorUtilities.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Win32;

namespace DarkMode.Core.Services.Theme;

public class MonetColorService : IMonetColorService
{
    // Android's default "tonal spot" seed, used when no wallpaper can be read.
    private const uint FallbackSeed = 0xFF6750A4;

    private readonly ILogger<MonetColorService> _logger;

    public MonetColorService(ILogger<MonetColorService>? logger = null)
    {
        _logger = logger ?? NullLogger<MonetColorService>.Instance;
    }

    public MonetPalette FromSeed(uint seedArgb)
    {
        var core = CorePalette.Of(seedArgb, Style.TonalSpot);
        var light = new LightSchemeMapper().Map(core);
        var dark = new DarkSchemeMapper().Map(core);

        return new MonetPalette(
            seedArgb,
            light.Primary, light.Secondary, light.Tertiary, light.Surface,
            dark.Primary, dark.Secondary, dark.Tertiary, dark.Surface);
    }

    public MonetPalette? FromWallpaper()
    {
        foreach (var candidate in WallpaperCandidates())
        {
            try
            {
                if (!File.Exists(candidate)) continue;

                var pixels = ReadPixels(candidate);
                if (pixels is null || pixels.Length == 0) continue;

                var best = ImageUtils.ColorsFromImage(pixels);
                if (best.Count == 0) continue;

                var seed = best[0];
                _logger.LogInformation("莫奈取色：壁纸 {File} → 种子色 #{Seed:X8}", Path.GetFileName(candidate), seed);
                return FromSeed(seed);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("读取壁纸 {File} 失败：{Message}", Path.GetFileName(candidate), ex.Message);
            }
        }

        _logger.LogInformation("莫奈取色：未能读取壁纸，使用兜底种子色 #{Seed:X8}", FallbackSeed);
        return null;
    }

    private static IEnumerable<string> WallpaperCandidates()
    {
        // The configured wallpaper first, then the file Windows actually renders
        // (covers slideshows and themes, where WallPaper is empty).
        var configured = Registry.CurrentUser
            .OpenSubKey(@"Control Panel\Desktop")
            ?.GetValue("WallPaper") as string;

        if (!string.IsNullOrWhiteSpace(configured)) yield return configured;

        var transcoded = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft", "Windows", "Themes", "TranscodedWallpaper");
        yield return transcoded;

        var cached = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft", "Windows", "Themes", "CachedFiles");
        if (Directory.Exists(cached))
        {
            foreach (var file in Directory.EnumerateFiles(cached, "*.jpg")) yield return file;
        }
    }

    // Small decode + BGRA copy: enough for quantisation, and it works for any format WPF can read.
    private static uint[]? ReadPixels(string path)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.UriSource = new Uri(path);
        image.DecodePixelWidth = 200;
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
        image.EndInit();
        image.Freeze();

        var converted = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
        var stride = converted.PixelWidth * 4;
        var buffer = new byte[stride * converted.PixelHeight];
        converted.CopyPixels(buffer, stride, 0);

        var pixels = new uint[buffer.Length / 4];
        for (var i = 0; i < pixels.Length; i++)
        {
            var b = buffer[i * 4];
            var g = buffer[i * 4 + 1];
            var r = buffer[i * 4 + 2];
            var a = buffer[i * 4 + 3];
            pixels[i] = ((uint)a << 24) | ((uint)r << 16) | ((uint)g << 8) | b;
        }

        return pixels;
    }
}
