using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace OnvifManager.ViewModels;

public partial class PtzViewModel : ConfigEditorViewModel
{
    private static readonly IReadOnlySet<string> Tracked = new HashSet<string>
    {
        nameof(PanSpeed), nameof(TiltSpeed), nameof(ZoomSpeed),
        nameof(Preset1), nameof(Preset2), nameof(Preset3),
        nameof(PatrolEnabled), nameof(PatrolDelay)
    };
    protected override IReadOnlySet<string> TrackedProperties => Tracked;

    [ObservableProperty] private string _capabilities = "Pan, Tilt, Zoom, Focus";
    [ObservableProperty] private double _panSpeed = 70;
    [ObservableProperty] private double _tiltSpeed = 70;
    [ObservableProperty] private double _zoomSpeed = 50;

    [ObservableProperty] private string _preset1 = "Главный вход";
    [ObservableProperty] private string _preset2 = "Парковка";
    [ObservableProperty] private string _preset3 = "Периметр";

    [ObservableProperty] private bool _patrolEnabled;
    [ObservableProperty] private int _patrolDelay = 10;

    [ObservableProperty] private string _statusText = "PTZ (stub) — ONVIF не подключён";

    [RelayCommand]
    private void Apply()
    {
        ResetChanges();
        StatusText = "Применено (stub)";
    }

    [RelayCommand]
    private void Cancel()
    {
        ResetChanges();
        StatusText = "Отменено (stub)";
    }
}
