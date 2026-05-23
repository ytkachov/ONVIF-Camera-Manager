using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using OnvifManager.Models;
using OnvifManager.Services;
using OnvifManager.Vendors.Config;

namespace OnvifManager.ViewModels;

// Loads the config-driven vendor parameters for the selected camera (only in Full mode)
// and groups them into sections keyed by target tab. Each tab View binds to its own slice
// via SectionsFor(tab). HasChanges feeds the shared "Применить" button; SaveAsync writes
// just the dirty values back through VendorParameterService. Vendor (ISAPI) traffic uses
// the camera's admin credentials when present — see OnvifClientProvider.GetVendor.
public partial class VendorParametersHostViewModel : ObservableObject
{
    private readonly OnvifClientProvider _provider;
    private readonly VendorParameterService _service;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private bool _hasChanges;

    public ObservableCollection<VendorSectionViewModel> Sections { get; } = new();

    public VendorParametersHostViewModel(OnvifClientProvider provider, VendorParameterService service)
    {
        _provider = provider;
        _service = service;
    }

    public bool HasProfile(CameraDevice? camera) => camera != null && _service.HasProfile(camera);

    public void Clear()
    {
        Sections.Clear();
        HasChanges = false;
    }

    public async Task LoadAsync(CameraDevice? camera, CancellationToken ct = default)
    {
        Sections.Clear();
        HasChanges = false;
        if (camera == null || !_service.HasProfile(camera)) return;

        IsLoading = true;
        StatusText = "Загрузка vendor-параметров…";
        try
        {
            var client = _provider.GetVendor(camera);
            var values = await _service.ReadAllAsync(client, ct);

            foreach (var byTab in values
                         .Where(v => v.Available)
                         .GroupBy(v => v.Descriptor.Tab))
            {
                foreach (var bySection in byTab.GroupBy(v => v.Descriptor.Section))
                {
                    var section = new VendorSectionViewModel(byTab.Key, bySection.Key);
                    foreach (var v in bySection)
                    {
                        var pvm = new VendorParamViewModel(v);
                        pvm.Changed += RecomputeHasChanges;
                        section.Parameters.Add(pvm);
                    }
                    Sections.Add(section);
                }
            }

            StatusText = Sections.Count == 0
                ? "Vendor-параметры недоступны на этой камере"
                : "Vendor-параметры загружены";
        }
        catch (OperationCanceledException) { StatusText = "Отменено"; }
        catch (Exception ex) { StatusText = $"Ошибка: {ex.Message}"; }
        finally { IsLoading = false; }
    }

    public IEnumerable<VendorSectionViewModel> SectionsFor(string tab) =>
        Sections.Where(s => string.Equals(s.Tab, tab, StringComparison.OrdinalIgnoreCase));

    private void RecomputeHasChanges() =>
        HasChanges = Sections.SelectMany(s => s.Parameters).Any(p => p.Model.IsDirty);

    public async Task<int> SaveAsync(CameraDevice? camera, CancellationToken ct = default)
    {
        if (camera == null) return 0;
        var values = Sections.SelectMany(s => s.Parameters).Select(p => p.Model).ToList();
        var client = _provider.GetVendor(camera);
        var written = await _service.WriteAsync(client, values, ct);
        RecomputeHasChanges();
        return written;
    }
}

public sealed class VendorSectionViewModel
{
    public string Tab { get; }
    public string Title { get; }
    public ObservableCollection<VendorParamViewModel> Parameters { get; } = new();

    public VendorSectionViewModel(string tab, string title)
    {
        Tab = tab;
        Title = title;
    }
}
