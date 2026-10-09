using System.Runtime.InteropServices;
using DarkMode.Core.Enums;
using DarkMode.Core.Interfaces.Theme;
using Microsoft.Win32;

namespace DarkMode.Core.Services.Theme;

/// <summary>
/// Applies the Windows light/dark mode of the current user.
/// The theme lives in HKCU\...\Themes\Personalize, so no elevation is required.
/// </summary>
public class ThemeService : IThemeService
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string AppsUseLightTheme = "AppsUseLightTheme";
    private const string SystemUsesLightTheme = "SystemUsesLightTheme";

    private const uint WmSettingChange = 0x001A;
    private const uint SmtoAbortIfHung = 0x0002;
    private static readonly IntPtr HwndBroadcast = new(0xffff);

    public SystemTheme GetCurrentTheme()
    {
        using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey, false);
        var value = key?.GetValue(AppsUseLightTheme);
        // 1 = light, 0 = dark. Missing value means Windows default (light).
        return value is int light && light == 0 ? SystemTheme.Dark : SystemTheme.Light;
    }

    public void ApplyTheme(SystemTheme theme)
    {
        var lightValue = theme == SystemTheme.Light ? 1 : 0;

        using (var key = Registry.CurrentUser.CreateSubKey(PersonalizeKey, true))
        {
            key.SetValue(AppsUseLightTheme, lightValue, RegistryValueKind.DWord);
            key.SetValue(SystemUsesLightTheme, lightValue, RegistryValueKind.DWord);
        }

        NotifyShell();
    }

    // Explorer and running apps only re-read the theme when the shell is told about it.
    private static void NotifyShell()
    {
        _ = SendMessageTimeout(HwndBroadcast, WmSettingChange, IntPtr.Zero, "ImmersiveColorSet",
            SmtoAbortIfHung, 1000, out _);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam, string lParam,
        uint flags, uint timeout, out IntPtr result);
}
