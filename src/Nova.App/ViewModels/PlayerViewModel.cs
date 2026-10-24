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
    private readonly ILibraryRepository? _libraryRepository;
    private readonly DispatcherTimer _overlayTimer;
    private bool _isUserSeeking;
    private bool _isDisposed;
    private DateTime _lastHistorySaveUtc = DateTime.MinValue;

    public event Action<MediaDetails>? ShowMediaInfoRequested;
    public event Action? ShowEqualizerRequested;
    public event Action? OpenUrlRequested;
    public event Action? ShowAboutRequested;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    private string _currentMediaTitle = "No media loaded";

    [ObservableProperty]
    private string? _currentFilePath;

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

    // Phase 3: Resume Playback
    [ObservableProperty]
    private bool _isResumePromptVisible;

    [ObservableProperty]
    private long _pendingResumePositionMs;

    [ObservableProperty]
    private string _resumePromptText = string.Empty;

    // Phase 3: Navigation and Library
    [ObservableProperty]
    private LibraryViewModel? _library;

    // Phase 5: Resumable Downloads
    [ObservableProperty]
    private DownloadsViewModel? _downloads;

    [ObservableProperty]
    private int _activeTabIndex;

    [RelayCommand]
    public void SwitchTab(int index)
    {
        ActiveTabIndex = index;
    }

    // Phase 4: Buffering & Network
    [ObservableProperty]
    private float _bufferingPercentage;

    [ObservableProperty]
    private bool _isNetworkPlayback;

    // Phase 4: Equalizer
    public EqualizerViewModel Equalizer { get; }

    // Phase 4: Mini-Player & Theming
    [ObservableProperty]
    private bool _isMiniPlayer;

    [ObservableProperty]
    private string _currentTheme = "Dark";

    // Phase 3: Playlist Queue
    [ObservableProperty]
    private ObservableCollection<string> _playbackQueue = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNextTrack))]
    [NotifyPropertyChangedFor(nameof(HasPreviousTrack))]
    private int _currentQueueIndex = -1;

    [ObservableProperty]
    private RepeatMode _repeatMode = RepeatMode.None;

    public bool HasNextTrack => PlaybackQueue.Count > 0 && (CurrentQueueIndex < PlaybackQueue.Count - 1 || RepeatMode == RepeatMode.RepeatAll);
    public bool HasPreviousTrack => PlaybackQueue.Count > 0 && (CurrentQueueIndex > 0 || RepeatMode == RepeatMode.RepeatAll);

    public string WindowTitle => string.IsNullOrWhiteSpace(CurrentMediaTitle) || CurrentMediaTitle == "No media loaded"
        ? "NovaPlayer"
        : $"NovaPlayer — {CurrentMediaTitle}";

    public object NativeMediaPlayer => _playbackService.NativeMediaPlayer;

    public PlayerViewModel(IPlaybackService playbackService, ILibraryRepository? libraryRepository = null)
    {
        _playbackService = playbackService;
        _libraryRepository = libraryRepository;
        Equalizer = new EqualizerViewModel(playbackService, libraryRepository);

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
        _ = LoadInitialThemeAsync();
    }

    private async Task LoadInitialThemeAsync()
    {
        if (_libraryRepository == null) return;
        try
        {
            var res = await _libraryRepository.GetSettingAsync("theme", "Dark");
            if (res.IsSuccess && !string.IsNullOrWhiteSpace(res.Value))
            {
                CurrentTheme = res.Value;
                Services.ThemeManager.ApplyTheme(CurrentTheme);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load saved theme.");
        }
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
        _playbackService.BufferingProgressChanged += OnBufferingProgressChanged;
    }

    private void OnBufferingProgressChanged(object? sender, float cache)
    {
        RunOnUi(() =>
        {
            BufferingPercentage = cache;
            IsBuffering = cache < 100f;
        });
    }

    private void OnStateChanged(object? sender, PlaybackState state)
    {
        RunOnUi(() =>
        {
            IsPlaying = state == PlaybackState.Playing;
            IsPaused = state == PlaybackState.Paused;
            IsBuffering = state == PlaybackState.Buffering;

            if (state == PlaybackState.Playing)
            {
                BufferingPercentage = 100f;
            }
            else if (state == PlaybackState.Stopped || state == PlaybackState.Ended)
            {
                BufferingPercentage = 0f;
            }

            if (state == PlaybackState.Paused || state == PlaybackState.Ended)
            {
                SaveProgress();
            }

            if (state == PlaybackState.Ended)
            {
                IsPlaying = false;
                IsPaused = false;
                StatusMessage = "Playback ended.";
                IsOverlayVisible = true;

                HandlePlaybackEnded();
            }
        });
    }

    private void HandlePlaybackEnded()
    {
        if (RepeatMode == RepeatMode.RepeatOne && !string.IsNullOrWhiteSpace(CurrentFilePath))
        {
            _ = PlayFileAsync(CurrentFilePath);
        }
        else if (PlaybackQueue.Count > 0)
        {
            if (CurrentQueueIndex < PlaybackQueue.Count - 1)
            {
                _ = PlayNextTrackAsync();
            }
            else if (RepeatMode == RepeatMode.RepeatAll)
            {
                CurrentQueueIndex = 0;
                _ = PlayFileAsync(PlaybackQueue[0]);
            }
        }
    }

    private void OnPositionChanged(object? sender, PlaybackPositionEventArgs e)
    {
        if (_isUserSeeking) return;

        RunOnUi(() =>
        {
            CurrentPositionMs = e.PositionMs;
            CurrentTimeText = TimeFormatter.FormatMilliseconds(e.PositionMs);

            // Throttle periodic progress save to database (every 5 seconds)
            if (_libraryRepository != null && !string.IsNullOrWhiteSpace(CurrentFilePath) && CurrentPositionMs > 0)
            {
                if ((DateTime.UtcNow - _lastHistorySaveUtc).TotalSeconds >= 5)
                {
                    _lastHistorySaveUtc = DateTime.UtcNow;
                    SaveProgress();
                }
            }
        });
    }

    private void SaveProgress()
    {
        if (_libraryRepository == null || string.IsNullOrWhiteSpace(CurrentFilePath)) return;

        var path = CurrentFilePath;
        var title = CurrentMediaTitle;
        var pos = CurrentPositionMs;
        var dur = TotalDurationMs;

        Task.Run(async () =>
        {
            try
            {
                await _libraryRepository.RecordPlaybackHistoryAsync(path, title, pos, dur);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to persist playback history for {Path}", path);
            }
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
        });
    }

    private void OnAudioTracksChanged(object? sender, IReadOnlyList<TrackInfo> tracks)
    {
        RunOnUi(() =>
        {
            AudioTracks.Clear();
            foreach (var track in tracks)
            {
                AudioTracks.Add(track);
            }
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
            SubtitleTracks.Clear();
            foreach (var track in tracks)
            {
                SubtitleTracks.Add(track);
            }
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

    private void OnAspectRatioChanged(object? sender, string? aspectRatio)
    {
        RunOnUi(() =>
        {
            SelectedAspectRatio = string.IsNullOrWhiteSpace(aspectRatio) ? "Default" : aspectRatio;
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
            // Clear queue when opening single standalone file
            PlaybackQueue.Clear();
            CurrentQueueIndex = -1;
            await PlayFileAsync(openFileDialog.FileName);
        }
    }

    public async Task PlayFileAsync(string filePath)
    {
        try
        {
            CurrentFilePath = filePath;
            IsNetworkPlayback = filePath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                                filePath.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

            CurrentMediaTitle = IsNetworkPlayback
                ? filePath
                : Path.GetFileName(filePath);

            StatusMessage = null;
            IsResumePromptVisible = false;
            PendingResumePositionMs = 0;

            // Check if resume point exists
            if (_libraryRepository != null)
            {
                try
                {
                    var resumeResult = await _libraryRepository.GetResumePointAsync(filePath);
                    if (resumeResult.IsSuccess && resumeResult.Value != null)
                    {
                        var entry = resumeResult.Value;
                        // Threshold: > 10 seconds in and < 95% of duration
                        if (entry.PositionMs > 10000 &&
                            (entry.DurationMs <= 0 || entry.PositionMs < (long)(entry.DurationMs * 0.95)))
                        {
                            PendingResumePositionMs = entry.PositionMs;
                            ResumePromptText = $"Resume from {TimeFormatter.FormatMilliseconds(entry.PositionMs)}?";
                            IsResumePromptVisible = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to check resume state for {Path}", filePath);
                }
            }

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
    public async Task ResumePlaybackAsync()
    {
        if (PendingResumePositionMs > 0)
        {
            await _playbackService.SeekAsync(PendingResumePositionMs);
        }
        IsResumePromptVisible = false;
    }

    [RelayCommand]
    public void DismissResumePrompt()
    {
        IsResumePromptVisible = false;
    }

    public void SetQueue(IReadOnlyList<string> items, int startIndex = 0)
    {
        PlaybackQueue.Clear();
        foreach (var item in items)
        {
            PlaybackQueue.Add(item);
        }

        if (startIndex >= 0 && startIndex < PlaybackQueue.Count)
        {
            CurrentQueueIndex = startIndex;
            _ = PlayFileAsync(PlaybackQueue[startIndex]);
        }
        else
        {
            CurrentQueueIndex = -1;
        }

        OnPropertyChanged(nameof(HasNextTrack));
        OnPropertyChanged(nameof(HasPreviousTrack));
    }

    [RelayCommand]
    public async Task PlayNextTrackAsync()
    {
        if (PlaybackQueue.Count == 0) return;

        if (CurrentQueueIndex < PlaybackQueue.Count - 1)
        {
            CurrentQueueIndex++;
            await PlayFileAsync(PlaybackQueue[CurrentQueueIndex]);
        }
        else if (RepeatMode == RepeatMode.RepeatAll)
        {
            CurrentQueueIndex = 0;
            await PlayFileAsync(PlaybackQueue[0]);
        }
    }

    [RelayCommand]
    public async Task PlayPreviousTrackAsync()
    {
        if (PlaybackQueue.Count == 0) return;

        // If playing for more than 3 seconds, restart current track
        if (CurrentPositionMs > 3000)
        {
            await _playbackService.SeekAsync(0);
            return;
        }

        if (CurrentQueueIndex > 0)
        {
            CurrentQueueIndex--;
            await PlayFileAsync(PlaybackQueue[CurrentQueueIndex]);
        }
        else if (RepeatMode == RepeatMode.RepeatAll)
        {
            CurrentQueueIndex = PlaybackQueue.Count - 1;
            await PlayFileAsync(PlaybackQueue[CurrentQueueIndex]);
        }
    }

    [RelayCommand]
    public void CycleRepeatMode()
    {
        RepeatMode = RepeatMode switch
        {
            RepeatMode.None => RepeatMode.RepeatAll,
            RepeatMode.RepeatAll => RepeatMode.RepeatOne,
            RepeatMode.RepeatOne => RepeatMode.None,
            _ => RepeatMode.None
        };
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
        SaveProgress();
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
    public async Task SetVolumeAsync(int newVolume)
    {
        await _playbackService.SetVolumeAsync(newVolume);
        TriggerUserActivity();
    }

    [RelayCommand]
    public async Task VolumeRelativeAsync(int delta)
    {
        var newVol = Math.Clamp(Volume + delta, 0, 100);
        await SetVolumeAsync(newVol);
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

    [RelayCommand]
    public async Task SelectAudioTrackAsync(int trackId)
    {
        await _playbackService.SetAudioTrackAsync(trackId);
        TriggerUserActivity();
    }

    [RelayCommand]
    public async Task AdjustAudioDelayAsync(long deltaMs)
    {
        var newDelay = Math.Clamp(AudioDelayMs + deltaMs, -5000, 5000);
        await _playbackService.SetAudioDelayAsync(newDelay);
        StatusMessage = $"Audio Delay: {newDelay:+0;-0;0} ms";
        TriggerUserActivity();
    }

    [RelayCommand]
    public async Task ResetAudioDelayAsync()
    {
        await _playbackService.SetAudioDelayAsync(0);
        StatusMessage = "Audio Delay: 0 ms";
        TriggerUserActivity();
    }

    [RelayCommand]
    public async Task SelectSubtitleTrackAsync(int trackId)
    {
        await _playbackService.SetSubtitleTrackAsync(trackId);
        TriggerUserActivity();
    }

    [RelayCommand]
    public async Task AdjustSubtitleDelayAsync(long deltaMs)
    {
        var newDelay = Math.Clamp(SubtitleDelayMs + deltaMs, -5000, 5000);
        await _playbackService.SetSubtitleDelayAsync(newDelay);
        StatusMessage = $"Subtitle Delay: {newDelay:+0;-0;0} ms";
        TriggerUserActivity();
    }

    [RelayCommand]
    public async Task ResetSubtitleDelayAsync()
    {
        await _playbackService.SetSubtitleDelayAsync(0);
        StatusMessage = "Subtitle Delay: 0 ms";
        TriggerUserActivity();
    }

    [RelayCommand]
    public async Task LoadExternalSubtitleAsync()
    {
        var openFileDialog = new OpenFileDialog
        {
            Title = "Select Subtitle File — NovaPlayer",
            Filter = "Subtitle Files (*.srt;*.sub;*.vtt;*.ass;*.ssa)|*.srt;*.sub;*.vtt;*.ass;*.ssa|All Files (*.*)|*.*",
            Multiselect = false
        };

        if (openFileDialog.ShowDialog() == true)
        {
            var result = await _playbackService.AddSubtitleFileAsync(openFileDialog.FileName);
            if (result.IsSuccess)
            {
                StatusMessage = $"Loaded subtitle: {Path.GetFileName(openFileDialog.FileName)}";
            }
            else
            {
                StatusMessage = result.Error;
            }
        }
        TriggerUserActivity();
    }

    [RelayCommand]
    public async Task SetPlaybackSpeedAsync(float speed)
    {
        await _playbackService.SetPlaybackSpeedAsync(speed);
        StatusMessage = $"Speed: {speed:0.##}×";
        TriggerUserActivity();
    }

    [RelayCommand]
    public async Task CyclePlaybackSpeedAsync()
    {
        int currentIndex = Array.FindIndex(AvailablePlaybackSpeeds, s => Math.Abs(s - PlaybackSpeed) < 0.05f);
        int nextIndex = currentIndex < 0 ? 0 : (currentIndex + 1) % AvailablePlaybackSpeeds.Length;
        await SetPlaybackSpeedAsync(AvailablePlaybackSpeeds[nextIndex]);
    }

    [RelayCommand]
    public async Task SetAspectRatioAsync(string aspectRatio)
    {
        var ratioParam = aspectRatio == "Default" ? null : aspectRatio;
        await _playbackService.SetAspectRatioAsync(ratioParam);
        SelectedAspectRatio = aspectRatio;
        StatusMessage = $"Aspect Ratio: {aspectRatio}";
        TriggerUserActivity();
    }

    [RelayCommand]
    public async Task PlayUrlAsync(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;

        var validation = UrlValidator.ValidateMediaUrl(url);
        if (validation.IsFailure)
        {
            StatusMessage = validation.Error;
            return;
        }

        ActiveTabIndex = 0;
        await PlayFileAsync(validation.Value.AbsoluteUri);
    }

    [RelayCommand]
    public void OpenUrl()
    {
        OpenUrlRequested?.Invoke();
    }

    [RelayCommand]
    public void ShowEqualizer()
    {
        ShowEqualizerRequested?.Invoke();
    }

    [RelayCommand]
    public void ShowAbout()
    {
        ShowAboutRequested?.Invoke();
    }

    [RelayCommand]
    public async Task TakeSnapshotAsync()
    {
        var result = await _playbackService.TakeSnapshotAsync();
        if (result.IsSuccess)
        {
            StatusMessage = $"Snapshot saved: {Path.GetFileName(result.Value)}";
        }
        else
        {
            StatusMessage = result.Error;
        }
        TriggerUserActivity();
    }

    [RelayCommand]
    public async Task ToggleThemeAsync()
    {
        Services.ThemeManager.ToggleTheme();
        CurrentTheme = Services.ThemeManager.CurrentTheme;
        StatusMessage = $"Theme: {CurrentTheme}";

        if (_libraryRepository != null)
        {
            await _libraryRepository.SetSettingAsync("theme", CurrentTheme);
        }
    }

    [RelayCommand]
    public void ToggleMiniPlayer()
    {
        IsMiniPlayer = !IsMiniPlayer;
        TriggerUserActivity();
    }

    [RelayCommand]
    public void ExitMiniPlayer()
    {
        if (IsMiniPlayer)
        {
            IsMiniPlayer = false;
            TriggerUserActivity();
        }
    }

    [RelayCommand]
    public async Task ShowMediaInfoAsync()
    {
        var result = await _playbackService.GetMediaDetailsAsync();
        if (result.IsSuccess && result.Value != null)
        {
            ShowMediaInfoRequested?.Invoke(result.Value);
        }
        else
        {
            StatusMessage = result.Error ?? "Media information not available.";
        }
    }

    public void TriggerUserActivity()
    {
        IsOverlayVisible = true;
        if (IsFullscreen && IsPlaying)
        {
            _overlayTimer.Stop();
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

        SaveProgress();

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
        _playbackService.BufferingProgressChanged -= OnBufferingProgressChanged;

        _playbackService.Dispose();
    }
}
