using System.IO;
using DarkMode.Core.Interfaces.Startup;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Win32;

namespace DarkMode.Core.Services.Startup;

/// <summary>
/// Autostart lives in HKCU\...\CurrentVersion\Run, so enabling it needs no elevation.
/// </summary>
public class StartupService : IStartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "DarkMode2";

    private readonly ILogger<StartupService> _logger;

    public StartupService(ILogger<StartupService>? logger = null)
    {
        _logger = logger ?? NullLogger<StartupService>.Instance;
    }

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
                var exe = ResolveExecutablePath();
                key.SetValue(ValueName, $"\"{exe}\"", RegistryValueKind.String);
                _logger.LogInformation("开机自启动已启用：{Exe}", exe);
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

    private static string ResolveExecutablePath()
    {
        // the apphost next to the assemblies is the real entry point;
        // Environment.ProcessPath would be dotnet.exe when run as "dotnet app.dll"
        var appHost = Path.Combine(AppContext.BaseDirectory, "DarkMode.Ui.exe");
        return File.Exists(appHost) ? appHost : Environment.ProcessPath ?? appHost;
    }
}
