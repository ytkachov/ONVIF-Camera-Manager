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
        _vm.LastProbeError = "";
    }

    private async void Add_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.IsProbing) return;
        var ok = await _vm.TryAddManualAsync();
        if (ok)
        {
            DialogResult = true;
            Close();
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
