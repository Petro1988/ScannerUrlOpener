using ScannerUrlOpener.Services;

namespace ScannerUrlOpener.Tests;

public sealed class UrlValidatorTests
{
    [Theory]
    [InlineData("http://inventory/LOGINventory/default.aspx")]
    [InlineData("https://inventory/LOGINventory/default.aspx")]
    [InlineData("http://inventory/LOGINventory/details.aspx?invnr=ASSET-3")]
    [InlineData("https://example.org/test")]
    public void TryValidate_WithAllowedUrl_ReturnsTrue(
        string value)
    {
        bool result = UrlValidator.TryValidate(
            value,
            out Uri? validatedUrl);

        Assert.True(result);
        Assert.NotNull(validatedUrl);
        Assert.True(
            validatedUrl.Scheme == Uri.UriSchemeHttp ||
            validatedUrl.Scheme == Uri.UriSchemeHttps);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ASSET-3")]
    [InlineData("C:\\Windows\\notepad.exe")]
    [InlineData("file:///C:/Test.pdf")]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://example.org/file")]
    public void TryValidate_WithDisallowedValue_ReturnsFalse(
        string value)
    {
        bool result = UrlValidator.TryValidate(
            value,
            out Uri? validatedUrl);

        Assert.False(result);
        Assert.Null(validatedUrl);
    }

    [Fact]
    public void TryValidate_WithNull_ReturnsFalse()
    {
        bool result = UrlValidator.TryValidate(
            null,
            out Uri? validatedUrl);

        Assert.False(result);
        Assert.Null(validatedUrl);
    }

    [Fact]
    public void TryValidate_RemovesLeadingAndTrailingSpaces()
    {
        const string value =
            "  http://inventory/LOGINventory/default.aspx  ";

        bool result = UrlValidator.TryValidate(
            value,
            out Uri? validatedUrl);

        Assert.True(result);
        Assert.NotNull(validatedUrl);
        Assert.Equal(
            "http://inventory/LOGINventory/default.aspx",
            validatedUrl.AbsoluteUri);
    }
}