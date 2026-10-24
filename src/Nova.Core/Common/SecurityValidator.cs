using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace Nova.Core.Common;

public static class SecurityValidator
{
    private static readonly HashSet<string> AllowedSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        Uri.UriSchemeHttp,
        Uri.UriSchemeHttps
    };

    private static readonly HashSet<string> WindowsReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    /// <summary>
    /// Validates URL scheme and ensures no embedded user credentials (user:pass).
    /// </summary>
    public static Result<Uri> ValidateUrlStructure(string? urlString)
    {
        if (string.IsNullOrWhiteSpace(urlString))
        {
            return Result<Uri>.Failure("URL cannot be empty.");
        }

        var trimmed = urlString.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            return Result<Uri>.Failure("Malformed URL format.");
        }

        if (!AllowedSchemes.Contains(uri.Scheme))
        {
            return Result<Uri>.Failure($"Unsupported scheme '{uri.Scheme}'. Only HTTP and HTTPS are permitted.");
        }

        if (string.IsNullOrWhiteSpace(uri.Host))
        {
            return Result<Uri>.Failure("URL host cannot be empty.");
        }

        // Security requirement: Reject embedded credentials in URLs
        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            return Result<Uri>.Failure("URLs with embedded user credentials (user:password@host) are prohibited.");
        }

        return Result<Uri>.Success(uri);
    }

    /// <summary>
    /// Validates IP address against loopback, private, link-local, multicast, and reserved ranges.
    /// </summary>
    public static bool IsProhibitedIpAddress(IPAddress ipAddress)
    {
        // Normalize IPv4-mapped IPv6 address (e.g. ::ffff:192.168.1.1)
        if (ipAddress.IsIPv4MappedToIPv6)
        {
            ipAddress = ipAddress.MapToIPv4();
        }

        if (ipAddress.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = ipAddress.GetAddressBytes();

            // 0.0.0.0/8 (Current network)
            if (bytes[0] == 0) return true;

            // 127.0.0.0/8 (Loopback)
            if (bytes[0] == 127) return true;

            // 10.0.0.0/8 (Private)
            if (bytes[0] == 10) return true;

            // 172.16.0.0/12 (Private: 172.16.0.0 - 172.31.255.255)
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return true;

            // 192.168.0.0/16 (Private)
            if (bytes[0] == 192 && bytes[1] == 168) return true;

            // 169.254.0.0/16 (Link-Local)
            if (bytes[0] == 169 && bytes[1] == 254) return true;

            // 100.64.0.0/10 (Carrier-Grade NAT: 100.64.0.0 - 100.127.255.255)
            if (bytes[0] == 100 && (bytes[1] & 0xC0) == 64) return true;

            // 192.0.2.0/24, 198.51.100.0/24, 203.0.113.0/24 (Documentation / Test-Net)
            if (bytes[0] == 192 && bytes[1] == 0 && bytes[2] == 2) return true;
            if (bytes[0] == 198 && bytes[1] == 51 && bytes[2] == 100) return true;
            if (bytes[0] == 203 && bytes[1] == 0 && bytes[2] == 113) return true;

            // 198.18.0.0/15 (Benchmarking)
            if (bytes[0] == 198 && (bytes[1] & 0xFE) == 18) return true;

            // 224.0.0.0/4 (Multicast: 224.0.0.0 - 239.255.255.255)
            if (bytes[0] >= 224 && bytes[0] <= 239) return true;

            // 240.0.0.0/4 (Reserved) & 255.255.255.255 (Broadcast)
            if (bytes[0] >= 240) return true;

            return false;
        }

        if (ipAddress.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (IPAddress.IPv6Loopback.Equals(ipAddress)) return true;
            if (IPAddress.IPv6None.Equals(ipAddress)) return true;
            if (IPAddress.IPv6Any.Equals(ipAddress)) return true;

            var bytes = ipAddress.GetAddressBytes();

            // fe80::/10 (Link-Local)
            if (bytes[0] == 0xFE && (bytes[1] & 0xC0) == 0x80) return true;

            // fc00::/7 (Unique Local Address: fc00:: - fdff::)
            if ((bytes[0] & 0xFE) == 0xFC) return true;

            // ff00::/8 (Multicast)
            if (bytes[0] == 0xFF) return true;

            // 2001:db8::/32 (Documentation)
            if (bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] == 0x0D && bytes[3] == 0xB8) return true;

            return false;
        }

        return true;
    }

    /// <summary>
    /// Resolves DNS and validates that resolved IP addresses are not restricted/private (SSRF guard).
    /// </summary>
    public static async Task<Result<IPAddress[]>> ValidateDnsAndIpAsync(string host, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return Result<IPAddress[]>.Failure("Invalid host name.");
        }

        try
        {
            // If host is already an IP address literal
            if (IPAddress.TryParse(host, out var parsedIp))
            {
                if (IsProhibitedIpAddress(parsedIp))
                {
                    return Result<IPAddress[]>.Failure($"Direct access to restricted IP address '{parsedIp}' is prohibited (SSRF prevention).");
                }
                return Result<IPAddress[]>.Success([parsedIp]);
            }

            var addresses = await Dns.GetHostAddressesAsync(host, ct);
            if (addresses == null || addresses.Length == 0)
            {
                return Result<IPAddress[]>.Failure($"Could not resolve host '{host}'.");
            }

            foreach (var addr in addresses)
            {
                if (IsProhibitedIpAddress(addr))
                {
                    return Result<IPAddress[]>.Failure($"Host '{host}' resolves to restricted IP address '{addr}' (SSRF prevention).");
                }
            }

            return Result<IPAddress[]>.Success(addresses);
        }
        catch (SocketException ex)
        {
            return Result<IPAddress[]>.Failure($"DNS resolution failed for '{host}': {ex.Message}");
        }
        catch (Exception ex)
        {
            return Result<IPAddress[]>.Failure($"Network validation error for '{host}': {ex.Message}");
        }
    }

    /// <summary>
    /// Sanitizes file names to prevent directory traversal and remove illegal NTFS / filesystem characters.
    /// </summary>
    public static string SanitizeFileName(string? rawName, string defaultExtension = ".bin")
    {
        if (string.IsNullOrWhiteSpace(rawName))
        {
            return $"download_{Guid.NewGuid():N}{defaultExtension}";
        }

        // Strip path directories if provided (e.g. ../../test.mp4 -> test.mp4)
        var fileName = Path.GetFileName(rawName);

        // Remove invalid characters
        var invalidChars = Path.GetInvalidFileNameChars();
        var cleanChars = fileName.Where(c => !invalidChars.Contains(c) && c != '/' && c != '\\').ToArray();
        var sanitized = new string(cleanChars).Trim();

        // Trim trailing periods and spaces which are invalid in Windows filenames
        sanitized = sanitized.TrimEnd('.', ' ');

        // Check Windows reserved names (CON, NUL, AUX, PRN, COM1-9, LPT1-9)
        var baseWithoutExt = Path.GetFileNameWithoutExtension(sanitized);
        if (WindowsReservedNames.Contains(baseWithoutExt) || string.IsNullOrWhiteSpace(sanitized))
        {
            var ext = Path.GetExtension(sanitized);
            if (string.IsNullOrEmpty(ext)) ext = defaultExtension;
            sanitized = $"file_{Guid.NewGuid():N}{ext}";
        }

        return sanitized;
    }

    /// <summary>
    /// Verifies that a target file path remains safely within the designated destination directory.
    /// </summary>
    public static bool VerifyPathContainment(string destinationDir, string targetPath)
    {
        try
        {
            var fullDest = Path.GetFullPath(destinationDir);
            var fullTarget = Path.GetFullPath(targetPath);

            var destWithSep = fullDest.EndsWith(Path.DirectorySeparatorChar.ToString())
                ? fullDest
                : fullDest + Path.DirectorySeparatorChar;

            return fullTarget.StartsWith(destWithSep, StringComparison.OrdinalIgnoreCase) ||
                   fullTarget.Equals(fullDest, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Computes a unique, non-colliding file path in the target directory (e.g., file (1).mp4).
    /// </summary>
    public static string GetUniqueFilePath(string destinationDir, string fileName)
    {
        var sanitizedName = SanitizeFileName(fileName);
        var targetPath = Path.Combine(destinationDir, sanitizedName);

        if (!File.Exists(targetPath) && !File.Exists($"{targetPath}.part"))
        {
            return targetPath;
        }

        var nameWithoutExt = Path.GetFileNameWithoutExtension(sanitizedName);
        var ext = Path.GetExtension(sanitizedName);

        int counter = 1;
        while (counter < 10000)
        {
            var candidate = Path.Combine(destinationDir, $"{nameWithoutExt} ({counter}){ext}");
            if (!File.Exists(candidate) && !File.Exists($"{candidate}.part"))
            {
                return candidate;
            }
            counter++;
        }

        return Path.Combine(destinationDir, $"{nameWithoutExt}_{Guid.NewGuid():N}{ext}");
    }
}
