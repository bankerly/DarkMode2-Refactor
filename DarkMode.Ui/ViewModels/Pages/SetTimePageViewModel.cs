using System.Globalization;
using System.Windows.Threading;
using DarkMode.Core.Constants;
using DarkMode.Core.Enums;
using DarkMode.Core.Interfaces.Config;
using DarkMode.Core.Interfaces.Hardware;
using DarkMode.Core.Interfaces.Location;
using DarkMode.Core.Models;
using DarkMode.Core.Utils;
using DarkMode.Ui.Models;
using Microsoft.Extensions.Logging;

namespace DarkMode.Ui.ViewModels.Pages;

/// <summary>
/// The three time modes are mutually exclusive: exactly one of custom time, sunrise/sunset
/// and ambient light sensor is active, and enabling one disables the other two immediately.
/// </summary>
public partial class SetTimePageViewModel : ViewModel
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    private readonly IConfigService _configService;
    private readonly IHardwareService _hardwareService;
    private readonly ILocationService _locationService;
    private readonly ILogger<SetTimePageViewModel> _logger;
    private readonly DispatcherTimer _sensorTimer;

    private UserSettings _settings;

    public SetTimePageViewModel(
        IConfigService configService,
        IHardwareService hardwareService,
        ILocationService locationService,
        ILogger<SetTimePageViewModel> logger)
    {
        _configService = configService;
        _hardwareService = hardwareService;
        _locationService = locationService;
        _logger = logger;

        _settings = _configService.LoadConfig<UserSettings>(PathConstants.UserSettingsPath);

        _sensorTimer = new DispatcherTimer { Interval = PollInterval };
        _sensorTimer.Tick += (_, _) => RefreshSensorState();

        LoadFromSettings();
    }

    // ---- 模式 -----------------------------------------------------------------
    [ObservableProperty] private bool _isCustomTimeMode;
    [ObservableProperty] private bool _isSunriseSunsetMode;
    [ObservableProperty] private bool _isSensorMode;

    [ObservableProperty] private bool _isCustomTimeEnabled = true;

    // ---- 感光 -----------------------------------------------------------------
    [ObservableProperty] private bool _isSensorAvailable;
    [ObservableProperty] private string _ambientLightText = "当前亮度：—";
    [ObservableProperty] private string _sensorStatusText = "正在检测环境光传感器…";
    [ObservableProperty] private double _sensorThreshold = 17;

    // ---- 时间 / 坐标 -----------------------------------------------------------
    [ObservableProperty] private DateTime? _lightTime;
    [ObservableProperty] private DateTime? _darkTime;
    [ObservableProperty] private string _latitudeText = "未获取";
    [ObservableProperty] private string _longitudeText = "未获取";
    [ObservableProperty] private string _sunTimesText = "—";
    [ObservableProperty] private string _locationStatus = string.Empty;

    public void StartMonitoring()
    {
        RefreshSensorState();
        _sensorTimer.Start();
    }

    public void StopMonitoring() => _sensorTimer.Stop();

    public void SelectMode(TimeMode mode)
    {
        if (mode == TimeMode.Sensor && !IsSensorAvailable)
        {
            _logger.LogWarning("未检测到环境光传感器，忽略感光模式切换");
            return;
        }

        _settings.ExecutionMode = mode == TimeMode.Sensor ? ExecutionType.Sensor : ExecutionType.TimeSchedule;
        _settings.UseSunriseSunset = mode == TimeMode.SunriseSunset;
        Persist();

        ApplyMode(mode);
        _logger.LogInformation("时间模式已切换为 {Mode}", mode);

        if (mode == TimeMode.SunriseSunset) _ = RelocateIfNeededAsync();
    }

    [RelayCommand]
    private void SaveTimes()
    {
        _settings.LightConfig ??= new LightConfig();
        _settings.DarkConfig ??= new DarkConfig();

        _settings.LightConfig.Time = (LightTime ?? DateTime.Today.AddHours(6)).ToString("H:mm", CultureInfo.InvariantCulture);
        _settings.DarkConfig.Time = (DarkTime ?? DateTime.Today.AddHours(18)).ToString("H:mm", CultureInfo.InvariantCulture);

        Persist();
        _logger.LogInformation("自定义时间已保存：浅色 {Light}，深色 {Dark}", _settings.LightConfig.Time, _settings.DarkConfig.Time);
    }

    [RelayCommand]
    private async Task RelocateAsync()
    {
        try
        {
            LocationStatus = "正在通过 Windows 位置服务定位…";
            var (latitude, longitude) = await _locationService.GetCurrentLocationAsync();

            _settings.Latitude = latitude.ToString("0.######", CultureInfo.InvariantCulture);
            _settings.Longitude = longitude.ToString("0.######", CultureInfo.InvariantCulture);
            Persist();
            RefreshLocationInfo();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "定位失败");
            LocationStatus = ex.Message;
        }
    }

    // 阈值拖动时即时保存（配置文件监听会驱动调度器重载）
    partial void OnSensorThresholdChanged(double value)
    {
        _settings.AmbientLightSensorThreshold = value;
        Persist();
    }

    private void LoadFromSettings()
    {
        SensorThreshold = _settings.AmbientLightSensorThreshold;

        LightTime = DateTime.Today + ParseTime(_settings.LightConfig?.Time, new TimeSpan(6, 0, 0));
        DarkTime = DateTime.Today + ParseTime(_settings.DarkConfig?.Time, new TimeSpan(18, 0, 0));

        ApplyMode(CurrentMode());
        RefreshSensorState();
        RefreshLocationInfo();
    }

    private TimeMode CurrentMode()
        => _settings.ExecutionMode == ExecutionType.Sensor
            ? TimeMode.Sensor
            : _settings.UseSunriseSunset ? TimeMode.SunriseSunset : TimeMode.CustomTime;

    private void ApplyMode(TimeMode mode)
    {
        IsCustomTimeMode = mode == TimeMode.CustomTime;
        IsSunriseSunsetMode = mode == TimeMode.SunriseSunset;
        IsSensorMode = mode == TimeMode.Sensor;
    }

    private void RefreshSensorState()
    {
        var lux = _hardwareService.GetAmbientLight();
        IsSensorAvailable = lux is not null;

        AmbientLightText = lux is null
            ? "当前亮度：—"
            : $"当前亮度：{lux.Value.ToString("0.0", CultureInfo.InvariantCulture)} lx";

        SensorStatusText = lux is null
            ? "未检测到环境光传感器，感光模式不可用。"
            : "环境光传感器已就绪。";

        // 传感器被移除时把模式退回日出日落，避免停在一个不可用的模式上
        if (!IsSensorAvailable && IsSensorMode) SelectMode(TimeMode.SunriseSunset);
    }

    private void RefreshLocationInfo()
    {
        LatitudeText = string.IsNullOrWhiteSpace(_settings.Latitude) ? "未获取" : _settings.Latitude;
        LongitudeText = string.IsNullOrWhiteSpace(_settings.Longitude) ? "未获取" : _settings.Longitude;

        if (TryGetCoordinates(out var latitude, out var longitude))
        {
            var sun = SunCalculator.TryCalculate(DateTime.Now, latitude, longitude);
            SunTimesText = sun is null
                ? "极昼 / 极夜（当日无日出日落）"
                : $"日出 {sun.Value.Sunrise:HH:mm} · 日落 {sun.Value.Sunset:HH:mm}";
            LocationStatus = "坐标来源：Windows 位置服务";
        }
        else
        {
            SunTimesText = "—";
            LocationStatus = "尚未获取坐标，开启本模式后会自动定位。";
        }
    }

    private async Task RelocateIfNeededAsync()
    {
        if (TryGetCoordinates(out _, out _))
        {
            RefreshLocationInfo();
            return;
        }

        await RelocateAsync();
    }

    private bool TryGetCoordinates(out double latitude, out double longitude)
    {
        latitude = 0;
        longitude = 0;

        return double.TryParse(_settings.Latitude, NumberStyles.Float, CultureInfo.InvariantCulture, out latitude)
            && double.TryParse(_settings.Longitude, NumberStyles.Float, CultureInfo.InvariantCulture, out longitude);
    }

    private void Persist() => _configService.SaveConfig(PathConstants.UserSettingsPath, _settings);

    private static TimeSpan ParseTime(string? value, TimeSpan fallback)
        => TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
}
