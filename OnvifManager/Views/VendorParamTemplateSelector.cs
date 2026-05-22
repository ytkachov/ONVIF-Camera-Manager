using System.Windows;
using System.Windows.Controls;
using OnvifManager.ViewModels;
using OnvifManager.Vendors.Config;

namespace OnvifManager.Views;

public sealed class VendorParamTemplateSelector : DataTemplateSelector
{
    public DataTemplate? BoolTemplate { get; set; }
    public DataTemplate? IntTemplate { get; set; }
    public DataTemplate? EnumTemplate { get; set; }
    public DataTemplate? StringTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object? item, DependencyObject container)
    {
        if (item is not VendorParamViewModel vm) return base.SelectTemplate(item, container);
        return vm.Type switch
        {
            VendorParameterType.Bool => BoolTemplate,
            VendorParameterType.Int => IntTemplate,
            VendorParameterType.Enum => EnumTemplate,
            _ => StringTemplate
        };
    }
}
