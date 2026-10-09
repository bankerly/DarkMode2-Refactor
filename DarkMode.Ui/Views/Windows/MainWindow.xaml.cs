using System.Windows;
using DarkMode.Ui.ViewModels.Windows;

namespace DarkMode.Ui.Views.Windows;

public partial class MainWindow
{
    private readonly MainWindowViewModel _viewModel;

    public MainWindow(MainWindowViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
    }

    private void OnTrayLeftClick(object sender, RoutedEventArgs e) => _viewModel.OpenSettingsWindow();

    private void OnOpenSettings(object sender, RoutedEventArgs e) => _viewModel.OpenSettingsWindow();

    private void OnExit(object sender, RoutedEventArgs e) => _viewModel.ExitProgram();
}
