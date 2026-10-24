using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using Nova.Core.Common;
using Nova.Core.Models;

namespace Nova.App.ViewModels;

public partial class DownloadItemViewModel : ObservableObject
{
    public DownloadItem Model { get; }

    public string Id => Model.Id;
    public string Url => Model.Url;
    public string DestinationPath => Model.DestinationPath;
    public string TempFilePath => Model.TempFilePath;
    public string FileName => Model.FileName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(CanPause))]
    [NotifyPropertyChangedFor(nameof(CanResume))]
    [NotifyPropertyChangedFor(nameof(CanCancel))]
    [NotifyPropertyChangedFor(nameof(CanRetry))]
    [NotifyPropertyChangedFor(nameof(CanOpenFile))]
    private DownloadState _state;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressText))]
    [NotifyPropertyChangedFor(nameof(ProgressPercentage))]
    private long _bytesDownloaded;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressText))]
    [NotifyPropertyChangedFor(nameof(ProgressPercentage))]
    [NotifyPropertyChangedFor(nameof(IsIndeterminate))]
    private long? _totalBytes;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SpeedText))]
    private double _bytesPerSecond;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EtaText))]
    private TimeSpan? _estimatedRemaining;

    [ObservableProperty]
    private string? _errorMessage;

    public double ProgressPercentage => TotalBytes.HasValue && TotalBytes.Value > 0
        ? Math.Clamp((double)BytesDownloaded / TotalBytes.Value * 100.0, 0.0, 100.0)
        : 0.0;

    public bool IsIndeterminate => (!TotalBytes.HasValue || TotalBytes.Value <= 0) && State == DownloadState.Downloading;

    public string StatusText => State switch
    {
        DownloadState.Queued => "Queued",
        DownloadState.Downloading => "Downloading",
        DownloadState.Paused => "Paused",
        DownloadState.Completed => "Completed",
        DownloadState.Failed => "Failed",
        DownloadState.Cancelled => "Cancelled",
        _ => State.ToString()
    };

    public string ProgressText
    {
        get
        {
            var downloadedStr = FormatFileSize(BytesDownloaded);
            if (TotalBytes.HasValue && TotalBytes.Value > 0)
            {
                var totalStr = FormatFileSize(TotalBytes.Value);
                return $"{downloadedStr} / {totalStr} ({ProgressPercentage:0.0}%)";
            }
            return downloadedStr;
        }
    }

    public string SpeedText => State == DownloadState.Downloading && BytesPerSecond > 0
        ? $"{FormatFileSize((long)BytesPerSecond)}/s"
        : "—";

    public string EtaText
    {
        get
        {
            if (State != DownloadState.Downloading || !EstimatedRemaining.HasValue)
                return "—";

            var eta = EstimatedRemaining.Value;
            if (eta.TotalHours >= 1)
                return $"{eta.Hours}h {eta.Minutes}m remaining";
            if (eta.TotalMinutes >= 1)
                return $"{eta.Minutes}m {eta.Seconds}s remaining";
            return $"{eta.Seconds}s remaining";
        }
    }

    public bool CanPause => State == DownloadState.Downloading || State == DownloadState.Queued;
    public bool CanResume => State == DownloadState.Paused || State == DownloadState.Cancelled || State == DownloadState.Failed;
    public bool CanCancel => State == DownloadState.Downloading || State == DownloadState.Queued || State == DownloadState.Paused;
    public bool CanRetry => State == DownloadState.Failed || State == DownloadState.Cancelled;
    public bool CanOpenFile => State == DownloadState.Completed && File.Exists(DestinationPath);

    public DownloadItemViewModel(DownloadItem model)
    {
        Model = model;
        _state = model.State;
        _bytesDownloaded = model.BytesDownloaded;
        _totalBytes = model.TotalBytes;
        _errorMessage = model.ErrorMessage;
    }

    public void UpdateProgress(long bytesDownloaded, long? totalBytes, double speed, TimeSpan? eta)
    {
        BytesDownloaded = bytesDownloaded;
        if (totalBytes.HasValue) TotalBytes = totalBytes;
        BytesPerSecond = speed;
        EstimatedRemaining = eta;
    }

    public void UpdateState(DownloadState state, string? error)
    {
        State = state;
        ErrorMessage = error;
        if (state != DownloadState.Downloading)
        {
            BytesPerSecond = 0;
            EstimatedRemaining = null;
        }
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes <= 0) return "0 B";
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}
