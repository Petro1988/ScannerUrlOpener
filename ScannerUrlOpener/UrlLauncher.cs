using System.Diagnostics;

namespace ScannerUrlOpener;

internal sealed class UrlLauncher
{
    private readonly AppLogger _logger;

    public UrlLauncher(AppLogger logger)
    {
        _logger = logger
            ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool OpenUrl(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url.AbsoluteUri,
                UseShellExecute = true
            });

            _logger.Info(
                $"Browser geöffnet: {CreateSafeLogValue(url)}");

            return true;
        }
        catch (Exception exception)
        {
            _logger.Error(
                $"Browser konnte nicht geöffnet werden: " +
                CreateSafeLogValue(url),
                exception);

            MessageBox.Show(
                $"Der Browser konnte nicht geöffnet werden.\n\n" +
                exception.Message,
                "Scanner URL Opener",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);

            return false;
        }
    }

    private static string CreateSafeLogValue(Uri url)
    {
        /*
         * Query-Parameter werden nicht protokolliert.
         * Dadurch landen beispielsweise Inventarnummern nicht
         * unnötig in der Logdatei.
         */
        return $"{url.Scheme}://{url.Host}{url.AbsolutePath}";
    }
}