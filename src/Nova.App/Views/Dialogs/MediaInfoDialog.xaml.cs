using System.Windows;
using Nova.Core.Common;
using Nova.Core.Models;

namespace Nova.App.Views.Dialogs;

public partial class MediaInfoDialog : Window
{
    public MediaInfoDialog(MediaDetails details)
    {
        InitializeComponent();
        PopulateDetails(details);
    }

    private void PopulateDetails(MediaDetails details)
    {
        TxtFileName.Text = string.IsNullOrWhiteSpace(details.FileName) ? "Unknown" : details.FileName;
        TxtFilePath.Text = string.IsNullOrWhiteSpace(details.FilePath) ? "Unknown" : details.FilePath;
        TxtContainer.Text = string.IsNullOrWhiteSpace(details.Container) ? "Unknown" : details.Container;
        TxtDuration.Text = details.DurationMs > 0 ? TimeFormatter.FormatMilliseconds(details.DurationMs) : "Unknown / Live";

        // Video
        TxtVideoCodec.Text = string.IsNullOrWhiteSpace(details.VideoCodec) ? "None / Audio-Only" : details.VideoCodec;
        TxtResolution.Text = details.ResolutionDisplay;
        TxtFrameRate.Text = details.FrameRateDisplay;
        TxtVideoBitrate.Text = details.VideoBitrate.HasValue && details.VideoBitrate.Value > 0
            ? $"{details.VideoBitrate.Value:N0} kbps"
            : "N/A";

        // Audio
        TxtAudioCodec.Text = string.IsNullOrWhiteSpace(details.AudioCodec) ? "None / Video-Only" : details.AudioCodec;
        TxtAudioChannels.Text = details.AudioChannelsDisplay;
        TxtAudioSampleRate.Text = details.AudioSampleRateDisplay;

        // Track Lists
        LstAudioTracks.Items.Clear();
        if (details.AudioTracks.Count > 0)
        {
            foreach (var track in details.AudioTracks)
            {
                LstAudioTracks.Items.Add(track.ToString());
            }
        }
        else
        {
            LstAudioTracks.Items.Add("No audio tracks found");
        }

        LstSubtitleTracks.Items.Clear();
        if (details.SubtitleTracks.Count > 0)
        {
            foreach (var track in details.SubtitleTracks)
            {
                LstSubtitleTracks.Items.Add(track.ToString());
            }
        }
        else
        {
            LstSubtitleTracks.Items.Add("No subtitle tracks found");
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
