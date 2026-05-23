using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace OnvifManager.ViewModels;

public partial class EventsViewModel : ConfigEditorViewModel
{
    private static readonly IReadOnlySet<string> Tracked = new HashSet<string>
    {
        nameof(MotionEnabled), nameof(MotionSensitivity), nameof(LineCrossing),
        nameof(Intrusion), nameof(ObjectLeft), nameof(FaceRecognition),
        nameof(EmailNotify), nameof(AlarmSound), nameof(FtpUpload)
    };
    protected override IReadOnlySet<string> TrackedProperties => Tracked;

    [ObservableProperty] private bool _motionEnabled = true;
    [ObservableProperty] private double _motionSensitivity = 60;

    [ObservableProperty] private bool _lineCrossing = true;
    [ObservableProperty] private bool _intrusion;
    [ObservableProperty] private bool _objectLeft;
    [ObservableProperty] private bool _faceRecognition;

    [ObservableProperty] private bool _emailNotify = true;
    [ObservableProperty] private bool _alarmSound;
    [ObservableProperty] private bool _ftpUpload;

    [ObservableProperty] private string _statusText = "События (stub) — ONVIF Event Service не подключён";

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
