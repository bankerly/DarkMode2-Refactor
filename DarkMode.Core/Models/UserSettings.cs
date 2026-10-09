using DarkMode.Core.Enums;

namespace DarkMode.Core.Models;

public class UserSettings
{
    public ExecutionType ExecutionMode { get; set; } = ExecutionType.TimeSchedule;

    /// <summary>Use sunrise/sunset of the configured coordinates instead of fixed light/dark times.</summary>
    public bool UseSunriseSunset { get; set; } = false;

    public LightConfig LightConfig { get; set; } = new();
    public DarkConfig DarkConfig { get; set; } = new();

    public string Longitude { get; set; } = null;
    public string Latitude { get; set; } = null;
    public double AmbientLightSensorThreshold { get; set; } = 17.0;
    public int AmbientLightSensorStabilizationSeconds { get; set; }
    public float GpuUsage { get; set; }
    public int GpuUsageStabilizationSeconds { get; set; }
}

public class LightConfig
{
    public string Time { get; set; } = "6:00";
    public SystemCursor SystemCursor { get; set; } = SystemCursor.WhiteCursor;
}

public class DarkConfig
{
    public string Time { get; set; } = "18:00";
    public SystemCursor SystemCursor { get; set; } = SystemCursor.BlackCursor;
}
