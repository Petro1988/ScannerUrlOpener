using ScannerUrlOpener.Configuration;
using ScannerUrlOpener.Input;
using ScannerUrlOpener.Services;

namespace ScannerUrlOpener.Application;

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

    private readonly ScannerDeviceConfigurationStore _scannerConfigurationStore;

    private readonly ScannerDeviceConfiguration _scannerConfiguration;

    private readonly HashSet<string> _detectedDeviceNames = new(StringComparer.OrdinalIgnoreCase);

    private ToolStripMenuItem? _statusItem;
    private bool _isLearningScanner;
    private string? _learningCandidateDeviceName;

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
               System.Windows.Forms.Application.ExecutablePath)
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

    private void LogItem_Click(
    object? sender,
    EventArgs e)
    {
        try
        {
            if (!File.Exists(_logger.LogFilePath))
            {
                _logger.Info(
                    "Protokolldatei wurde über das Menü angefordert.");
            }

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
                "Die Protokolldatei konnte nicht geöffnet werden." +
                Environment.NewLine +
                Environment.NewLine +
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
            SelectLearningCandidate(e.DeviceName);
        }

        /*
         * Nur Eingaben des gespeicherten oder vorläufig
         * ausgewählten Geräts werden verarbeitet.
         */
        if (!_scannerDeviceMatcher.IsScanner(e.DeviceName))
        {
            return;
        }

        if (!_isLearningScanner &&
            _statusItem != null &&
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
            "Raw Input hat eine Eingabe abgeschlossen. " +
            $"Zeichenanzahl: {scannedValue.Length}");

        /*
         * Während des Anlernmodus wird der Scanner erst gespeichert,
         * wenn der vollständige Scan eine gültige HTTP-/HTTPS-URL ist.
         */
        if (_isLearningScanner)
        {
            CompleteScannerLearning(scannedValue);
            return;
        }

        ProcessNormalScan(scannedValue);
    }

    private void LearnScannerItem_Click(
    object? sender,
    EventArgs e)
    {
        /*
         * Eine eventuell vorher vorhandene vorläufige Auswahl
         * wird verworfen.
         */
        _learningCandidateDeviceName = null;

        /*
         * Der gespeicherte Scanner bleibt zunächst bestehen.
         * Der Matcher wird nur für den neuen Anlernversuch geleert.
         */
        _scannerDeviceMatcher.Clear();

        _isLearningScanner = true;

        if (_statusItem != null)
        {
            _statusItem.Text =
                "Bitte jetzt einen URL-Barcode scannen...";
        }

        _logger.Info(
            "Sicherer Scanner-Anlernmodus wurde gestartet.");

        _notifyIcon.ShowBalloonTip(
            4000,
            "Scanner anlernen",
            "Bitte jetzt einen vollständigen HTTP- oder HTTPS-Barcode scannen.",
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
        _isLearningScanner = false;
        _learningCandidateDeviceName = null;

        _scannerDeviceMatcher.Clear();

        _scannerConfiguration.DeviceIdentifier = null;
        _scannerConfigurationStore.Delete();

        UpdateScannerStatus();

        _logger.Info(
            "Die gespeicherte Scannerkonfiguration wurde entfernt.");

        _notifyIcon.ShowBalloonTip(
            2500,
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

    private void SelectLearningCandidate(string deviceName)
    {
        if (!_isLearningScanner ||
            string.IsNullOrWhiteSpace(deviceName))
        {
            return;
        }

        /*
         * Das erste Gerät des aktuellen Anlernversuchs wird nur
         * vorläufig ausgewählt. Es wird noch nicht gespeichert.
         */
        if (_learningCandidateDeviceName != null)
        {
            return;
        }

        _learningCandidateDeviceName = deviceName;

        /*
         * Der Matcher wird vorläufig konfiguriert, damit die weiteren
         * Zeichen dieses Geräts vom RawInputScannerReader verarbeitet
         * werden können.
         */
        _scannerDeviceMatcher.Configure(deviceName);

        _logger.Info(
            "Ein Eingabegerät wurde als vorläufiger " +
            "Scannerkandidat erkannt.");

        if (_statusItem != null)
        {
            _statusItem.Text =
                "Scan wird geprüft...";
        }
    }

    private void CompleteScannerLearning(
    string scannedValue)
    {
        if (!_isLearningScanner)
        {
            return;
        }

        bool hasMinimumLength =
            scannedValue.Length >= _settings.MinimumScanLength;

        bool isValidUrl = UrlValidator.TryValidate(
            scannedValue,
            out Uri? validatedUrl);

        if (!hasMinimumLength || !isValidUrl)
        {
            _logger.Warning(
                "Der Anlernversuch wurde verworfen. " +
                "Es wurde keine vollständige HTTP-/HTTPS-Adresse erkannt.");

            /*
             * Das vorläufig ausgewählte Gerät wird wieder freigegeben.
             * Der Anlernmodus bleibt aktiv, sodass direkt erneut
             * gescannt werden kann.
             */
            _learningCandidateDeviceName = null;
            _scannerDeviceMatcher.Clear();

            if (_statusItem != null)
            {
                _statusItem.Text =
                    "Ungültig – bitte URL-Barcode erneut scannen...";
            }

            _notifyIcon.ShowBalloonTip(
                3000,
                "Scanner nicht gespeichert",
                "Bitte einen vollständigen HTTP- oder HTTPS-Barcode scannen.",
                ToolTipIcon.Warning);

            return;
        }

        if (_learningCandidateDeviceName == null)
        {
            _logger.Warning(
                "Der Scan war gültig, aber es wurde kein " +
                "Scannergerät ermittelt.");

            return;
        }

        /*
         * Erst jetzt wird die Gerätekennung dauerhaft gespeichert.
         */
        _scannerConfiguration.DeviceIdentifier =
            _scannerDeviceMatcher.DeviceIdentifier;

        _scannerConfigurationStore.Save(
            _scannerConfiguration);

        _isLearningScanner = false;
        _learningCandidateDeviceName = null;

        if (_statusItem != null)
        {
            _statusItem.Text = "Scanner erkannt";
        }

        _logger.Info(
            "Scanner wurde nach einem vollständigen " +
            "HTTP-/HTTPS-Scan gespeichert: " +
            _scannerDeviceMatcher.DeviceIdentifier);

        _notifyIcon.ShowBalloonTip(
            3000,
            "Scanner erfolgreich gespeichert",
            "Der Scanner wurde erkannt. Der gescannte Link wird jetzt geöffnet.",
            ToolTipIcon.Info);

        /*
         * Der zum Anlernen verwendete URL-Barcode wird direkt geöffnet.
         */
        ProcessValidatedUrl(validatedUrl!);
    }

    private void ProcessNormalScan(string scannedValue)
    {
        if (scannedValue.Length < _settings.MinimumScanLength)
        {
            _logger.Warning(
                "Der Scan wurde verworfen, weil die " +
                "Mindestlänge nicht erreicht wurde.");

            return;
        }

        if (!UrlValidator.TryValidate(
                scannedValue,
                out Uri? validatedUrl))
        {
            _logger.Warning(
                "Der Scan wurde verworfen, weil keine gültige " +
                "HTTP-/HTTPS-Adresse erkannt wurde.");

            return;
        }

        ProcessValidatedUrl(validatedUrl);
    }

    private void ProcessValidatedUrl(Uri validatedUrl)
    {
        ArgumentNullException.ThrowIfNull(validatedUrl);

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
}
