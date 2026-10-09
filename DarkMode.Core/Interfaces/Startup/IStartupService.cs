namespace DarkMode.Core.Interfaces.Startup;

/// <summary>Registers the app in the current user's autostart list.</summary>
public interface IStartupService
{
    /// <summary>Command line used by the autostart entry (exe + the autostart switch).</summary>
    string BuildCommand();

    bool IsEnabled();

    void SetEnabled(bool enabled);

    /// <summary>Rewrites an existing entry into the current format (adds the autostart switch).</summary>
    bool Normalize();
}
