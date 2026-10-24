using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Nova.Core.Common;
using Nova.Core.Interfaces;
using Nova.Core.Models;
using Serilog;

namespace Nova.App.ViewModels;

public partial class PlayerViewModel : ObservableObject, IDisposable
{
    private readonly IPlaybackService _playbackService;
    private readonly DispatcherTimer _overlayTimer;
    private bool _isUserSeeking;
    private bool _isDisposed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    private string _currentMediaTitle = "No media loaded";

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private bool _isPaused;

    [ObservableProperty]
    private bool _isBuffering;

    [ObservableProperty]
    private long _currentPositionMs;

    [ObservableProperty]
    private long _totalDurationMs;

    [ObservableProperty]
    private string _currentTimeText = "00:00";

    [ObservableProperty]
    private string _totalTimeText = "00:00";

    [ObservableProperty]
    private int _volume = 100;

    [ObservableProperty]
    private bool _isMuted;

    [ObservableProperty]
    private bool _isFullscreen;

    [ObservableProperty]
    private bool _isOverlayVisible = true;

    [ObservableProperty]
    private string? _statusMessage;

    // Phase 2: Audio Tracks
    [ObservableProperty]
    private ObservableCollection<TrackInfo> _audioTracks = new();

    [ObservableProperty]
    private int _selectedAudioTrackId = -1;

    [ObservableProperty]
    private long _audioDelayMs;

    // Phase 2: Subtitle Tracks
    [ObservableProperty]
    private ObservableCollection<TrackInfo> _subtitleTracks = new();

    [ObservableProperty]
    private int _selectedSubtitleTrackId = -1;

    [ObservableProperty]
    private long _subtitleDelayMs;

    // Phase 2: Playback Speed
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlaybackSpeedText))]
    private float _playbackSpeed = 1.0f;

    public string PlaybackSpeedText => $"{PlaybackSpeed:0.##}×";

    public static float[] AvailablePlaybackSpeeds { get; } = [0.25f, 0.5f, 0.75f, 1.0f, 1.25f, 1.5f, 2.0f, 3.0f, 4.0f];

    // Phase 2: Aspect Ratio
    [ObservableProperty]
    private string _selectedAspectRatio = "Default";

    public static string[] AvailableAspectRatios { get; } = ["Default", "16:9", "4:3", "1:1", "16:10", "21:9"];

    public string WindowTitle => string.IsNullOrWhiteSpace(CurrentMediaTitle) || CurrentMediaTitle == "No media loaded"
        ? "NovaPlayer"
        : $"NovaPlayer — {CurrentMediaTitle}";

    public object NativeMediaPlayer => _playbackService.NativeMediaPlayer;

    public PlayerViewModel(IPlaybackService playbackService)
    {
        _playbackService = playbackService;

        _overlayTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2.5)
        };
        _overlayTimer.Tick += (s, e) =>
        {
            if (IsFullscreen && IsPlaying)
            {
                IsOverlayVisible = false;
            }
        };

        SubscribePlaybackEvents();
    }

    private void SubscribePlaybackEvents()
    {
        _playbackService.StateChanged += OnStateChanged;
        _playbackService.PositionChanged += OnPositionChanged;
        _playbackService.DurationChanged += OnDurationChanged;
        _playbackService.VolumeChanged += OnVolumeChanged;
        _playbackService.MuteChanged += OnMuteChanged;
        _playbackService.ErrorOccurred += OnErrorOccurred;

        _playbackService.AudioTracksChanged += OnAudioTracksChanged;
        _playbackService.AudioTrackChanged += OnAudioTrackChanged;
        _playbackService.AudioDelayChanged += OnAudioDelayChanged;

        _playbackService.SubtitleTracksChanged += OnSubtitleTracksChanged;
        _playbackService.SubtitleTrackChanged += OnSubtitleTrackChanged;
        _playbackService.SubtitleDelayChanged += OnSubtitleDelayChanged;

        _playbackService.PlaybackSpeedChanged += OnPlaybackSpeedChanged;
        _playbackService.AspectRatioChanged += OnAspectRatioChanged;
    }

    private void OnStateChanged(object? sender, PlaybackState state)
    {
        RunOnUi(() =>
        {
            IsPlaying = state == PlaybackState.Playing;
            IsPaused = state == PlaybackState.Paused;
            IsBuffering = state == PlaybackState.Buffering;

            if (state == PlaybackState.Ended)
            {
                IsPlaying = false;
                IsPaused = false;
                StatusMessage = "Playback ended.";
                IsOverlayVisible = true;
            }
        });
    }

    private void OnPositionChanged(object? sender, PlaybackPositionEventArgs e)
    {
        if (_isUserSeeking) return;

        RunOnUi(() =>
        {
            CurrentPositionMs = e.PositionMs;
            CurrentTimeText = TimeFormatter.FormatMilliseconds(e.PositionMs);
        });
    }

    private void OnDurationChanged(object? sender, long durationMs)
    {
        RunOnUi(() =>
        {
            TotalDurationMs = durationMs;
            TotalTimeText = TimeFormatter.FormatMilliseconds(durationMs);
        });
    }

    private void OnVolumeChanged(object? sender, int volume)
    {
        RunOnUi(() =>
        {
            Volume = volume;
        });
    }

    private void OnMuteChanged(object? sender, bool isMuted)
    {
        RunOnUi(() =>
        {
            IsMuted = isMuted;
        });
    }

    private void OnErrorOccurred(object? sender, PlaybackErrorEventArgs e)
    {
        RunOnUi(() =>
        {
            StatusMessage = e.Message;
            IsOverlayVisible = true;
        });
    }

    private void OnAudioTracksChanged(object? sender, IReadOnlyList<TrackInfo> tracks)
    {
        RunOnUi(() =>
        {
            AudioTracks = new ObservableCollection<TrackInfo>(tracks);
        });
    }

    private void OnAudioTrackChanged(object? sender, int trackId)
    {
        RunOnUi(() =>
        {
            SelectedAudioTrackId = trackId;
        });
    }

    private void OnAudioDelayChanged(object? sender, long delayMs)
    {
        RunOnUi(() =>
        {
            AudioDelayMs = delayMs;
        });
    }

    private void OnSubtitleTracksChanged(object? sender, IReadOnlyList<TrackInfo> tracks)
    {
        RunOnUi(() =>
        {
            SubtitleTracks = new ObservableCollection<TrackInfo>(tracks);
        });
    }

    private void OnSubtitleTrackChanged(object? sender, int trackId)
    {
        RunOnUi(() =>
        {
            SelectedSubtitleTrackId = trackId;
        });
    }

    private void OnSubtitleDelayChanged(object? sender, long delayMs)
    {
        RunOnUi(() =>
        {
            SubtitleDelayMs = delayMs;
        });
    }

    private void OnPlaybackSpeedChanged(object? sender, float speed)
    {
        RunOnUi(() =>
        {
            PlaybackSpeed = speed;
        });
    }

    private void OnAspectRatioChanged(object? sender, string? ratio)
    {
        RunOnUi(() =>
        {
            SelectedAspectRatio = string.IsNullOrWhiteSpace(ratio) ? "Default" : ratio;
        });
    }

    [RelayCommand]
    public async Task OpenFileAsync()
    {
        var openFileDialog = new OpenFileDialog
        {
            Title = "Select Media File — NovaPlayer",
            Filter = "Media Files (*.mp4;*.mkv;*.avi;*.webm;*.mov;*.mp3;*.flac;*.wav;*.ogg)|*.mp4;*.mkv;*.avi;*.webm;*.mov;*.mp3;*.flac;*.wav;*.ogg|All Files (*.*)|*.*",
            Multiselect = false
        };

        if (openFileDialog.ShowDialog() == true)
        {
            await PlayFileAsync(openFileDialog.FileName);
        }
    }

    public async Task PlayFileAsync(string filePath)
    {
        try
        {
            CurrentMediaTitle = Path.GetFileName(filePath);
            StatusMessage = null;
            var result = await _playbackService.PlayAsync(filePath);
            if (result.IsFailure)
            {
                StatusMessage = result.Error;
            }
            else
            {
                TriggerUserActivity();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to open file: {FilePath}", filePath);
            StatusMessage = $"Cannot play file: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task PlayPauseAsync()
    {
        if (IsPlaying)
        {
            await _playbackService.PauseAsync();
        }
        else if (IsPaused)
        {
            await _playbackService.ResumeAsync();
        }
        else if (_playbackService.CurrentMedia != null)
        {
            await _playbackService.PlayAsync(_playbackService.CurrentMedia.FilePathOrUrl);
        }
        else
        {
            await OpenFileAsync();
        }

        TriggerUserActivity();
    }

    [RelayCommand]
    public async Task StopAsync()
    {
        await _playbackService.StopAsync();
        IsPlaying = false;
        IsPaused = false;
        CurrentPositionMs = 0;
        CurrentTimeText = "00:00";
        IsOverlayVisible = true;
    }

    [RelayCommand]
    public void StartSeek()
    {
        _isUserSeeking = true;
    }

    [RelayCommand]
    public async Task EndSeekAsync(double newPositionMs)
    {
        _isUserSeeking = false;
        await _playbackService.SeekAsync((long)newPositionMs);
        TriggerUserActivity();
    }

    [RelayCommand]
    public async Task SeekRelativeAsync(long deltaMs)
    {
        var targetPos = CurrentPositionMs + deltaMs;
        await _playbackService.SeekAsync(targetPos);
        TriggerUserActivity();
    }

    [RelayCommand]
    public async Task ChangeVolumeAsync(int newVolume)
    {
        await _playbackService.SetVolumeAsync(newVolume);
    }

    [RelayCommand]
    public async Task VolumeRelativeAsync(int delta)
    {
        var targetVolume = Math.Clamp(Volume + delta, 0, 100);
        await _playbackService.SetVolumeAsync(targetVolume);
        TriggerUserActivity();
    }

    [RelayCommand]
    public async Task ToggleMuteAsync()
    {
        await _playbackService.ToggleMuteAsync();
        TriggerUserActivity();
    }

    [RelayCommand]
    public void ToggleFullscreen()
    {
        IsFullscreen = !IsFullscreen;
        IsOverlayVisible = true;
        TriggerUserActivity();
    }

    [RelayCommand]
    public void ExitFullscreen()
    {
        if (IsFullscreen)
        {
            IsFullscreen = false;
            IsOverlayVisible = true;
        }
    }

    // Phase 2: Audio Track Controls
    [RelayCommand]
    public async Task SelectAudioTrackAsync(int trackId)
    {
        await _playbackService.SetAudioTrackAsync(trackId);
        TriggerUserActivity();
    }

    [RelayCommand]
    public async Task AdjustAudioDelayAsync(long deltaMs)
    {
        var newDelay = AudioDelayMs + deltaMs;
        await _playbackService.SetAudioDelayAsync(newDelay);
        StatusMessage = $"Audio Delay: {newDelay} ms";
        TriggerUserActivity();
    }

    [RelayCommand]
    public async Task ResetAudioDelayAsync()
    {
        await _playbackService.SetAudioDelayAsync(0);
        StatusMessage = "Audio Delay reset to 0 ms";
        TriggerUserActivity();
    }

    // Phase 2: Subtitle Controls
    [RelayCommand]
    public async Task SelectSubtitleTrackAsync(int trackId)
    {
        await _playbackService.SetSubtitleTrackAsync(trackId);
        TriggerUserActivity();
    }

    [RelayCommand]
    public async Task AddExternalSubtitleAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Load External Subtitle File",
            Filter = "Subtitle Files (*.srt;*.ass;*.ssa;*.vtt;*.sub)|*.srt;*.ass;*.ssa;*.vtt;*.sub|All Files (*.*)|*.*",
            Multiselect = false
        };

        if (dialog.ShowDialog() == true)
        {
            var res = await _playbackService.AddSubtitleFileAsync(dialog.FileName);
            if (res.IsFailure)
            {
                StatusMessage = $"Failed to load subtitle: {res.Error}";
            }
            else
            {
                StatusMessage = $"Loaded subtitle: {Path.GetFileName(dialog.FileName)}";
            }
        }
        TriggerUserActivity();
    }

    [RelayCommand]
    public async Task AdjustSubtitleDelayAsync(long deltaMs)
    {
        var newDelay = SubtitleDelayMs + deltaMs;
        await _playbackService.SetSubtitleDelayAsync(newDelay);
        StatusMessage = $"Subtitle Delay: {newDelay} ms";
        TriggerUserActivity();
    }

    [RelayCommand]
    public async Task ResetSubtitleDelayAsync()
    {
        await _playbackService.SetSubtitleDelayAsync(0);
        StatusMessage = "Subtitle Delay reset to 0 ms";
        TriggerUserActivity();
    }

    // Phase 2: Playback Speed Controls
    [RelayCommand]
    public async Task SetPlaybackSpeedAsync(float speed)
    {
        await _playbackService.SetPlaybackSpeedAsync(speed);
        TriggerUserActivity();
    }

    [RelayCommand]
    public async Task CyclePlaybackSpeedAsync()
    {
        // Cycle: 0.5 -> 0.75 -> 1.0 -> 1.25 -> 1.5 -> 2.0 -> 0.5
        float nextSpeed = PlaybackSpeed switch
        {
            < 0.7f => 0.75f,
            < 0.9f => 1.0f,
            < 1.2f => 1.25f,
            < 1.4f => 1.5f,
            < 1.8f => 2.0f,
            _ => 1.0f
        };

        await SetPlaybackSpeedAsync(nextSpeed);
    }

    // Phase 2: Aspect Ratio Controls
    [RelayCommand]
    public async Task SetAspectRatioAsync(string? ratio)
    {
        await _playbackService.SetAspectRatioAsync(ratio);
        TriggerUserActivity();
    }

    // Event to decouple UI Dialog display from ViewModel
    public event Action<MediaDetails>? ShowMediaInfoRequested;

    // Phase 2: Media Details Inspector Dialog
    [RelayCommand]
    public async Task ShowMediaInfoAsync()
    {
        var detailsResult = await _playbackService.GetMediaDetailsAsync();
        if (detailsResult.IsSuccess)
        {
            ShowMediaInfoRequested?.Invoke(detailsResult.Value);
        }
        else
        {
            StatusMessage = detailsResult.Error;
        }
        TriggerUserActivity();
    }

    [RelayCommand]
    public void TriggerUserActivity()
    {
        IsOverlayVisible = true;
        _overlayTimer.Stop();
        if (IsFullscreen && IsPlaying)
        {
            _overlayTimer.Start();
        }
    }

    private static void RunOnUi(Action action)
    {
        if (System.Windows.Application.Current?.Dispatcher != null)
        {
            if (System.Windows.Application.Current.Dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                System.Windows.Application.Current.Dispatcher.Invoke(action);
            }
        }
        else
        {
            action();
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _overlayTimer.Stop();
        _playbackService.StateChanged -= OnStateChanged;
        _playbackService.PositionChanged -= OnPositionChanged;
        _playbackService.DurationChanged -= OnDurationChanged;
        _playbackService.VolumeChanged -= OnVolumeChanged;
        _playbackService.MuteChanged -= OnMuteChanged;
        _playbackService.ErrorOccurred -= OnErrorOccurred;

        _playbackService.AudioTracksChanged -= OnAudioTracksChanged;
        _playbackService.AudioTrackChanged -= OnAudioTrackChanged;
        _playbackService.AudioDelayChanged -= OnAudioDelayChanged;

        _playbackService.SubtitleTracksChanged -= OnSubtitleTracksChanged;
        _playbackService.SubtitleTrackChanged -= OnSubtitleTrackChanged;
        _playbackService.SubtitleDelayChanged -= OnSubtitleDelayChanged;

        _playbackService.PlaybackSpeedChanged -= OnPlaybackSpeedChanged;
        _playbackService.AspectRatioChanged -= OnAspectRatioChanged;

        _playbackService.Dispose();
    }
}
