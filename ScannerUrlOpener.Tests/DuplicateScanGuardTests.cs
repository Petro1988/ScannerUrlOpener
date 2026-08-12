using ScannerUrlOpener.Input;

namespace ScannerUrlOpener.Tests;

public sealed class DuplicateScanGuardTests
{
    [Fact]
    public void IsDuplicate_FirstScan_ReturnsFalse()
    {
        DuplicateScanGuard guard = new(
            TimeSpan.FromSeconds(2));

        bool result = guard.IsDuplicate(
            "http://inventory/asset/3");

        Assert.False(result);
    }

    [Fact]
    public void IsDuplicate_SameUrlImmediately_ReturnsTrue()
    {
        DuplicateScanGuard guard = new(
            TimeSpan.FromSeconds(2));

        const string url =
            "http://inventory/asset/3";

        bool firstResult = guard.IsDuplicate(url);
        bool secondResult = guard.IsDuplicate(url);

        Assert.False(firstResult);
        Assert.True(secondResult);
    }

    [Fact]
    public void IsDuplicate_DifferentUrls_ReturnsFalse()
    {
        DuplicateScanGuard guard = new(
            TimeSpan.FromSeconds(2));

        bool firstResult = guard.IsDuplicate(
            "http://inventory/asset/3");

        bool secondResult = guard.IsDuplicate(
            "http://inventory/asset/4");

        Assert.False(firstResult);
        Assert.False(secondResult);
    }

    [Fact]
    public void IsDuplicate_UsesCaseInsensitiveComparison()
    {
        DuplicateScanGuard guard = new(
            TimeSpan.FromSeconds(2));

        bool firstResult = guard.IsDuplicate(
            "http://inventory/LOGINventory/Details.aspx");

        bool secondResult = guard.IsDuplicate(
            "http://inventory/loginventory/details.aspx");

        Assert.False(firstResult);
        Assert.True(secondResult);
    }

    [Fact]
    public void Constructor_WithInvalidBlockingPeriod_ThrowsException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new DuplicateScanGuard(TimeSpan.Zero));
    }
}