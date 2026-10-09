using System.Windows.Controls;
using DarkMode.Ui.ViewModels.Pages;

namespace DarkMode.Ui.Views.Pages;

public partial class SettingsPage : Page
{
    public SettingsPage(SettingsPageViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
    }

    public SettingsPageViewModel ViewModel { get; }
}
