using DarkMode.Core.Enums;

namespace DarkMode.Core.Interfaces.Theme;

/// <summary>
/// Reads and applies the Windows app/system light-dark mode.
/// </summary>
public interface IThemeService
{
    /// <summary>Current system theme (apps + system setting of the current user).</summary>
    SystemTheme GetCurrentTheme();

    /// <summary>Applies the theme to the current user and notifies the shell.</summary>
    void ApplyTheme(SystemTheme theme);
}
