using System.Windows;
using OnvifManager.ViewModels;

namespace OnvifManager.Views;

public partial class ManualAddDialog : Window
{
    private readonly DiscoveryViewModel _vm;

    public ManualAddDialog(DiscoveryViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
    }

    private async void Add_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_vm.ManualIp)) return;
        await _vm.AddManualCommand.ExecuteAsync(null);
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
