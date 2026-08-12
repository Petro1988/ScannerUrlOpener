using System.Text.RegularExpressions;

namespace ScannerUrlOpener.Input;

internal sealed class ScannerDeviceMatcher
{
    private string? _deviceIdentifier;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_deviceIdentifier);

    public string? DeviceIdentifier =>
        _deviceIdentifier;

    public void Configure(string deviceName)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
        {
            throw new ArgumentException(
                "Der Gerätename darf nicht leer sein.",
                nameof(deviceName));
        }

        _deviceIdentifier =
            ExtractStableIdentifier(deviceName);
    }

    public void LoadIdentifier(string? deviceIdentifier)
    {
        _deviceIdentifier =
            string.IsNullOrWhiteSpace(deviceIdentifier)
                ? null
                : deviceIdentifier.Trim();
    }

    public void Clear()
    {
        _deviceIdentifier = null;
    }

    public bool IsScanner(string? deviceName)
    {
        if (!IsConfigured ||
            string.IsNullOrWhiteSpace(deviceName))
        {
            return false;
        }

        return deviceName.Contains(
            _deviceIdentifier!,
            StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractStableIdentifier(
        string deviceName)
    {
        Match match = Regex.Match(
            deviceName,
            @"VID_[0-9A-F]{4}&PID_[0-9A-F]{4}",
            RegexOptions.IgnoreCase);

        if (match.Success)
        {
            return match.Value.ToUpperInvariant();
        }

        /*
         * Falls das Gerät keine VID/PID liefert,
         * wird als Fallback der vollständige Gerätepfad verwendet.
         */
        return deviceName.Trim();
    }
}