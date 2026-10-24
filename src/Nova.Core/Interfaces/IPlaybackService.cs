using Nova.Core.Common;
using Nova.Core.Models;

namespace Nova.Core.Interfaces;

public class PlaybackPositionEventArgs : EventArgs
{
    public long PositionMs { get; }
    public long DurationMs { get; }

    public PlaybackPositionEventArgs(long positionMs, long durationMs)
    {
        PositionMs = positionMs;
        DurationMs = durationMs;
    }
}

public class PlaybackErrorEventArgs : EventArgs
{
    public string Message { get; }

    public PlaybackErrorEventArgs(string message)
    {
        Message = message;
    }
}

public interface IPlaybackService : IDisposable
{
    PlaybackState State { get; }
    long CurrentTimeMs { get; }
    long TotalDurationMs { get; }
    int Volume { get; }
    bool IsMuted { get; }
    bool IsSeekable { get; }
    MediaItem? CurrentMedia { get; }

    // Audio Tracks & Delay
    IReadOnlyList<TrackInfo> AudioTracks { get; }
    int CurrentAudioTrackId { get; }
    long AudioDelayMs { get; }

    // Subtitle Tracks & Delay
    IReadOnlyList<TrackInfo> SubtitleTracks { get; }
    int CurrentSubtitleTrackId { get; }
    long SubtitleDelayMs { get; }

    // Speed & Aspect Ratio
    float PlaybackSpeed { get; }
    string? AspectRatio { get; }

    // Events
    event EventHandler<PlaybackState>? StateChanged;
    event EventHandler<PlaybackPositionEventArgs>? PositionChanged;
    event EventHandler<long>? DurationChanged;
    event EventHandler<int>? VolumeChanged;
    event EventHandler<bool>? MuteChanged;
    event EventHandler<PlaybackErrorEventArgs>? ErrorOccurred;

    event EventHandler<IReadOnlyList<TrackInfo>>? AudioTracksChanged;
    event EventHandler<int>? AudioTrackChanged;
    event EventHandler<long>? AudioDelayChanged;

    event EventHandler<IReadOnlyList<TrackInfo>>? SubtitleTracksChanged;
    event EventHandler<int>? SubtitleTrackChanged;
    event EventHandler<long>? SubtitleDelayChanged;

    event EventHandler<float>? PlaybackSpeedChanged;
    event EventHandler<string?>? AspectRatioChanged;

    // Playback Control Operations
    Task<Result> PlayAsync(string pathOrUrl, long startPositionMs = 0);
    Task<Result> PauseAsync();
    Task<Result> ResumeAsync();
    Task<Result> StopAsync();
    Task<Result> SeekAsync(long positionMs);
    Task<Result> SetVolumeAsync(int volume);
    Task<Result> ToggleMuteAsync();

    // Advanced Playback Operations
    Task<Result> SetAudioTrackAsync(int trackId);
    Task<Result> SetAudioDelayAsync(long delayMs);
    Task<Result> SetSubtitleTrackAsync(int trackId);
    Task<Result> AddSubtitleFileAsync(string subtitleFilePath);
    Task<Result> SetSubtitleDelayAsync(long delayMs);
    Task<Result> SetPlaybackSpeedAsync(float speed);
    Task<Result> SetAspectRatioAsync(string? aspectRatio);
    Task<Result<MediaDetails>> GetMediaDetailsAsync();

    // Direct MediaPlayer access for UI VideoView binding
    object NativeMediaPlayer { get; }
}
