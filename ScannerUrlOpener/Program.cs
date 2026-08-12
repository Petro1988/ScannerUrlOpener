using ScannerUrlOpener.Application;
using ScannerUrlOpener.Configuration;
using ScannerUrlOpener.Services;

namespace ScannerUrlOpener;

internal static class Program
{
    private const string ApplicationMutexName =
        "ScannerUrlOpener.B7D8A66C-3F62-4CF8-91D8-78E84903EB43";

    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        using SingleInstanceGuard singleInstanceGuard =
            new(ApplicationMutexName);

        if (!singleInstanceGuard.IsPrimaryInstance)
        {
            MessageBox.Show(
                "Scanner URL Opener wird bereits ausgeführt."
                + Environment.NewLine
                + Environment.NewLine
                + "Prüfen Sie das Symbol neben der Windows-Uhr.",
                "Scanner URL Opener",

                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            return;
        }

        ScannerSettings settings = new();

        try
        {
            settings.Validate();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                "Die Programmeinstellungen sind ungültig."
                + Environment.NewLine
                + Environment.NewLine
                + exception.Message,
                "Scanner URL Opener",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);

            return;
        }

        AppLogger logger = new(
            settings.MaximumLogFileSizeBytes);

        try
        {
            logger.Info("Anwendung wird gestartet.");

            using ScannerApplicationContext context = new(
                settings,
                logger);

            System.Windows.Forms.Application.Run(context);
        }
        catch (Exception exception)
        {
            logger.Error(
                "Die Anwendung konnte nicht gestartet werden.",
                exception);

            MessageBox.Show(
                "Die Anwendung konnte nicht gestartet werden."
                + Environment.NewLine
                + Environment.NewLine
                + exception.Message,
                "Scanner URL Opener",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            logger.Info("Anwendungsprozess wurde beendet.");
        }
    }
}