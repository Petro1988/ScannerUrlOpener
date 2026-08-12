namespace ScannerUrlOpener.Input;

internal sealed class DuplicateScanGuard
{
    private readonly TimeSpan _blockingPeriod;

    private string? _lastUrl;
    private DateTime _lastScanTime = DateTime.MinValue;

    public DuplicateScanGuard(TimeSpan blockingPeriod)
    {
        if (blockingPeriod <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(blockingPeriod),
                "Die Sperrzeit muss größer als null sein.");
        }

        _blockingPeriod = blockingPeriod;
    }

    public bool IsDuplicate(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        DateTime currentTime = DateTime.UtcNow;

        bool sameUrl = string.Equals(
            _lastUrl,
            url,
            StringComparison.OrdinalIgnoreCase);

        bool withinBlockingPeriod =
            currentTime - _lastScanTime < _blockingPeriod;

        if (sameUrl && withinBlockingPeriod)
        {
            return true;
        }

        _lastUrl = url;
        _lastScanTime = currentTime;

        return false;
    }
}
