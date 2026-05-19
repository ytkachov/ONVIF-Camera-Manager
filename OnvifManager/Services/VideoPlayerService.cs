using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using LibVLCSharp.Shared;

namespace OnvifManager.Services;

public sealed partial class VideoPlayerService : ObservableObject, IDisposable
{
    private readonly LibVLC _libVlc;
    private readonly MediaPlayer _player;
    private readonly DispatcherTimer _statsTimer;
    private Media? _currentMedia;
    private bool _disposed;

    [ObservableProperty] private bool _isPlaying;
    [ObservableProperty] private bool _isBuffering;
    [ObservableProperty] private bool _isRecording;
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private string _currentRecordingPath = "";
    [ObservableProperty] private float _fps;
    [ObservableProperty] private int _bitrateKbps;
    [ObservableProperty] private uint _videoWidth;
    [ObservableProperty] private uint _videoHeight;

    private string? _lastUri;
    private string? _lastUser;
    private string? _lastPass;

    public MediaPlayer Player => _player;

    public VideoPlayerService()
    {
        Core.Initialize();
        _libVlc = new LibVLC(
            "--no-osd",
            "--no-snapshot-preview",
            "--no-stats",
            "--network-caching=300");

        _player = new MediaPlayer(_libVlc)
        {
            EnableHardwareDecoding = true,
            EnableKeyInput = false,
            EnableMouseInput = false
        };

        _player.Playing += OnPlayerPlaying;
        _player.Stopped += OnPlayerStopped;
        _player.EndReached += OnPlayerStopped;
        _player.EncounteredError += OnPlayerError;
        _player.Buffering += OnPlayerBuffering;

        _statsTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _statsTimer.Tick += (_, _) => UpdateStats();
    }

    public void Play(string uri, string? username = null, string? password = null,
        string? recordPath = null)
    {
        if (_disposed) return;
        if (string.IsNullOrWhiteSpace(uri))
        {
            StatusText = "Empty RTSP URI";
            return;
        }

        StopInternal();

        _lastUri = uri;
        _lastUser = username;
        _lastPass = password;

        var media = new Media(_libVlc, new Uri(uri));
        media.AddOption(":rtsp-tcp");
        media.AddOption(":network-caching=300");
        media.AddOption(":live-caching=300");
        if (!string.IsNullOrEmpty(username))
            media.AddOption($":rtsp-user={username}");
        if (!string.IsNullOrEmpty(password))
            media.AddOption($":rtsp-pwd={password}");

        if (!string.IsNullOrEmpty(recordPath))
        {
            var soutPath = recordPath.Replace("\\", "/");
            media.AddOption(
                $":sout=#duplicate{{dst=display,dst=std{{access=file,mux=ts,dst='{soutPath}'}}}}");
            media.AddOption(":sout-keep");
        }

        _currentMedia = media;
        _player.Media = media;
        StatusText = "Connecting...";
        _player.Play();
        _statsTimer.Start();

        Dispatch(() =>
        {
            IsRecording = !string.IsNullOrEmpty(recordPath);
            CurrentRecordingPath = recordPath ?? "";
        });
    }

    public void StartRecording(string filePath)
    {
        if (_disposed) return;
        if (string.IsNullOrEmpty(_lastUri))
        {
            StatusText = "Запустите поток перед записью";
            return;
        }
        Play(_lastUri, _lastUser, _lastPass, filePath);
    }

    public void StopRecording()
    {
        if (_disposed || !IsRecording) return;
        if (string.IsNullOrEmpty(_lastUri))
        {
            Stop();
            return;
        }
        Play(_lastUri, _lastUser, _lastPass, null);
    }

    public void Stop()
    {
        if (_disposed) return;
        StopInternal();
        _lastUri = null;
        _lastUser = null;
        _lastPass = null;
        Dispatch(() =>
        {
            IsPlaying = false;
            IsBuffering = false;
            IsRecording = false;
            CurrentRecordingPath = "";
            StatusText = "Stopped";
            Fps = 0;
            BitrateKbps = 0;
        });
    }

    private void StopInternal()
    {
        _statsTimer.Stop();
        if (_player.IsPlaying)
        {
            try { _player.Stop(); } catch { }
        }
        _player.Media = null;
        if (_currentMedia != null)
        {
            try { _currentMedia.Dispose(); } catch { }
            _currentMedia = null;
        }
    }

    private void OnPlayerPlaying(object? sender, EventArgs e) => Dispatch(() =>
    {
        IsPlaying = true;
        IsBuffering = false;
        StatusText = "Playing";
    });

    private void OnPlayerStopped(object? sender, EventArgs e) => Dispatch(() =>
    {
        IsPlaying = false;
        IsBuffering = false;
        if (string.IsNullOrEmpty(StatusText) || StatusText == "Playing")
            StatusText = "Stopped";
        Fps = 0;
        BitrateKbps = 0;
    });

    private void OnPlayerError(object? sender, EventArgs e) => Dispatch(() =>
    {
        IsPlaying = false;
        IsBuffering = false;
        StatusText = "Playback error";
    });

    private void OnPlayerBuffering(object? sender, MediaPlayerBufferingEventArgs e)
    {
        var cache = e.Cache;
        Dispatch(() =>
        {
            IsBuffering = cache < 100f;
            if (IsBuffering) StatusText = $"Buffering {cache:0}%";
        });
    }

    private void UpdateStats()
    {
        if (_disposed || _currentMedia == null || !_player.IsPlaying) return;

        var fps = _player.Fps;
        var stats = _currentMedia.Statistics;
        var kbps = (int)(stats.DemuxBitrate * 8f / 1000f);

        uint w = 0, h = 0;
        try { _player.Size(0, ref w, ref h); } catch { }

        Dispatch(() =>
        {
            if (fps > 0) Fps = fps;
            if (kbps > 0) BitrateKbps = kbps;
            if (w > 0 && h > 0) { VideoWidth = w; VideoHeight = h; }
        });
    }

    private static void Dispatch(Action action)
    {
        var app = Application.Current;
        if (app == null) { action(); return; }
        if (app.Dispatcher.CheckAccess()) action();
        else app.Dispatcher.BeginInvoke(action);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try { _statsTimer.Stop(); } catch { }

        _player.Playing -= OnPlayerPlaying;
        _player.Stopped -= OnPlayerStopped;
        _player.EndReached -= OnPlayerStopped;
        _player.EncounteredError -= OnPlayerError;
        _player.Buffering -= OnPlayerBuffering;

        try { if (_player.IsPlaying) _player.Stop(); } catch { }
        try { _player.Media = null; } catch { }
        try { _currentMedia?.Dispose(); } catch { }
        try { _player.Dispose(); } catch { }
        try { _libVlc.Dispose(); } catch { }
    }
}
