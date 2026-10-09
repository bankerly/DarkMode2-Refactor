using DarkMode.Core.Enums;
using DarkMode.Core.Interfaces.Execution;
using DarkMode.Core.Interfaces.Task;
using DarkMode.Core.Interfaces.Theme;
using DarkMode.Core.Services.Execution;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DarkMode.Core.Services.Task;

/// <summary>
/// Switches the Windows light/dark mode according to the active execution mode.
/// Idempotent: it never touches the registry when the theme already matches.
/// </summary>
public class ChangeThemeTask : TaskBase
{
    private readonly IExecutionModeFactory _modeFactory;
    private readonly IThemeService _themeService;
    private readonly ILogger<ChangeThemeTask> _logger;

    public ChangeThemeTask(
        IExecutionModeFactory modeFactory,
        IThemeService themeService,
        ILogger<ChangeThemeTask>? logger = null)
    {
        _modeFactory = modeFactory;
        _themeService = themeService;
        _logger = logger ?? NullLogger<ChangeThemeTask>.Instance;
    }

    public override string Name => "SwitchTheme";

    protected override void ExecuteTask()
    {
        var mode = _modeFactory.GetExecutionMode();
        if (mode is not TimeScheduleModeService timeMode)
        {
            _logger.LogDebug("Execution mode {Mode} has no theme decision yet; nothing to do", mode.Name);
            return;
        }

        var wanted = timeMode.IsDarkPeriod() ? SystemTheme.Dark : SystemTheme.Light;
        var current = _themeService.GetCurrentTheme();
        if (current == wanted) return;

        _themeService.ApplyTheme(wanted);
        _logger.LogInformation("System theme switched: {From} -> {To}", current, wanted);
    }
}
