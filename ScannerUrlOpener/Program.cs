namespace ScannerUrlOpener;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        AppLogger logger = new();

        try
        {
            logger.Info("Anwendung wird gestartet.");

            ScannerSettings settings = new();
            settings.Validate();

            using ScannerApplicationContext context = new(
                settings,
                logger);

            Application.Run(context);
        }
        catch (Exception exception)
        {
            logger.Error(
                "Die Anwendung konnte nicht gestartet werden.",
                exception);

            MessageBox.Show(
                $"Die Anwendung konnte nicht gestartet werden.\n\n" +
                exception.Message,
                "Scanner URL Opener",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}