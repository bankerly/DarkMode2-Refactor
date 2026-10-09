using DarkMode.Core.Constants;
using DarkMode.Core.Domains;
using DarkMode.Core.Interfaces.Config;
using DarkMode.Core.Interfaces.Execution;
using DarkMode.Core.Interfaces.Location;
using DarkMode.Core.Interfaces.Task;
using DarkMode.Core.Interfaces.Theme;
using DarkMode.Core.Interfaces.Hardware;
using DarkMode.Core.Models;
using DarkMode.Core.Services.Config;
using DarkMode.Core.Services.Execution;
using DarkMode.Core.Services.Execution.Factory;
using DarkMode.Core.Services.Hardware;
using DarkMode.Core.Services.Location;
using DarkMode.Core.Services.Scheduler;
using DarkMode.Core.Services.Task;
using DarkMode.Core.Services.Theme;
using DarkMode.Core.Services.Logging;
using DarkMode.Ui.Services;
using DarkMode.Ui.Services.Contracts;
using DarkMode.Ui.ViewModels.Pages;
using DarkMode.Ui.ViewModels.Windows;
using DarkMode.Ui.Views.Pages;
using DarkMode.Ui.Views.Windows;
using Lepo.i18n;
using Lepo.i18n.DependencyInjection;
using Lepo.i18n.Json;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Serilog;
using Wpf.Ui;
using Wpf.Ui.DependencyInjection;
using Wpf.Ui.Extensions;

namespace DarkMode.Ui;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App
{
    private static readonly IHost _host = Host
        .CreateDefaultBuilder()
        .ConfigureLogging(c =>
        {
            c.ClearProviders();
            // Logging
            var loggerService = LoggerService.CreateLogger();
            c.AddSerilog(loggerService);
        })
        .ConfigureAppConfiguration(c =>
        {
            _ = c.SetBasePath(AppContext.BaseDirectory);
        }).ConfigureServices((context, services) =>
        {
            _ = services.AddNavigationViewPageProvider();
            
            // App Host
            _ = services.AddHostedService<ApplicationHostService>();
            
            // Window container with navigation
            _ = services.AddSingleton<IWindow, SettingsWindow>();
            _ = services.AddSingleton<INavigationService, NavigationService>();
            _ = services.AddSingleton<ISnackbarService, SnackbarService>();
            _ = services.AddSingleton<IContentDialogService, ContentDialogService>();
            _ = services.AddSingleton<WindowsProviderService>();
            
            // Window
            _ = services.AddSingleton<MainWindow>();
            _ = services.AddSingleton<MainWindowViewModel>();
            // Transient on purpose: WPF throws when Show() is called on a closed window,
            // and the window is re-opened from the tray menu every time.
            _ = services.AddTransient<SettingsWindow>();
            _ = services.AddSingleton<SettingsWindowViewModel>();
            
            // Pages
            _ = services.AddTransient<SetTimePage>();
            _ = services.AddTransient<SetTimePageViewModel>();
            _ = services.AddTransient<SetWallpaperPage>();
            _ = services.AddTransient<SetWallpaperPageViewModel>();
            _ = services.AddTransient<CustomPage>();
            _ = services.AddTransient<CustomPageViewModel>();
            _ = services.AddTransient<LaboratoryPage>();
            _ = services.AddTransient<LaboratoryPageViewModel>();
            _ = services.AddTransient<DeveloperModePage>();
            _ = services.AddTransient<DeveloperModePageViewModel>();
            _ = services.AddTransient<AboutPage>();
            _ = services.AddTransient<AboutPageViewModel>();
            _ = services.AddTransient<SettingsPage>();
            _ = services.AddTransient<SettingsPageViewModel>();
            
            // I18n
            _ = services.AddSingleton<LocalizationBuilder>();
            _ = services.AddStringLocalizer(b =>
            {
                _ = b.FromJson(Assembly.GetExecutingAssembly(), "Resources.Translations-zh-CN.json", new CultureInfo("zh-CN"));
                _ = b.FromJson(Assembly.GetExecutingAssembly(), "Resources.Translations-zh-TW.json", new CultureInfo("zh-TW"));
                _ = b.FromJson(Assembly.GetExecutingAssembly(), "Resources.Translations-en-US.json", new CultureInfo("en-US"));
                if (!File.Exists(PathConstants.AppSettingsPath))
                {
                    b.SetCulture(new CultureInfo( "en-US")); // Default English
                }
                else
                {
                    var stored = JsonConvert.DeserializeObject<AppSettings>(File.ReadAllText(PathConstants.AppSettingsPath));
                    b.SetCulture(new CultureInfo(stored?.Language ?? "zh-CN"));
                }
                
            });
            
            // Other
            _ = services.AddSingleton<IHardwareService, HardwareService>();
            _ = services.AddSingleton<ConfigService>();
            _ = services.AddSingleton<IConfigService>(provider => provider.GetRequiredService<ConfigService>());
            _ = services.AddSingleton<ILocationService, LocationService>();
            // fully qualified: Wpf.Ui exposes same-named types
            _ = services.AddSingleton<DarkMode.Core.Interfaces.Theme.IThemeService, DarkMode.Core.Services.Theme.ThemeService>();

            // Execution modes + scheduler: without these the schedule never ran at all.
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
            _ = services.AddHostedService<SchedulerHostedService>();
        }).Build();
    private void App_OnStartup(object sender, StartupEventArgs e)
    {
        // Single instance
        bool createdNew;
        _ = new Mutex(true, "DarkMode.Ui", out createdNew);
        if (!createdNew) Environment.Exit(0);
        
        // Start application
        _host.Start();
        
        var logger = _host.Services.GetRequiredService<ILogger<Application>>();
        logger.LogInformation("DarkMode starting...");
        
        // Init App
        var config = _host.Services.GetRequiredService<IConfigService>();
        config.EnsureValidConfig(PathConstants.AppSettingsPath, new AppSettings());
        config.EnsureValidConfig(PathConstants.UserSettingsPath, new UserSettings());
    }

    private void App_OnExit(object sender, ExitEventArgs e)
    {
        var logger = _host.Services.GetRequiredService<ILogger<Application>>();

        _host.StopAsync().Wait();
        logger.LogInformation("DarkMode exiting...");
        
        _host.Dispose();
    }

    private void App_OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        var logger = _host.Services.GetRequiredService<ILogger<Application>>();
        logger.LogError("Unhandled exception: {Message}", e.Exception.Message);
        
        var dialog = _host.Services.GetRequiredService<IContentDialogService>();
        dialog.ShowSimpleDialogAsync(
            new SimpleContentDialogCreateOptions()
            {
                Title = "Unhandled exception",
                Content = e.Exception,
                PrimaryButtonText = "Feedback",
                CloseButtonText = "Cancel"
            }
        );
        e.Handled = true;
    }
}