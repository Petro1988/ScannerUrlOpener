using System.Text;

namespace ScannerUrlOpener;

internal sealed class AppLogger
{
    private const string CurrentLogFileName =
        "ScannerUrlOpener.log";

    private const string ArchivedLogFileName =
        "ScannerUrlOpener-old.log";

    private readonly object _syncRoot = new();

    private readonly string _logFilePath;
    private readonly string _archivedLogFilePath;
    private readonly long _maximumLogFileSizeBytes;

    public AppLogger(long maximumLogFileSizeBytes)
    {
        if (maximumLogFileSizeBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumLogFileSizeBytes),
                "Die maximale Logdateigröße muss größer als 0 sein.");
        }

        _maximumLogFileSizeBytes =
            maximumLogFileSizeBytes;

        string logDirectory = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "ScannerUrlOpener");

        Directory.CreateDirectory(logDirectory);

        _logFilePath = Path.Combine(
            logDirectory,
            CurrentLogFileName);

        _archivedLogFilePath = Path.Combine(
            logDirectory,
            ArchivedLogFileName);
    }

    public string LogFilePath => _logFilePath;

    public string ArchivedLogFilePath =>
        _archivedLogFilePath;

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

    public void Error(
        string message,
        Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        WriteLog(
            "FEHLER",
            $"{message} | " +
            $"{exception.GetType().Name}: " +
            exception.Message);
    }

    private void WriteLog(
        string level,
        string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        string logEntry =
            $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} " +
            $"[{level}] {message}" +
            Environment.NewLine;

        try
        {
            lock (_syncRoot)
            {
                RotateLogIfNecessary(logEntry);

                File.AppendAllText(
                    _logFilePath,
                    logEntry,
                    Encoding.UTF8);
            }
        }
        catch
        {
            /*
             * Ein Loggerfehler darf die Anwendung nicht beenden.
             *
             * An dieser Stelle darf nicht erneut über AppLogger
             * protokolliert werden, weil sonst eine Endlosschleife
             * entstehen könnte.
             */
        }
    }

    private void RotateLogIfNecessary(
        string nextLogEntry)
    {
        if (!File.Exists(_logFilePath))
        {
            return;
        }

        long currentFileSize =
            new FileInfo(_logFilePath).Length;

        int nextEntrySize =
            Encoding.UTF8.GetByteCount(nextLogEntry);

        long expectedFileSize =
            currentFileSize + nextEntrySize;

        if (expectedFileSize <=
            _maximumLogFileSizeBytes)
        {
            return;
        }

        if (File.Exists(_archivedLogFilePath))
        {
            File.Delete(_archivedLogFilePath);
        }

        File.Move(
            _logFilePath,
            _archivedLogFilePath);
    }
}