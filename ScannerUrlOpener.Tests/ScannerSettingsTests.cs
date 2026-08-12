using ScannerUrlOpener.Configuration;

namespace ScannerUrlOpener.Tests;

public sealed class ScannerSettingsTests
{
    [Fact]
    public void Validate_WithDefaultSettings_DoesNotThrow()
    {
        ScannerSettings settings = new();

        Exception? exception =
            Record.Exception(settings.Validate);

        Assert.Null(exception);
    }

    [Fact]
    public void Validate_WithInvalidMaximumScanLength_ThrowsException()
    {
        ScannerSettings settings = new()
        {
            MinimumScanLength = 100,
            MaximumScanLength = 10
        };

        Assert.Throws<InvalidOperationException>(
            settings.Validate);
    }

    [Fact]
    public void Validate_WithInvalidLogSize_ThrowsException()
    {
        ScannerSettings settings = new()
        {
            MaximumLogFileSizeBytes = 0
        };

        Assert.Throws<InvalidOperationException>(
            settings.Validate);
    }

    [Fact]
    public void Validate_WithInvalidTestUrl_ThrowsException()
    {
        ScannerSettings settings = new()
        {
            TestUrl = "ASSET-3"
        };

        Assert.Throws<InvalidOperationException>(
            settings.Validate);
    }

    [Fact]
    public void Validate_WithValidHttpsTestUrl_DoesNotThrow()
    {
        ScannerSettings settings = new()
        {
            TestUrl = "https://example.org/test"
        };

        Exception? exception =
            Record.Exception(settings.Validate);

        Assert.Null(exception);
    }
}