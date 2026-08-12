namespace ScannerUrlOpener.Services;

internal static class UrlValidator
{
    public static bool TryValidate(
        string? value,
        out Uri? validatedUrl)
    {
        validatedUrl = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string trimmedValue = value.Trim();

        if (!Uri.TryCreate(
                trimmedValue,
                UriKind.Absolute,
                out Uri? parsedUrl))
        {
            return false;
        }

        if (!IsAllowedScheme(parsedUrl))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(parsedUrl.Host))
        {
            return false;
        }

        validatedUrl = parsedUrl;
        return true;
    }

    private static bool IsAllowedScheme(Uri url)
    {
        return url.Scheme.Equals(
                   Uri.UriSchemeHttp,
                   StringComparison.OrdinalIgnoreCase) ||
               url.Scheme.Equals(
                   Uri.UriSchemeHttps,
                   StringComparison.OrdinalIgnoreCase);
    }
}