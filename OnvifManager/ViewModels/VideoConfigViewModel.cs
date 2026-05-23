using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OnvifManager.Models;
using OnvifManager.Services;

namespace OnvifManager.ViewModels;

public partial class VideoConfigViewModel : ConfigEditorViewModel, IDisposable
{
    private readonly DiscoveryViewModel _discovery;
    private readonly OnvifClientProvider _provider;
    private CancellationTokenSource? _loadCts;
    private CameraDevice? _camera;
    private bool _disposed;
    private bool _suppressProfileChange;
    private bool _useMedia2;

    // Codec is read-only display (the true value, from Media2); the other fields are edits.
    private static readonly IReadOnlySet<string> Tracked = new HashSet<string>
    {
        nameof(Width), nameof(Height), nameof(FrameRateLimit), nameof(BitrateLimit),
        nameof(EncodingInterval), nameof(GovLength), nameof(H264Profile), nameof(Quality)
    };
    protected override IReadOnlySet<string> TrackedProperties => Tracked;

    public event Action? StreamProfileChanged;

    [ObservableProperty] private ObservableCollection<CameraProfile> _profiles = new();
    [ObservableProperty] private CameraProfile? _selectedProfile;
    [ObservableProperty] private ObservableCollection<VideoEncoderConfig> _encoderConfigs = new();
    [ObservableProperty] private VideoEncoderConfig? _selectedEncoder;
    [ObservableProperty] private string _encoding = "H264";
    [ObservableProperty] private int _width = 1920;
    [ObservableProperty] private int _height = 1080;
    [ObservableProperty] private int _frameRateLimit = 30;
    [ObservableProperty] private int _bitrateLimit = 4096;
    [ObservableProperty] private int _encodingInterval = 1;
    [ObservableProperty] private string _govLength = "30";
    [ObservableProperty] private string _h264Profile = "High";
    [ObservableProperty] private VideoQualityType _quality;
    [ObservableProperty] private string _streamUri = "";
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _statusText = "";

    public VideoConfigViewModel(DiscoveryViewModel discovery, OnvifClientProvider provider)
    {
        _discovery = discovery;
        _provider = provider;
        _discovery.CameraSelected += OnCameraChanged;
    }

    private void OnCameraChanged()
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = new CancellationTokenSource();
        _ = LoadAsync(_loadCts.Token);
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        _camera = _discovery.SelectedCamera;
        if (_camera == null) return;

        using var _track = SuspendTracking();
        ResetChanges();
        IsLoading = true;
        StatusText = "Loading video configurations...";

        try
        {
            var client = _provider.Get(_camera);
            var mediaService = new MediaService(client);

            var profiles = await mediaService.GetProfilesAsync(ct);
            Profiles = new ObservableCollection<CameraProfile>(profiles);

            // ONVIF Media1 (ver10) cannot express H265 — it reports H264 for an H265 stream
            // and its Set rejects H265. Prefer Media2 (ver20) when the device offers it; its
            // configs carry the real codec and round-trip correctly. Fall back to Media1.
            List<VideoEncoderConfig> configs;
            try
            {
                configs = await mediaService.GetVideoEncoderConfigurations2Async(ct);
                _useMedia2 = configs.Count > 0;
            }
            catch (OperationCanceledException) { throw; }
            catch { configs = new List<VideoEncoderConfig>(); _useMedia2 = false; }

            if (!_useMedia2)
                configs = await mediaService.GetAllVideoEncoderConfigurationsAsync(ct);

            EncoderConfigs = new ObservableCollection<VideoEncoderConfig>(configs);

            if (configs.Count > 0)
            {
                SelectedEncoder = configs[0];
                LoadEncoderIntoFields(configs[0]);
            }

            if (profiles.Count > 0)
            {
                _suppressProfileChange = true;
                try { SelectedProfile = profiles[0]; }
                finally { _suppressProfileChange = false; }
                try
                {
                    StreamUri = await mediaService.GetStreamUriAsync(profiles[0].Token, ct: ct);
                }
                catch (OperationCanceledException) { throw; }
                catch
                {
                    StreamUri = "Unable to retrieve stream URI";
                }
            }

            StatusText = "Configuration loaded";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Cancelled";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void LoadEncoderIntoFields(VideoEncoderConfig config)
    {
        Encoding = config.Encoding;
        Width = config.Width;
        Height = config.Height;
        FrameRateLimit = config.FrameRateLimit;
        BitrateLimit = config.BitrateLimit;
        EncodingInterval = config.EncodingInterval;
        GovLength = config.GovLength;
        H264Profile = config.H264Profile;
        Quality = config.Quality;
    }

    partial void OnSelectedEncoderChanged(VideoEncoderConfig? value)
    {
        if (value == null) return;
        using var _track = SuspendTracking();
        LoadEncoderIntoFields(value);
    }

    partial void OnSelectedProfileChanged(CameraProfile? value)
    {
        if (_suppressProfileChange || value == null || _camera == null) return;
        _ = RefreshStreamUriAsync(value);
    }

    private async Task RefreshStreamUriAsync(CameraProfile profile)
    {
        try
        {
            var client = _provider.Get(_camera!);
            var ms = new MediaService(client);
            StreamUri = await ms.GetStreamUriAsync(profile.Token);
        }
        catch (Exception ex)
        {
            StatusText = $"StreamUri error: {ex.Message}";
        }
        finally
        {
            StreamProfileChanged?.Invoke();
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (SelectedEncoder == null || _camera == null) return;

        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = new CancellationTokenSource();
        var ct = _loadCts.Token;

        IsLoading = true;
        StatusText = "Saving configuration...";

        try
        {
            SelectedEncoder.Encoding = Encoding;
            SelectedEncoder.Width = Width;
            SelectedEncoder.Height = Height;
            SelectedEncoder.FrameRateLimit = FrameRateLimit;
            SelectedEncoder.BitrateLimit = BitrateLimit;
            SelectedEncoder.EncodingInterval = EncodingInterval;
            SelectedEncoder.GovLength = GovLength;
            SelectedEncoder.H264Profile = H264Profile;
            SelectedEncoder.Quality = Quality;

            var client = _provider.Get(_camera);
            var mediaService = new MediaService(client);
            // Write through the same API we read from (Media2 handles H265; Media1 can't).
            if (_useMedia2)
                await mediaService.SetVideoEncoderConfiguration2Async(SelectedEncoder, ct);
            else
                await mediaService.SetVideoEncoderConfigurationAsync(SelectedEncoder, ct);

            ResetChanges();
            StatusText = "Configuration saved successfully";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Save cancelled";
        }
        catch (Exception ex)
        {
            StatusText = $"Save error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void Back() => _discovery.SelectedCamera = null;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _discovery.CameraSelected -= OnCameraChanged;
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = null;
    }
}
