using DarkMode.Core.Interfaces.Hardware;
using LibreHardwareMonitor.Hardware;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Windows.Devices.Sensors;

namespace DarkMode.Core.Services.Hardware;

public class HardwareService : IHardwareService, IDisposable
{
    private readonly ILogger<HardwareService> _logger;

    // Computer.Open() enumerates every device and can take seconds, so it is opened once
    // and reused. Opening it per call froze the UI whenever the developer page polled.
    private readonly object _computerGate = new();
    private Computer? _computer;
    private bool _computerOpened;

    private LightSensor? _lightSensor;
    private bool _lightSensorResolved;

    private bool _isDisposed;

    public HardwareService(ILogger<HardwareService>? logger = null)
    {
        _logger = logger ?? NullLogger<HardwareService>.Instance;
    }

    public float GetGpuUsage()
    {
        lock (_computerGate)
        {
            if (_isDisposed) return 0f;

            if (!_computerOpened)
            {
                _computer = new Computer { IsGpuEnabled = true };
                _computer.Open();
                _computerOpened = true;
            }

            return GetGpuUtilization();
        }
    }

    // LightSensor.GetDefault() returns null on devices without an ambient light
    // sensor (most desktops), so this has to report "unavailable" instead of throwing.
    public float? GetAmbientLight()
    {
        try
        {
            if (!_lightSensorResolved)
            {
                _lightSensor = LightSensor.GetDefault();
                _lightSensorResolved = true;
                _logger.LogInformation(_lightSensor is null ? "未检测到环境光传感器" : "环境光传感器已就绪");
            }

            return _lightSensor?.GetCurrentReading()?.IlluminanceInLux;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "读取环境光失败");
            return null;
        }
    }

    private float GetGpuUtilization()
    {
        var maxGpuUsage = -1f;

        if (_computer is null) return 0f;

        foreach (var hardware in _computer.Hardware)
        {
            hardware.Update();

            if (!IsSupportedGpuType(hardware.HardwareType)) continue;

            var usage = GetGpuUsageFromHardware(hardware);
            if (usage > maxGpuUsage) maxGpuUsage = usage;
        }

        return Math.Clamp(maxGpuUsage < 0 ? 0 : maxGpuUsage, 0, 100);
    }

    private static bool IsSupportedGpuType(HardwareType hardwareType)
    {
        return hardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel;
    }

    private float GetGpuUsageFromHardware(IHardware hardware)
    {
        var maxUsage = -1f;

        foreach (var sensor in hardware.Sensors)
        {
            if (sensor.SensorType != LibreHardwareMonitor.Hardware.SensorType.Load) continue;
            if (sensor.Name != "GPU Core" || !sensor.Value.HasValue) continue;

            var currentUsage = Convert.ToSingle(sensor.Value);
            if (currentUsage > maxUsage) maxUsage = currentUsage;
        }

        return maxUsage;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_isDisposed) return;

        if (disposing)
        {
            lock (_computerGate)
            {
                _computer?.Close();
                _computer = null;
                _computerOpened = false;
            }
        }

        _isDisposed = true;
    }
}
