using MaterialDesignThemes.Wpf;

namespace DarkMode.Ui.Models;

/// <summary>One entry of the settings window navigation rail.</summary>
public sealed record NavItem(string Title, PackIconKind Icon, Type PageType);
