using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using OnvifManager.Vendors.Config;

namespace OnvifManager.ViewModels;

// Wraps one VendorParameterValue and exposes typed, bindable accessors plus the control
// hints the dynamic UI uses to pick a template. All edits flow back into RawValue, so the
// service's dirty tracking sees them.
public partial class VendorParamViewModel : ObservableObject
{
    private readonly VendorParameterValue _value;

    public VendorParamViewModel(VendorParameterValue value)
    {
        _value = value;
        if (value.Descriptor.Type == VendorParameterType.Enum && value.Descriptor.Options != null)
            Options = new ObservableCollection<VendorEnumOption>(value.Descriptor.Options);
    }

    public VendorParameterValue Model => _value;
    public VendorParameterDescriptor Descriptor => _value.Descriptor;
    public string Label => _value.Descriptor.Label;
    public VendorParameterType Type => _value.Descriptor.Type;

    public int Min => _value.Descriptor.Min;
    public int Max => _value.Descriptor.Max;
    public int Step => _value.Descriptor.Step;

    public ObservableCollection<VendorEnumOption>? Options { get; }

    // Raised after any edit so the host can recompute its aggregate dirty state.
    public event Action? Changed;

    public bool BoolValue
    {
        get => string.Equals(_value.RawValue, _value.Descriptor.TrueValue, StringComparison.OrdinalIgnoreCase);
        set
        {
            _value.RawValue = value ? _value.Descriptor.TrueValue : _value.Descriptor.FalseValue;
            OnValueChanged();
        }
    }

    public int IntValue
    {
        get => int.TryParse(_value.RawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0;
        set
        {
            _value.RawValue = value.ToString(CultureInfo.InvariantCulture);
            OnValueChanged();
        }
    }

    public string StringValue
    {
        get => _value.RawValue ?? "";
        set
        {
            _value.RawValue = value;
            OnValueChanged();
        }
    }

    public VendorEnumOption? SelectedOption
    {
        get => Options?.FirstOrDefault(o =>
            string.Equals(o.Value, _value.RawValue, StringComparison.OrdinalIgnoreCase));
        set
        {
            if (value == null) return;
            _value.RawValue = value.Value;
            OnValueChanged();
        }
    }

    // Human-readable current value (enum label rather than raw token, on/off for bool).
    public string DisplayValue => Type switch
    {
        VendorParameterType.Bool => BoolValue ? "Вкл" : "Выкл",
        VendorParameterType.Enum => SelectedOption?.Label ?? _value.RawValue ?? "—",
        _ => string.IsNullOrEmpty(_value.RawValue) ? "—" : _value.RawValue
    };

    public string Tooltip
    {
        get
        {
            var lines = new List<string> { Label };
            if (!string.IsNullOrWhiteSpace(Descriptor.Description))
                lines.Add(Descriptor.Description!);
            lines.Add($"Текущее значение: {DisplayValue}");
            if (Type == VendorParameterType.Int)
                lines.Add($"Диапазон: {Min}–{Max}");
            return string.Join(Environment.NewLine, lines);
        }
    }

    private void OnValueChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        OnPropertyChanged(name);
        OnPropertyChanged(nameof(DisplayValue));
        OnPropertyChanged(nameof(Tooltip));
        Changed?.Invoke();
    }
}
