using System.Windows;
using OnvifManager.ViewModels;
using OnvifManager.Views;

namespace OnvifManager;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    public MainWindow(MainViewModel mainViewModel)
    {
        InitializeComponent();
        _vm = mainViewModel;
        DataContext = mainViewModel;
        mainViewModel.Discovery.AddManualRequested += OnAddManualRequested;
        Closed += (_, _) => mainViewModel.Discovery.AddManualRequested -= OnAddManualRequested;
    }

    private void OnAddManualRequested()
    {
        var dlg = new ManualAddDialog(_vm.Discovery) { Owner = this };
        dlg.ShowDialog();
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => Close();
}
