using System.Windows;
using DarkMode.Ui.Views.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DarkMode.Ui.ViewModels.Windows;

public partial class MainWindowViewModel(IServiceProvider serviceProvider, ILogger<MainWindowViewModel> logger)
{
    private SettingsWindow? _settingsWindow;

    [RelayCommand]
    public void OpenSettingsWindow()
    {
        if (_settingsWindow is { IsVisible: true })
        {
            _settingsWindow.Activate();
            return;
        }

        // transient on purpose: WPF forbids Show() on a closed window
        _settingsWindow = serviceProvider.GetRequiredService<SettingsWindow>();
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
        _settingsWindow.Activate();

        logger.LogInformation("Settings window opened");
    }

    [RelayCommand]
    public void ExitProgram() => Application.Current.Shutdown();
}
