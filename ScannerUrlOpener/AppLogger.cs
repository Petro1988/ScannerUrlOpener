using System.Text;

namespace ScannerUrlOpener;

internal sealed class AppLogger
{
    private readonly object _syncRoot = new();
    private readonly string _logFilePath;

    public AppLogger()
    {
        string logDirectory = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "ScannerUrlOpener");

        Directory.CreateDirectory(logDirectory);

        _logFilePath = Path.Combine(
            logDirectory,
            "ScannerUrlOpener.log");
    }

    public string LogFilePath => _logFilePath;

    public void Info(string message)
    {
        WriteLog("INFO", message);
    }

    public void Warning(string message)
    {
        WriteLog("WARNUNG", message);
    }

    public void Error(string message)
    {
        WriteLog("FEHLER", message);
    }

    public void Error(string message, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        WriteLog(
            "FEHLER",
            $"{message} | " +
            $"{exception.GetType().Name}: {exception.Message}");
    }

    private void WriteLog(string level, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        string logEntry =
            $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} " +
            $"[{level}] {message}{Environment.NewLine}";

        try
        {
            lock (_syncRoot)
            {
                File.AppendAllText(
                    _logFilePath,
                    logEntry,
                    Encoding.UTF8);
            }
        }
        catch
        {
            /*
             * Ein Fehler beim Schreiben der Logdatei darf die
             * Scanner-Anwendung nicht beenden.
             */
        }
    }
}