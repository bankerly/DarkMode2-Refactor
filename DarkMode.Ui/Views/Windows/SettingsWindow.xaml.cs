using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DarkMode.Ui.Models;
using DarkMode.Ui.Services;
using DarkMode.Ui.ViewModels.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace DarkMode.Ui.Views.Windows;

public partial class SettingsWindow : Window
{
    private readonly IServiceProvider _serviceProvider;
    private readonly MonetThemeService _monetThemeService;

    public SettingsWindow(SettingsWindowViewModel viewModel, IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _monetThemeService = serviceProvider.GetRequiredService<MonetThemeService>();
        ViewModel = viewModel;
        DataContext = viewModel;

        InitializeComponent();
        Loaded += OnLoaded;

        // 主题随时可能因日出日落而切换：窗口底色要跟着走，否则会出现"深导航栏 + 浅内容"
        _monetThemeService.ThemeApplied += OnThemeApplied;
        Closed += (_, _) => _monetThemeService.ThemeApplied -= OnThemeApplied;
    }

    public SettingsWindowViewModel ViewModel { get; }

    // DWM needs a real HWND before the backdrop can be attached.
    private void OnSourceInitialized(object? sender, EventArgs e) => ApplyBackdrop();

    private void OnThemeApplied(Color surface, bool dark)
    {
        // DWM 的亚克力着色不会自己跟着我们的主题走，重挂一次
        _backdropApplied = WindowBackdrop.Apply(this, dark, surface);
        ApplyTint(surface);
    }

    private bool _backdropApplied;

    private void ApplyBackdrop()
    {
        _backdropApplied = WindowBackdrop.Apply(this, _monetThemeService.IsDark, _monetThemeService.SurfaceColor);
        ApplyTint(_monetThemeService.SurfaceColor);
    }

    // 半透明表面色叠在模糊之上 = 混色；不支持模糊的系统上保持不透明，避免字看不清
    private void ApplyTint(Color tint) => Background = new SolidColorBrush(_backdropApplied
        ? Color.FromArgb(0x99, tint.R, tint.G, tint.B)
        : Color.FromArgb(0xFF, tint.R, tint.G, tint.B));

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (NavList.SelectedItem is null && ViewModel.NavItems.Count > 0) NavList.SelectedIndex = 0;
    }

    private void NavList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // pages come from the container so they keep their injected services;
        // a Page must be hosted by a Window or Frame, hence the Frame host
        if (NavList.SelectedItem is NavItem item) PageHost.Content = _serviceProvider.GetRequiredService(item.PageType);
    }
}
