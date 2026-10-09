namespace DarkMode.Core.Interfaces.Startup;

/// <summary>Registers the app in the current user's autostart list.</summary>
public interface IStartupService
{
    bool IsEnabled();

    void SetEnabled(bool enabled);
}
