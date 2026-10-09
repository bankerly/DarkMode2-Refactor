using DarkMode.Core.Constants;
using DarkMode.Core.Interfaces.Config;
using DarkMode.Core.Interfaces.Location;
using DarkMode.Core.Models;
using DarkMode.Core.Services.Scheduler;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DarkMode.Ui.Services;

/// <summary>
/// Starts the scheduler with the rest of the host and refreshes the stored coordinates
/// once per start (the "update sunrise/sunset on every start" option).
/// </summary>
public class SchedulerHostedService : IHostedService
{
    private readonly SchedulerService _scheduler;
    private readonly IConfigService _configService;
    private readonly ILocationService _locationService;
    private readonly ILogger<SchedulerHostedService> _logger;

    public SchedulerHostedService(
        SchedulerService scheduler,
        IConfigService configService,
        ILocationService locationService,
        ILogger<SchedulerHostedService> logger)
    {
        _scheduler = scheduler;
        _configService = configService;
        _locationService = locationService;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = RefreshCoordinatesAsync();
        _scheduler.Start();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _scheduler.Stop();
        return Task.CompletedTask;
    }

    private async Task RefreshCoordinatesAsync()
    {
        try
        {
            var settings = _configService.LoadConfig<UserSettings>(PathConstants.UserSettingsPath);
            if (!settings.UseSunriseSunset) return;

            var (latitude, longitude) = await _locationService.GetCurrentLocationAsync();
            var lat = latitude.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);
            var lon = longitude.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);

            if (lat == settings.Latitude && lon == settings.Longitude) return;

            _configService.UpdateConfig<UserSettings>(PathConstants.UserSettingsPath, s =>
            {
                s.Latitude = lat;
                s.Longitude = lon;
            });

            _logger.LogInformation("日出日落模式：坐标已更新为 {Lat},{Lon}", lat, lon);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("日出日落模式：获取坐标失败（{Message}），将沿用已保存的坐标或固定时间", ex.Message);
        }
    }
}
