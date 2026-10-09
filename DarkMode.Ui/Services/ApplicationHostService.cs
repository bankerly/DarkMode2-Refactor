using DarkMode.Ui.Services.Contracts;
using DarkMode.Ui.Views.Pages;
using DarkMode.Ui.Views.Windows;

namespace DarkMode.Ui.Services;

public class ApplicationHostService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;

    public ApplicationHostService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return HandleActivationAsync();
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private Task HandleActivationAsync()
    {
        if (Application.Current.Windows.OfType<MainWindow>().Any())
        {
            return Task.CompletedTask;
        }
        
        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();

        // MainWindow is only a tray-icon host (hidden, opacity 0, no taskbar entry), so
        // launching the app showed nothing at all and looked like it never started.
        // Surface the settings window on start; it is opened without an owner so it is
        // not tied to the hidden main window.
        var settingsWindow = _serviceProvider.GetRequiredService<SettingsWindow>();
        settingsWindow.Show();

        return Task.CompletedTask;
    }

}