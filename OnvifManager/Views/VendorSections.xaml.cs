using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using OnvifManager.ViewModels;

namespace OnvifManager.Views;

public partial class VendorSections : UserControl
{
    public static readonly DependencyProperty TabKeyProperty =
        DependencyProperty.Register(nameof(TabKey), typeof(string), typeof(VendorSections),
            new PropertyMetadata("", OnTabKeyChanged));

    public string TabKey
    {
        get => (string)GetValue(TabKeyProperty);
        set => SetValue(TabKeyProperty, value);
    }

    public VendorSections()
    {
        InitializeComponent();
        Loaded += (_, _) => Rebuild();
        DataContextChanged += (_, _) => Rebuild();
    }

    private static void OnTabKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((VendorSections)d).Rebuild();

    private void Rebuild()
    {
        if (DataContext is not VendorParametersHostViewModel host)
        {
            SectionsHost.ItemsSource = null;
            return;
        }

        var view = new CollectionViewSource { Source = host.Sections }.View;
        var tab = TabKey;
        view.Filter = o => o is VendorSectionViewModel s &&
                           string.Equals(s.Tab, tab, StringComparison.OrdinalIgnoreCase);
        SectionsHost.ItemsSource = view;
    }
}
