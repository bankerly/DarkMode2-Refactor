using DarkMode.Core.Interfaces.Location;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Windows.Devices.Geolocation;

namespace DarkMode.Core.Services.Location;

/// <summary>
/// Resolves the device position through the Windows location service.
/// An unpackaged desktop app needs "Let desktop apps access your location"
/// (Settings > Privacy &amp; security > Location) to be enabled.
/// </summary>
public class LocationService : ILocationService
{
    private static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private readonly ILogger<LocationService> _logger;

    public LocationService(ILogger<LocationService>? logger = null)
    {
        _logger = logger ?? NullLogger<LocationService>.Instance;
    }

    public async Task<(double lat, double lon)> GetCurrentLocationAsync()
    {
        try
        {
            return await ReadPositionAsync();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or InvalidOperationException)
        {
            _logger.LogWarning("Windows 位置服务首次访问失败（{Message}），尝试请求授权", ex.Message);

            if (await TryRequestAccessAsync() != GeolocationAccessStatus.Allowed)
            {
                throw new InvalidOperationException("Windows 位置服务未授权：请在 设置 > 隐私和安全性 > 位置 中开启位置并允许桌面应用访问位置", ex);
            }

            return await ReadPositionAsync();
        }
    }

    private async Task<(double lat, double lon)> ReadPositionAsync()
    {
        var locator = new Geolocator { DesiredAccuracyInMeters = 2000 };
        var position = await locator.GetGeopositionAsync(MaxAge, Timeout);
        var coordinates = position.Coordinate;

        _logger.LogInformation("位置已解析：{Lat},{Lon}（精度约 {Accuracy} 米）",
            coordinates.Point.Position.Latitude, coordinates.Point.Position.Longitude, coordinates.Accuracy);

        return (coordinates.Point.Position.Latitude, coordinates.Point.Position.Longitude);
    }

    private static async Task<GeolocationAccessStatus> TryRequestAccessAsync()
    {
        try
        {
            return await Geolocator.RequestAccessAsync();
        }
        catch (Exception)
        {
            // RequestAccessAsync is documented for packaged apps; plain desktop apps
            // get an exception here and rely on the system-wide desktop switch instead.
            return GeolocationAccessStatus.Denied;
        }
    }
}
