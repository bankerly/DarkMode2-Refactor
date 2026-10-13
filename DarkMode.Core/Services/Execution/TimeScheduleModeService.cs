using System.Globalization;
using DarkMode.Core.Constants;
using DarkMode.Core.Enums;
using DarkMode.Core.Interfaces.Config;
using DarkMode.Core.Interfaces.Execution;
using DarkMode.Core.Interfaces.Theme;
using DarkMode.Core.Models;
using DarkMode.Core.Services.Config;
using DarkMode.Core.Services.Theme;
using DarkMode.Core.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DarkMode.Core.Services.Execution;

/// <summary>
/// Decides whether the dark period is active: either from the fixed light/dark times the
/// user configured, or from sunrise/sunset computed for the configured coordinates.
/// </summary>
public class TimeScheduleModeService : IExecutionMode
{
    private static readonly TimeSpan DefaultLightTime = new(6, 0, 0);
    private static readonly TimeSpan DefaultDarkTime = new(18, 0, 0);
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromSeconds(20);

    private readonly IConfigService _configService;
    private readonly IThemeService _themeService;
    private readonly ILogger<TimeScheduleModeService> _logger;

    private DateTime _cacheStamp = DateTime.MinValue;
    private bool _cacheValue;
    private DateTime _lastLoggedDate = DateTime.MinValue;
    private DateTime _lastDecisionLog = DateTime.MinValue;

    public TimeScheduleModeService(
        IConfigService? configService = null,
        IThemeService? themeService = null,
        ILogger<TimeScheduleModeService>? logger = null)
    {
        _configService = configService ?? new ConfigService();
        _themeService = themeService ?? new ThemeService();
        _logger = logger ?? NullLogger<TimeScheduleModeService>.Instance;
    }

    public ExecutionType Name => ExecutionType.TimeSchedule;

    /// <summary>
    /// The scheduler runs its tasks whenever this returns true, so it must mean
    /// "the current theme no longer matches the period". Returning the period itself
    /// made the app one-directional: during the light period nothing ever ran.
    /// </summary>
    public bool ShouldExecute()
    {
        var dark = IsDarkPeriod();
        var wanted = dark ? SystemTheme.Dark : SystemTheme.Light;
        var current = _themeService.GetCurrentTheme();
        var needed = current != wanted;

        // heartbeat: makes "why did nothing happen" answerable from the log alone
        var now = DateTime.Now;
        if (now - _lastDecisionLog > TimeSpan.FromSeconds(30))
        {
            _lastDecisionLog = now;
            _logger.LogInformation("时段判定：{Period}，系统主题 {Current}，需要切换 {Needed}",
                dark ? "深色时段" : "浅色时段", current, needed);
        }

        return needed;
    }

    /// <summary>True while the dark period is active.</summary>
    public bool IsDarkPeriod()
    {
        var now = DateTime.Now;
        if (now - _cacheStamp < CacheLifetime) return _cacheValue;

        _cacheValue = ComputeIsDarkPeriod(now);
        _cacheStamp = now;
        return _cacheValue;
    }

    private bool ComputeIsDarkPeriod(DateTime now)
    {
        var settings = _configService.LoadConfig<UserSettings>(PathConstants.UserSettingsPath);

        if (settings.UseSunriseSunset && TryGetCoordinates(settings, out var latitude, out var longitude))
        {
            var sun = SunCalculator.TryCalculate(now, latitude, longitude);
            if (sun is not null)
            {
                var (sunrise, sunset) = sun.Value;

                // once a day, so the computed times are actually visible in the log
                if (_lastLoggedDate != now.Date)
                {
                    _lastLoggedDate = now.Date;
                    _logger.LogInformation(
                        "日出日落模式：{Date:yyyy-MM-dd} 日出 {Sunrise:HH:mm}、日落 {Sunset:HH:mm}（坐标 {Latitude},{Longitude}）",
                        now, sunrise, sunset, latitude, longitude);
                }

                return now < sunrise || now >= sunset;
            }

            _logger.LogWarning("坐标 {Latitude},{Longitude} 当日无日出/日落（极昼极夜），回退到固定时间", latitude, longitude);
        }
        else if (settings.UseSunriseSunset)
        {
            _logger.LogWarning("已开启日出日落模式但经纬度无效（{Latitude},{Longitude}），回退到固定时间",
                settings.Latitude, settings.Longitude);
        }

        var lightTime = ParseTime(settings.LightConfig?.Time, DefaultLightTime);
        var darkTime = ParseTime(settings.DarkConfig?.Time, DefaultDarkTime);
        return now.TimeOfDay >= darkTime || now.TimeOfDay < lightTime;
    }

    private static bool TryGetCoordinates(UserSettings settings, out double latitude, out double longitude)
    {
        latitude = 0;
        longitude = 0;

        return double.TryParse(settings.Latitude, NumberStyles.Float, CultureInfo.InvariantCulture, out latitude)
            && double.TryParse(settings.Longitude, NumberStyles.Float, CultureInfo.InvariantCulture, out longitude);
    }

    private static TimeSpan ParseTime(string? value, TimeSpan fallback)
        => TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
}
