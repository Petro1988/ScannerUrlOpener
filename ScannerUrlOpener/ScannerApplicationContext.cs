namespace ScannerUrlOpener;

internal sealed class ScannerApplicationContext : ApplicationContext
{
    private readonly ScannerSettings _settings;
    private readonly AppLogger _logger;
    private readonly UrlLauncher _urlLauncher;
    private readonly NotifyIcon _notifyIcon;
    private readonly DuplicateScanGuard _duplicateScanGuard;
    private readonly RawInputWindow _rawInputWindow;
    private readonly ScannerDeviceMatcher _scannerDeviceMatcher;
    private readonly RawInputScannerReader _rawInputScannerReader;

    private readonly ScannerDeviceConfigurationStore
        _scannerConfigurationStore;

    private readonly ScannerDeviceConfiguration
        _scannerConfiguration;

    private readonly HashSet<string> _detectedDeviceNames =
        new(StringComparer.OrdinalIgnoreCase);

    private ToolStripMenuItem? _statusItem;
    private bool _isLearningScanner;



    public ScannerApplicationContext(
    ScannerSettings settings,
    AppLogger logger)
    {
        _settings = settings
            ?? throw new ArgumentNullException(nameof(settings));

        _logger = logger
            ?? throw new ArgumentNullException(nameof(logger));

        _urlLauncher = new UrlLauncher(_logger);

        _scannerConfigurationStore = new ScannerDeviceConfigurationStore();

        _scannerConfiguration =
            _scannerConfigurationStore.Load();

        _scannerDeviceMatcher =
            new ScannerDeviceMatcher();

        _scannerDeviceMatcher.LoadIdentifier(
            _scannerConfiguration.DeviceIdentifier);

        _rawInputScannerReader = new RawInputScannerReader(
            _settings,
            _scannerDeviceMatcher,
            _logger);

        _rawInputScannerReader.ScanCompleted +=
            RawInputScannerReader_ScanCompleted;

        _duplicateScanGuard = new DuplicateScanGuard(
            TimeSpan.FromMilliseconds(
                _settings.DuplicateBlockingPeriodMilliseconds));

        ContextMenuStrip menu = CreateContextMenu();

        _notifyIcon = new NotifyIcon
        {
            Icon = Icon.ExtractAssociatedIcon(
                       Application.ExecutablePath)
                   ?? SystemIcons.Application,

            Text = "Scanner URL Opener",
            Visible = true,
            ContextMenuStrip = menu
        };

        _rawInputWindow = new RawInputWindow();
        _rawInputWindow.KeyReceived +=
            RawInputWindow_KeyReceived;

        _logger.Info(
            "Raw-Input-Scannererkennung wurde erfolgreich gestartet.");

        _notifyIcon.ShowBalloonTip(
            2000,
            "Scanner URL Opener",
            "Die Anwendung wartet auf einen HTTP- oder HTTPS-Scan.",
            ToolTipIcon.Info);

        UpdateScannerStatus();
    }

    private ContextMenuStrip CreateContextMenu()
    {
        ContextMenuStrip menu = new();

        _statusItem = new ToolStripMenuItem(
            "Warte auf Scanner...")
        {
            Enabled = false
        };

        ToolStripMenuItem testItem = new(
            "LOGINventory WebViewer öffnen");

        testItem.Click += TestItem_Click;

        ToolStripMenuItem logItem = new(
            "Protokolldatei öffnen");

        logItem.Click += LogItem_Click;

        ToolStripMenuItem exitItem = new("Beenden");
        exitItem.Click += ExitItem_Click;

        ToolStripMenuItem learnScannerItem = new("Scanner anlernen");

        learnScannerItem.Click += LearnScannerItem_Click;

        ToolStripMenuItem forgetScannerItem =
            new("Gespeicherten Scanner entfernen");

        forgetScannerItem.Click += ForgetScannerItem_Click;

        menu.Items.Add(_statusItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(testItem);
        menu.Items.Add(logItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(learnScannerItem);
        menu.Items.Add(forgetScannerItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);

        return menu;
    }

    private void TestItem_Click(object? sender, EventArgs e)
    {
        if (!UrlValidator.TryValidate(
        _settings.TestUrl,
        out Uri? validatedUrl))
        {
            _logger.Warning(
                "Die konfigurierte LOGINventory-Adresse ist ungültig.");

            MessageBox.Show(
                "Die Adresse ist ungültig.",
                "Scanner URL Opener",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);

            return;
        }

        _logger.Info("LOGINventory WebViewer wurde über das Menü geöffnet.");

        _urlLauncher.OpenUrl(validatedUrl);
    }

    private void ExitItem_Click(object? sender, EventArgs e)
    {
        ExitThread();
    }

    protected override void ExitThreadCore()
    {
        _logger.Info("Anwendung wird beendet.");

        _rawInputScannerReader.ScanCompleted -=
            RawInputScannerReader_ScanCompleted;

        _rawInputScannerReader.Dispose();

        _rawInputWindow.KeyReceived -=
            RawInputWindow_KeyReceived;

        _rawInputWindow.Dispose();

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();

        base.ExitThreadCore();
    }

    private void LogItem_Click(object? sender, EventArgs e)
    {
        try
        {
            if (!File.Exists(_logger.LogFilePath))
            {
                _logger.Info(
                    "Protokolldatei wurde über das Menü angefordert.");
            }

            using System.Diagnostics.Process? process =
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = _logger.LogFilePath,
                        UseShellExecute = true
                    });
        }
        catch (Exception exception)
        {
            _logger.Error(
                "Die Protokolldatei konnte nicht geöffnet werden.",
                exception);

            MessageBox.Show(
                $"Die Protokolldatei konnte nicht geöffnet werden.\n\n" +
                exception.Message,
                "Scanner URL Opener",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void RawInputWindow_KeyReceived(
    object? sender,
    RawInputKeyEventArgs e)
    {
        LogDeviceOnce(e.DeviceName);

        if (_isLearningScanner)
        {
            LearnScanner(e.DeviceName);
        }

        if (!_scannerDeviceMatcher.IsScanner(e.DeviceName))
        {
            return;
        }

        if (_statusItem != null &&
            _statusItem.Text != "Scanner erkannt")
        {
            _statusItem.Text = "Scanner erkannt";
        }

        _rawInputScannerReader.ProcessKey(e);
    }

    private void LogDeviceOnce(string deviceName)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
        {
            return;
        }

        if (!_detectedDeviceNames.Add(deviceName))
        {
            return;
        }

        bool isConfiguredScanner =
            _scannerDeviceMatcher.IsScanner(deviceName);

        string deviceType = isConfiguredScanner
            ? "Konfigurierter Scanner"
            : "Anderes Eingabegerät";

        _logger.Info(
            $"{deviceType} erkannt: {deviceName}");
    }

    private void RawInputScannerReader_ScanCompleted(
    object? sender,
    string scannedValue)
    {
        _logger.Info(
            "Raw Input hat einen vollständigen Scan erkannt. " +
            $"Zeichenanzahl: {scannedValue.Length}");

        if (!UrlValidator.TryValidate(
                scannedValue,
                out Uri? validatedUrl))
        {
            _logger.Warning(
                "Der Scan wurde verworfen, weil keine gültige " +
                "HTTP-/HTTPS-Adresse erkannt wurde.");

            return;
        }

        string normalizedUrl = validatedUrl.AbsoluteUri;

        if (_duplicateScanGuard.IsDuplicate(normalizedUrl))
        {
            _logger.Info(
                "Ein doppelter Raw-Input-Scan wurde innerhalb " +
                "der Sperrzeit ignoriert.");

            return;
        }

        bool opened = _urlLauncher.OpenUrl(validatedUrl);

        if (!opened)
        {
            return;
        }

        _notifyIcon.ShowBalloonTip(
            _settings.NotificationDurationMilliseconds,
            "Link geöffnet",
            normalizedUrl,
            ToolTipIcon.Info);
    }

    private void LearnScannerItem_Click(
    object? sender,
    EventArgs e)
    {
        _isLearningScanner = true;

        if (_statusItem != null)
        {
            _statusItem.Text =
                "Bitte jetzt einen Barcode scannen...";
        }

        _logger.Info(
            "Scanner-Anlernmodus wurde gestartet.");

        _notifyIcon.ShowBalloonTip(
            3000,
            "Scanner anlernen",
            "Bitte jetzt einen Barcode mit dem gewünschten Scanner scannen.",
            ToolTipIcon.Info);
    }

    private void LearnScanner(string deviceName)
    {
        if (!_isLearningScanner ||
            string.IsNullOrWhiteSpace(deviceName))
        {
            return;
        }

        _scannerDeviceMatcher.Configure(deviceName);

        _scannerConfiguration.DeviceIdentifier =
            _scannerDeviceMatcher.DeviceIdentifier;

        _scannerConfigurationStore.Save(
            _scannerConfiguration);

        _isLearningScanner = false;

        if (_statusItem != null)
        {
            _statusItem.Text = "Scanner erkannt";
        }

        _logger.Info(
            "Scanner wurde angelernt: " +
            _scannerDeviceMatcher.DeviceIdentifier);

        _notifyIcon.ShowBalloonTip(
            2500,
            "Scanner gespeichert",
            "Der Scanner wurde erfolgreich erkannt und gespeichert.",
            ToolTipIcon.Info);
    }

    private void ForgetScannerItem_Click(
    object? sender,
    EventArgs e)
    {
        _scannerDeviceMatcher.Clear();

        _scannerConfiguration.DeviceIdentifier = null;

        _scannerConfigurationStore.Delete();

        _isLearningScanner = false;

        UpdateScannerStatus();

        _logger.Info(
            "Die gespeicherte Scannerkonfiguration wurde entfernt.");

        _notifyIcon.ShowBalloonTip(
            2000,
            "Scanner entfernt",
            "Ein anderer Scanner kann jetzt angelernt werden.",
            ToolTipIcon.Info);
    }

    private void UpdateScannerStatus()
    {
        if (_statusItem == null)
        {
            return;
        }

        _statusItem.Text =
            _scannerDeviceMatcher.IsConfigured
                ? "Scanner konfiguriert"
                : "Kein Scanner konfiguriert";
    }
}
