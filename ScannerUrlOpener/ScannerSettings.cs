namespace ScannerUrlOpener;

internal sealed class ScannerSettings
{
    public int MaximumCharacterDelayMilliseconds { get; init; } = 1000;

    public int ScanCompletionDelayMilliseconds { get; init; } = 1000;

    public int MinimumScanLength { get; init; } = 10;

    public int MaximumScanLength { get; init; } = 2048;

    public int DuplicateBlockingPeriodMilliseconds { get; init; } = 2000;

    public int NotificationDurationMilliseconds { get; init; } = 1500;

    public string TestUrl { get; init; } = "http://inventory/LOGINventory/default.aspx";

    public void Validate()
    {
        if (MaximumCharacterDelayMilliseconds <= 0)
        {
            throw new InvalidOperationException(
                "MaximumCharacterDelayMilliseconds muss größer als 0 sein.");
        }

        if (ScanCompletionDelayMilliseconds <= 0)
        {
            throw new InvalidOperationException(
                "ScanCompletionDelayMilliseconds muss größer als 0 sein.");
        }

        if (MinimumScanLength <= 0)
        {
            throw new InvalidOperationException(
                "MinimumScanLength muss größer als 0 sein.");
        }

        if (MaximumScanLength < MinimumScanLength)
        {
            throw new InvalidOperationException(
                "MaximumScanLength darf nicht kleiner " +
                "als MinimumScanLength sein.");
        }

        if (DuplicateBlockingPeriodMilliseconds <= 0)
        {
            throw new InvalidOperationException(
                "DuplicateBlockingPeriodMilliseconds " +
                "muss größer als 0 sein.");
        }

        if (NotificationDurationMilliseconds <= 0)
        {
            throw new InvalidOperationException(
                "NotificationDurationMilliseconds " +
                "muss größer als 0 sein.");
        }

        if (string.IsNullOrWhiteSpace(TestUrl))
        {
            throw new InvalidOperationException(
                "TestUrl darf nicht leer sein.");
        }
    }
}