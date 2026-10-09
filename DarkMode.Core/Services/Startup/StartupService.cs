using System.IO;
using DarkMode.Core.Interfaces.Startup;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Win32;

namespace DarkMode.Core.Services.Startup;

/// <summary>
/// Autostart lives in HKCU\\...\\CurrentVersion\\Run, so enabling it needs no elevation.
/// The registered command carries <see cref="AutostartArgument"/> so the app can tell a
/// logon launch (tray only) from a manual one (show the window).
/// </summary>
public class StartupService : IStartupService
{
    public const string AutostartArgument = "--autostart";

    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "DarkMode2";

    private readonly ILogger<StartupService> _logger;

    public StartupService(ILogger<StartupService>? logger = null)
    {
        _logger = logger ?? NullLogger<StartupService>.Instance;
    }

    public string BuildCommand() => $"\"{ResolveExecutablePath()}\" {AutostartArgument}";

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
        return key?.GetValue(ValueName) is string value && value.Length > 0;
    }

    public void SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, true);

            if (enabled)
            {
                var command = BuildCommand();
                key.SetValue(ValueName, command, RegistryValueKind.String);
                _logger.LogInformation("开机自启动已启用：{Command}", command);
            }
            else
            {
                key.DeleteValue(ValueName, false);
                _logger.LogInformation("开机自启动已关闭");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "修改开机自启动失败");
            throw;
        }
    }

    public bool Normalize()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
            if (key?.GetValue(ValueName) is not string current || current.Length == 0) return false;

            var expected = BuildCommand();
            if (string.Equals(current.Trim(), expected, StringComparison.OrdinalIgnoreCase)) return false;

            key.SetValue(ValueName, expected, RegistryValueKind.String);
            _logger.LogInformation("开机自启动项已升级为新格式：{Command}", expected);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "升级开机自启动项失败");
            return false;
        }
    }

    private static string ResolveExecutablePath()
    {
        // the apphost next to the assemblies is the real entry point;
        // Environment.ProcessPath would be dotnet.exe when run as "dotnet app.dll"
        var appHost = Path.Combine(AppContext.BaseDirectory, "DarkMode.Ui.exe");
        return File.Exists(appHost) ? appHost : Environment.ProcessPath ?? appHost;
    }
}
