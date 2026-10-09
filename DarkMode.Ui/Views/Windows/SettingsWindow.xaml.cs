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

    public SettingsWindow(SettingsWindowViewModel viewModel, IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        ViewModel = viewModel;
        DataContext = viewModel;

        InitializeComponent();
        Loaded += OnLoaded;
    }

    public SettingsWindowViewModel ViewModel { get; }

    // DWM needs a real HWND before the backdrop can be attached.
    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var monet = _serviceProvider.GetRequiredService<MonetThemeService>();
        var tint = monet.SurfaceColor;

        var backdropApplied = WindowBackdrop.Apply(this, monet.IsDark, tint);

        // 半透明表面色叠在模糊之上 = 混色；不支持模糊的系统上保持不透明，避免字看不清
        Background = new SolidColorBrush(backdropApplied
            ? Color.FromArgb(0x99, tint.R, tint.G, tint.B)
            : Color.FromArgb(0xFF, tint.R, tint.G, tint.B));
    }

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
