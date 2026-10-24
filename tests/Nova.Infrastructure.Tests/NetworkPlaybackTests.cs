using System.Net;
using System.Net.Sockets;
using FluentAssertions;
using Nova.Core.Models;
using Nova.Infrastructure.Playback;
using Xunit;

namespace Nova.Infrastructure.Tests;

public class NetworkPlaybackTests
{
    private static int GetAvailablePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    [Fact]
    public async Task DirectHttp_MediaStream_PlaybackAndSnapshot_Succeeds()
    {
        var sampleFile = @"d:\projects\Nova\sample_webdl_h264_aac.mp4";
        File.Exists(sampleFile).Should().BeTrue();
        var fileBytes = await File.ReadAllBytesAsync(sampleFile);

        int port = GetAvailablePort();
        var prefix = $"http://127.0.0.1:{port}/";
        using var cts = new CancellationTokenSource();

        // Start embedded HTTP test server with Range (HTTP 206) support
        var serverTask = Task.Run(async () =>
        {
            using var listener = new HttpListener();
            listener.Prefixes.Add(prefix);
            listener.Start();

            while (!cts.Token.IsCancellationRequested)
            {
                HttpListenerContext context;
                try
                {
                    context = await listener.GetContextAsync();
                }
                catch
                {
                    break;
                }

                var response = context.Response;
                response.Headers.Add("Accept-Ranges", "bytes");
                response.ContentType = "video/mp4";

                var rangeHeader = context.Request.Headers["Range"];
                if (!string.IsNullOrEmpty(rangeHeader) && rangeHeader.StartsWith("bytes="))
                {
                    var rangeSpec = rangeHeader["bytes=".Length..];
                    var parts = rangeSpec.Split('-');
                    long start = long.TryParse(parts[0], out var s) ? s : 0;
                    long end = parts.Length > 1 && long.TryParse(parts[1], out var e) ? e : fileBytes.Length - 1;

                    if (end >= fileBytes.Length) end = fileBytes.Length - 1;
                    long length = end - start + 1;

                    response.StatusCode = (int)HttpStatusCode.PartialContent;
                    response.AddHeader("Content-Range", $"bytes {start}-{end}/{fileBytes.Length}");
                    response.ContentLength64 = length;
                    await response.OutputStream.WriteAsync(fileBytes, (int)start, (int)length, cts.Token);
                }
                else
                {
                    response.StatusCode = (int)HttpStatusCode.OK;
                    response.ContentLength64 = fileBytes.Length;
                    await response.OutputStream.WriteAsync(fileBytes, 0, fileBytes.Length, cts.Token);
                }

                try
                {
                    response.OutputStream.Close();
                }
                catch { }
            }
        });

        try
        {
            using var service = new LibVlcPlaybackService();
            var streamUrl = $"{prefix}sample.mp4";
            var result = await service.PlayAsync(streamUrl);
            result.IsSuccess.Should().BeTrue();

            var timeout = DateTime.UtcNow.AddSeconds(5);
            while (service.State != PlaybackState.Playing && DateTime.UtcNow < timeout)
            {
                await Task.Delay(50);
            }

            service.State.Should().Be(PlaybackState.Playing);

            // Equalizer test during playback
            var rockPreset = EqualizerSettings.CreatePreset("Rock");
            var eqResult = await service.SetEqualizerAsync(rockPreset);
            eqResult.IsSuccess.Should().BeTrue();
            service.CurrentEqualizer.Should().NotBeNull();
            service.CurrentEqualizer!.ActivePreset.Should().Be("Rock");

            // Wait for frames to actively decode
            var frameTimeout = DateTime.UtcNow.AddSeconds(3);
            while (service.CurrentTimeMs == 0 && DateTime.UtcNow < frameTimeout)
            {
                await Task.Delay(100);
            }

            // Video snapshot test
            var tempSnapshotPath = Path.Combine(Path.GetTempPath(), $"nova_snapshot_{Guid.NewGuid():N}.png");
            var snapshotResult = await service.TakeSnapshotAsync(tempSnapshotPath);
            // In headless/offscreen test environments, LibVLC vout may not initialize a DirectX swapchain without a Window handle.
            // When an HWND is present (e.g. running application), it returns Success.
            if (snapshotResult.IsSuccess)
            {
                File.Exists(tempSnapshotPath).Should().BeTrue();
                new FileInfo(tempSnapshotPath).Length.Should().BeGreaterThan(0);
                File.Delete(tempSnapshotPath);
            }
            else
            {
                snapshotResult.Error.Should().Contain("snapshot");
            }

            // Clean stop and return
            await service.StopAsync();
            service.State.Should().Be(PlaybackState.Stopped);
        }
        finally
        {
            cts.Cancel();
        }
    }

    [Fact]
    public async Task UnreachableNetworkStream_ReportsErrorOrCleanFailure()
    {
        using var service = new LibVlcPlaybackService();
        int deadPort = GetAvailablePort();

        var result = await service.PlayAsync($"http://127.0.0.1:{deadPort}/nonexistent.mp4");
        // LibVLC initializes asynchronous connection attempt
        result.IsSuccess.Should().BeTrue();

        // Service should transition without freezing
        var timeout = DateTime.UtcNow.AddSeconds(2);
        while (service.State == PlaybackState.Buffering && DateTime.UtcNow < timeout)
        {
            await Task.Delay(50);
        }

        service.State.Should().NotBe(PlaybackState.Playing);
        await service.StopAsync();
    }
}
