using ScannerUrlOpener.Input;

namespace ScannerUrlOpener.Tests;

public sealed class ScannerDeviceMatcherTests
{
    private const string ScannerDevicePath =
        @"\\?\HID#VID_0461&PID_4D87&MI_00#123456";

    private const string KeyboardDevicePath =
        @"\\?\HID#VID_046D&PID_C534&MI_00#987654";

    [Fact]
    public void Configure_ExtractsVidAndPid()
    {
        ScannerDeviceMatcher matcher = new();

        matcher.Configure(ScannerDevicePath);

        Assert.True(matcher.IsConfigured);

        Assert.Equal(
            "VID_0461&PID_4D87",
            matcher.DeviceIdentifier);
    }

    [Fact]
    public void IsScanner_WithConfiguredDevice_ReturnsTrue()
    {
        ScannerDeviceMatcher matcher = new();

        matcher.Configure(ScannerDevicePath);

        bool result = matcher.IsScanner(
            ScannerDevicePath);

        Assert.True(result);
    }

    [Fact]
    public void IsScanner_WithDifferentDevice_ReturnsFalse()
    {
        ScannerDeviceMatcher matcher = new();

        matcher.Configure(ScannerDevicePath);

        bool result = matcher.IsScanner(
            KeyboardDevicePath);

        Assert.False(result);
    }

    [Fact]
    public void IsScanner_WithoutConfiguration_ReturnsFalse()
    {
        ScannerDeviceMatcher matcher = new();

        bool result = matcher.IsScanner(
            ScannerDevicePath);

        Assert.False(result);
    }

    [Fact]
    public void LoadIdentifier_LoadsSavedIdentifier()
    {
        ScannerDeviceMatcher matcher = new();

        matcher.LoadIdentifier(
            "VID_0461&PID_4D87");

        Assert.True(matcher.IsConfigured);
        Assert.True(
            matcher.IsScanner(ScannerDevicePath));
    }

    [Fact]
    public void Clear_RemovesConfiguration()
    {
        ScannerDeviceMatcher matcher = new();

        matcher.Configure(ScannerDevicePath);
        matcher.Clear();

        Assert.False(matcher.IsConfigured);
        Assert.Null(matcher.DeviceIdentifier);
        Assert.False(
            matcher.IsScanner(ScannerDevicePath));
    }

    [Fact]
    public void Configure_WithEmptyDeviceName_ThrowsException()
    {
        ScannerDeviceMatcher matcher = new();

        Assert.Throws<ArgumentException>(
            () => matcher.Configure(string.Empty));
    }
}