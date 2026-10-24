namespace Nova.Core.Common;

public static class UrlValidator
{
    private static readonly HashSet<string> AllowedSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        Uri.UriSchemeHttp,
        Uri.UriSchemeHttps
    };

    public static Result<Uri> ValidateMediaUrl(string? urlString)
    {
        if (string.IsNullOrWhiteSpace(urlString))
        {
            return Result<Uri>.Failure("Network URL cannot be empty.");
        }

        var trimmed = urlString.Trim();

        // Guard against scheme manipulation / file path injection
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            return Result<Uri>.Failure("Malformed URL format.");
        }

        if (!AllowedSchemes.Contains(uri.Scheme))
        {
            return Result<Uri>.Failure($"Unsupported protocol '{uri.Scheme}'. Only HTTP and HTTPS are permitted.");
        }

        if (string.IsNullOrWhiteSpace(uri.Host))
        {
            return Result<Uri>.Failure("URL must contain a valid host name.");
        }

        return Result<Uri>.Success(uri);
    }

    public static bool IsHlsStream(string url)
    {
        return url.Contains(".m3u8", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsDashStream(string url)
    {
        return url.Contains(".mpd", StringComparison.OrdinalIgnoreCase);
    }
}
