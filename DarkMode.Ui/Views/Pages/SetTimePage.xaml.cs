using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using DarkMode.Core.Constants;
using DarkMode.Core.Enums;
using DarkMode.Core.Interfaces.Config;
using DarkMode.Core.Interfaces.Location;
using DarkMode.Core.Models;
using DarkMode.Core.Services.Config;
using DarkMode.Core.Services.Location;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DarkMode.Ui.Views.Pages;

public partial class SetTimePage : Page
{
    private readonly IConfigService _configService;
    private readonly ILocationService _locationService;
    private readonly ILogger<SetTimePage> _logger;
    private bool _loading;

    public SetTimePage(
        IConfigService? configService = null,
        ILocationService? locationService = null,
        ILogger<SetTimePage>? logger = null)
    {
        _configService = configService ?? new ConfigService();
        _locationService = locationService ?? new LocationService();
        _logger = logger ?? NullLogger<SetTimePage>.Instance;

        InitializeComponent();

        Loaded += OnLoaded;
        SunRiseSet.Checked += OnSunriseSunsetToggled;
        SunRiseSet.Unchecked += OnSunriseSunsetToggled;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _loading = true;
        try
        {
            var settings = _configService.LoadConfig<UserSettings>(PathConstants.UserSettingsPath);
            SunRiseSet.IsChecked = settings.UseSunriseSunset;
            ShowCoordinates(settings);
        }
        finally
        {
            _loading = false;
        }
    }

    private async void OnSunriseSunsetToggled(object sender, RoutedEventArgs e)
    {
        if (_loading) return;

        var enabled = SunRiseSet.IsChecked == true;

        _configService.UpdateConfig<UserSettings>(PathConstants.UserSettingsPath, settings =>
        {
            settings.UseSunriseSunset = enabled;
            if (enabled) settings.ExecutionMode = ExecutionType.TimeSchedule;
        });

        if (!enabled) return;

        var current = _configService.LoadConfig<UserSettings>(PathConstants.UserSettingsPath);
        if (HasCoordinates(current))
        {
            ShowCoordinates(current);
            return;
        }

        await RefreshCoordinatesAsync();
    }

    private async Task RefreshCoordinatesAsync()
    {
        try
        {
            location.Text = "正在通过 Windows 位置服务定位…";
            var (latitude, longitude) = await _locationService.GetCurrentLocationAsync();

            var lat = latitude.ToString("0.######", CultureInfo.InvariantCulture);
            var lon = longitude.ToString("0.######", CultureInfo.InvariantCulture);

            _configService.UpdateConfig<UserSettings>(PathConstants.UserSettingsPath, settings =>
            {
                settings.Latitude = lat;
                settings.Longitude = lon;
            });

            ShowCoordinates(_configService.LoadConfig<UserSettings>(PathConstants.UserSettingsPath));
            _logger.LogInformation("日出日落模式坐标已保存：{Lat},{Lon}", lat, lon);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "获取经纬度失败");
            location.Text = ex.Message;
        }
    }

    private void ShowCoordinates(UserSettings settings)
    {
        var unknown = "未获取（打开开关后自动定位）";
        lat.Text = string.IsNullOrWhiteSpace(settings.Latitude) ? unknown : settings.Latitude;
        lng.Text = string.IsNullOrWhiteSpace(settings.Longitude) ? unknown : settings.Longitude;

        if (HasCoordinates(settings)) location.Text = "坐标来源：Windows 位置服务";
    }

    private static bool HasCoordinates(UserSettings settings)
        => double.TryParse(settings.Latitude, NumberStyles.Float, CultureInfo.InvariantCulture, out _)
           && double.TryParse(settings.Longitude, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
}
