using System.Collections.ObjectModel;
using System.Globalization;
using DarkMode.Core.Constants;
using DarkMode.Core.Interfaces.Config;
using DarkMode.Core.Interfaces.Startup;
using DarkMode.Core.Models;
using DarkMode.Ui.Resources;
using Lepo.i18n;
using Microsoft.Extensions.Logging;

namespace DarkMode.Ui.ViewModels.Pages;

public partial class SettingsPageViewModel : ViewModel
{
    private readonly IConfigService _configService;
    private readonly ILogger<SettingsPageViewModel> _logger;
    private readonly ILocalizationCultureManager _cultureManager;
    private readonly IStartupService _startupService;

    private bool _isInitializing;
    private bool _revertingAutoStart;

    public SettingsPageViewModel(
        IConfigService configService,
        ILogger<SettingsPageViewModel> logger,
        ILocalizationCultureManager cultureManager,
        IStartupService startupService)
    {
        _configService = configService;
        _logger = logger;
        _cultureManager = cultureManager;
        _startupService = startupService;

        var config = _configService.LoadConfig<AppSettings>(PathConstants.AppSettingsPath);

        SelectedLanguage = SupportedLanguages.FirstOrDefault(l => l.LanguageCode == config.Language) ?? SupportedLanguages[0];
        IsAutoStartEnabled = _startupService.IsEnabled();

        _isInitializing = true;
    }

    [ObservableProperty] private SupportedLanguage _selectedLanguage = new();
    [ObservableProperty] private bool _isAutoStartEnabled;

    public ObservableCollection<SupportedLanguage> SupportedLanguages { get; } =
    [
        new() { LanguageName = "简体中文 (中国)", LanguageCode = "zh-CN" },
        new() { LanguageName = "繁體中文 (中國台灣)", LanguageCode = "zh-TW" },
        new() { LanguageName = "English (United States)", LanguageCode = "en-US" }
    ];

    partial void OnSelectedLanguageChanged(SupportedLanguage value)
    {
        if (!_isInitializing || value is null) return;

        _logger.LogInformation("Change Language: {Language}", value.LanguageCode);
        _cultureManager.SetCulture(new CultureInfo(value.LanguageCode));
        _configService.UpdateConfig<AppSettings>(PathConstants.AppSettingsPath, settings => settings.Language = value.LanguageCode);
    }

    // 以注册表为准：写入失败就把界面上的开关回滚，避免显示与真实状态不一致
    partial void OnIsAutoStartEnabledChanged(bool value)
    {
        if (_isInitializing || _revertingAutoStart) return;

        try
        {
            _startupService.SetEnabled(value);
            _configService.UpdateConfig<AppSettings>(PathConstants.AppSettingsPath, settings => settings.AutoStart = value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "设置开机自启动失败");

            _revertingAutoStart = true;
            IsAutoStartEnabled = _startupService.IsEnabled();
            _revertingAutoStart = false;
        }
    }
}
