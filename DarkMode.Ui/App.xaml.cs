using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using DarkMode.Core.Constants;
using DarkMode.Core.Domains;
using DarkMode.Core.Enums;
using DarkMode.Core.Interfaces.Config;
using DarkMode.Core.Interfaces.Execution;
using DarkMode.Core.Interfaces.Hardware;
using DarkMode.Core.Interfaces.Location;
using DarkMode.Core.Interfaces.Startup;
using DarkMode.Core.Interfaces.Task;
using DarkMode.Core.Interfaces.Theme;
using DarkMode.Core.Models;
using DarkMode.Core.Services.Config;
using DarkMode.Core.Services.Execution;
using DarkMode.Core.Services.Execution.Factory;
using DarkMode.Core.Services.Hardware;
using DarkMode.Core.Services.Location;
using DarkMode.Core.Services.Logging;
using DarkMode.Core.Services.Scheduler;
using DarkMode.Core.Services.Startup;
using DarkMode.Core.Services.Task;
using DarkMode.Core.Services.Theme;
using DarkMode.Ui.Services;
using DarkMode.Ui.ViewModels.Pages;
using DarkMode.Ui.ViewModels.Windows;
using DarkMode.Ui.Views.Pages;
using DarkMode.Ui.Views.Windows;
using Lepo.i18n;
using Lepo.i18n.DependencyInjection;
using Lepo.i18n.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Newtonsoft.Json;
using Serilog;

namespace DarkMode.Ui;

public partial class App
{
    private static readonly IHost _host = Host
        .CreateDefaultBuilder()
        .ConfigureLogging(c =>
        {
            c.ClearProviders();
            var loggerService = LoggerService.CreateLogger();
            c.AddSerilog(loggerService);
        })
        .ConfigureAppConfiguration(c =>
        {
            _ = c.SetBasePath(AppContext.BaseDirectory);
        })
        .ConfigureServices((context, services) =>
        {
            // Hosted services
            _ = services.AddHostedService<ApplicationHostService>();
            _ = services.AddHostedService<SchedulerHostedService>();

            // Windows + view models
            _ = services.AddSingleton<MainWindow>();
            _ = services.AddSingleton<MainWindowViewModel>();
            // Transient on purpose: WPF throws when Show() is called on a closed window.
            _ = services.AddTransient<SettingsWindow>();
            _ = services.AddSingleton<SettingsWindowViewModel>();

            // Pages
            _ = services.AddTransient<SetTimePage>();
            _ = services.AddTransient<SetTimePageViewModel>();
            _ = services.AddTransient<AboutPage>();
            _ = services.AddTransient<AboutPageViewModel>();
            _ = services.AddTransient<SettingsPage>();
            _ = services.AddSingleton<SettingsPageViewModel>();

            // I18n
            _ = services.AddSingleton<LocalizationBuilder>();
            _ = services.AddStringLocalizer(b =>
            {
                _ = b.FromJson(Assembly.GetExecutingAssembly(), "Resources.Translations-zh-CN.json", new CultureInfo("zh-CN"));
                _ = b.FromJson(Assembly.GetExecutingAssembly(), "Resources.Translations-zh-TW.json", new CultureInfo("zh-TW"));
                _ = b.FromJson(Assembly.GetExecutingAssembly(), "Resources.Translations-en-US.json", new CultureInfo("en-US"));

                if (!File.Exists(PathConstants.AppSettingsPath))
                {
                    b.SetCulture(new CultureInfo("zh-CN"));
                }
                else
                {
                    var stored = JsonConvert.DeserializeObject<AppSettings>(File.ReadAllText(PathConstants.AppSettingsPath));
                    b.SetCulture(new CultureInfo(stored?.Language ?? "zh-CN"));
                }
            });

            // Core services
            _ = services.AddSingleton<ConfigService>();
            _ = services.AddSingleton<IConfigService>(provider => provider.GetRequiredService<ConfigService>());
            _ = services.AddSingleton<IHardwareService, HardwareService>();
            _ = services.AddSingleton<ILocationService, LocationService>();
            _ = services.AddSingleton<IStartupService, StartupService>();
            _ = services.AddSingleton<IThemeService, ThemeService>();
            _ = services.AddSingleton<IMonetColorService, MonetColorService>();
            _ = services.AddSingleton<MonetThemeService>();

            // Execution modes + scheduler
            _ = services.AddSingleton<TimeScheduleModeService>();
            _ = services.AddSingleton<SensorModeService>();
            _ = services.AddSingleton<IExecutionModeFactory, ExecutionModeFactory>();
            _ = services.AddSingleton<IConfigMonitorService<UserSettings>, ConfigMonitorService<UserSettings>>();
            _ = services.AddSingleton<SchedulerService>();
            _ = services.AddSingleton<ChangeThemeTask>();
            _ = services.AddSingleton(provider => new ScheduledJob
            {
                Name = "Theme switching",
                Tasks = new List<ITask> { provider.GetRequiredService<ChangeThemeTask>() }
            });
        })
        .Build();

    private void App_OnStartup(object sender, StartupEventArgs e)
    {
        // Single instance. The mutex must stay referenced: a collected mutex releases the
        // name and lets a second copy of the app start (two trays, two theme writers).
        bool createdNew;
        _singleInstanceMutex = new Mutex(true, "DarkMode.Ui", out createdNew);
        if (!createdNew)
        {
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
            Environment.Exit(0);
        }

        // UI colours: Material Design theme whose palette is extracted from the wallpaper (Monet).
        ApplyMonetTheme();

        _host.Start();

        var logger = _host.Services.GetRequiredService<ILogger<Application>>();
        logger.LogInformation("DarkMode starting...");

        var config = _host.Services.GetRequiredService<IConfigService>();
        config.EnsureValidConfig(PathConstants.AppSettingsPath, new AppSettings());
        config.EnsureValidConfig(PathConstants.UserSettingsPath, new UserSettings());

        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    private void App_OnExit(object sender, ExitEventArgs e)
    {
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;

        ILogger<Application>? logger = null;
        try
        {
            logger = _host.Services.GetRequiredService<ILogger<Application>>();
            _host.StopAsync().Wait();
            logger.LogInformation("DarkMode exiting...");
        }
        catch (Exception)
        {
            // never block shutdown
        }
        finally
        {
            _host.Dispose();
        }
    }

    private static Mutex? _singleInstanceMutex;
    private static bool _exceptionDialogShown;

    private void App_OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;

        try
        {
            _host.Services.GetRequiredService<ILogger<Application>>()
                .LogError(e.Exception, "Unhandled exception");
        }
        catch (Exception)
        {
            // the host may not exist yet (e.g. failure inside the static constructor)
        }

        // layout errors can fire on every frame: one dialog is enough
        if (_exceptionDialogShown) return;
        _exceptionDialogShown = true;

        MessageBox.Show(
            e.Exception.Message,
            "DarkMode",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        // Wallpaper or system theme changed: re-extract the Monet palette.
        if (e.Category is UserPreferenceCategory.Desktop or UserPreferenceCategory.General or UserPreferenceCategory.Color)
        {
            Dispatcher.Invoke(ApplyMonetTheme);
        }
    }

    private void ApplyMonetTheme()
    {
        var dark = _host.Services.GetRequiredService<IThemeService>().GetCurrentTheme() == SystemTheme.Dark;
        _host.Services.GetRequiredService<MonetThemeService>().Apply(dark);
    }
}
