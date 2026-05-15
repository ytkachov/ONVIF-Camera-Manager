using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace OnvifManager.Views.Controls;

public partial class IpAddressBox : UserControl
{
    private bool _suppressUpdate;
    private TextBox[] _octets = null!;

    public IpAddressBox()
    {
        InitializeComponent();
        _octets = new[] { Oct1, Oct2, Oct3, Oct4 };
    }

    public static readonly DependencyProperty AddressProperty =
        DependencyProperty.Register(
            nameof(Address), typeof(string), typeof(IpAddressBox),
            new FrameworkPropertyMetadata(string.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnAddressChanged));

    public string Address
    {
        get => (string)GetValue(AddressProperty);
        set => SetValue(AddressProperty, value);
    }

    private static void OnAddressChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not IpAddressBox box) return;
        if (box._suppressUpdate) return;
        var parts = ((string?)e.NewValue ?? "").Split('.');
        box._suppressUpdate = true;
        for (var i = 0; i < 4; i++)
        {
            var p = i < parts.Length ? parts[i] : "";
            if (p.Length > 3) p = p[..3];
            box._octets[i].Text = p;
        }
        box._suppressUpdate = false;
    }

    private void Oct_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressUpdate) return;
        _suppressUpdate = true;
        try
        {
            if (sender is TextBox tb && tb.Text.Length == 3)
            {
                var idx = Array.IndexOf(_octets, tb);
                if (idx >= 0 && idx < 3)
                    _octets[idx + 1].Focus();
            }
            Address = $"{Oct1.Text}.{Oct2.Text}.{Oct3.Text}.{Oct4.Text}";
        }
        finally { _suppressUpdate = false; }
    }

    private void Oct_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (!Regex.IsMatch(e.Text, @"^\d$"))
        {
            e.Handled = true;
            return;
        }
        if (sender is TextBox tb)
        {
            var prospective = (tb.Text ?? "") + e.Text;
            if (int.TryParse(prospective, out var n) && n > 255)
                e.Handled = true;
        }
    }

    private void Oct_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox tb) return;
        var idx = Array.IndexOf(_octets, tb);

        if (e.Key == Key.OemPeriod || e.Key == Key.Decimal)
        {
            if (idx >= 0 && idx < 3) _octets[idx + 1].Focus();
            e.Handled = true;
        }
        else if (e.Key == Key.Back && tb.SelectionStart == 0 && tb.SelectionLength == 0)
        {
            if (idx > 0) _octets[idx - 1].Focus();
        }
        else if (e.Key == Key.Left && tb.CaretIndex == 0 && idx > 0)
        {
            _octets[idx - 1].Focus();
            _octets[idx - 1].CaretIndex = _octets[idx - 1].Text.Length;
            e.Handled = true;
        }
        else if (e.Key == Key.Right && tb.CaretIndex == tb.Text.Length && idx < 3)
        {
            _octets[idx + 1].Focus();
            _octets[idx + 1].CaretIndex = 0;
            e.Handled = true;
        }
    }
}
