using System.Runtime.InteropServices;
using System.Text;

namespace ScannerUrlOpener;

internal sealed class RawInputScannerReader : IDisposable
{
    private const int VkReturn = 0x0D;
    private const int VkShift = 0x10;
    private const int VkLShift = 0xA0;
    private const int VkRShift = 0xA1;

    private readonly ScannerSettings _settings;
    private readonly ScannerDeviceMatcher _deviceMatcher;
    private readonly AppLogger _logger;
    private readonly StringBuilder _buffer = new();
    private readonly System.Windows.Forms.Timer _completionTimer;

    private DateTime _lastCharacterTime = DateTime.MinValue;
    private bool _shiftPressed;
    private bool _disposed;

    public RawInputScannerReader(
        ScannerSettings settings,
        ScannerDeviceMatcher deviceMatcher,
        AppLogger logger)
    {
        _settings = settings
            ?? throw new ArgumentNullException(nameof(settings));

        _deviceMatcher = deviceMatcher
            ?? throw new ArgumentNullException(nameof(deviceMatcher));

        _logger = logger
            ?? throw new ArgumentNullException(nameof(logger));

        _completionTimer = new System.Windows.Forms.Timer
        {
            Interval = _settings.ScanCompletionDelayMilliseconds
        };

        _completionTimer.Tick += CompletionTimer_Tick;
    }

    public event EventHandler<string>? ScanCompleted;

    public void ProcessKey(RawInputKeyEventArgs keyEvent)
    {
        ArgumentNullException.ThrowIfNull(keyEvent);

        if (_disposed)
        {
            return;
        }

        if (!_deviceMatcher.IsScanner(keyEvent.DeviceName))
        {
            return;
        }

        int virtualKey = keyEvent.VirtualKey;

        if (IsShiftKey(virtualKey))
        {
            _shiftPressed = !keyEvent.IsKeyReleased;
            return;
        }

        /*
         * Für normale Zeichen werden nur Tastendrücke verarbeitet.
         * Das Loslassen der Taste darf kein zweites Zeichen erzeugen.
         */
        if (keyEvent.IsKeyReleased)
        {
            return;
        }

        DateTime currentTime = DateTime.UtcNow;

        if (_lastCharacterTime != DateTime.MinValue &&
            currentTime - _lastCharacterTime >
            TimeSpan.FromMilliseconds(
                _settings.MaximumCharacterDelayMilliseconds))
        {
            ResetInputState();
        }

        _lastCharacterTime = currentTime;

        if (virtualKey == VkReturn)
        {
            CompleteScan();
            return;
        }

        string character = TranslateKey(
            virtualKey,
            keyEvent.ScanCode);

        if (string.IsNullOrEmpty(character))
        {
            return;
        }

        foreach (char value in character)
        {
            if (!char.IsControl(value))
            {
                _buffer.Append(value);
            }
        }

        if (_buffer.Length > _settings.MaximumScanLength)
        {
            _logger.Warning(
                "Raw-Input-Scan wurde verworfen, weil die " +
                "maximale Länge überschritten wurde.");

            ResetInputState();
            return;
        }

        RestartCompletionTimer();
    }

    private static bool IsShiftKey(int virtualKey)
    {
        return virtualKey == VkShift ||
               virtualKey == VkLShift ||
               virtualKey == VkRShift;
    }

    private string TranslateKey(
        int virtualKey,
        ushort scanCode)
    {
        byte[] keyboardState = new byte[256];

        if (!GetKeyboardState(keyboardState))
        {
            return string.Empty;
        }

        /*
         * Der Shift-Zustand wird explizit aus den Raw-Input-Ereignissen
         * gesetzt. Das ist wichtig für Zeichen wie :, ?, / und _.
         */
        if (_shiftPressed)
        {
            keyboardState[VkShift] = 0x80;
            keyboardState[VkLShift] = 0x80;
            keyboardState[VkRShift] = 0x80;
        }
        else
        {
            keyboardState[VkShift] = 0;
            keyboardState[VkLShift] = 0;
            keyboardState[VkRShift] = 0;
        }

        StringBuilder resultBuffer = new(8);

        IntPtr keyboardLayout = GetKeyboardLayout(0);

        int result = ToUnicodeEx(
            (uint)virtualKey,
            scanCode,
            keyboardState,
            resultBuffer,
            resultBuffer.Capacity,
            0,
            keyboardLayout);

        if (result <= 0)
        {
            return string.Empty;
        }

        return resultBuffer.ToString(0, result);
    }

    private void RestartCompletionTimer()
    {
        _completionTimer.Stop();
        _completionTimer.Start();
    }

    private void CompletionTimer_Tick(
        object? sender,
        EventArgs e)
    {
        _completionTimer.Stop();

        if (_buffer.Length == 0)
        {
            return;
        }

        CompleteScan();
    }

    private void CompleteScan()
    {
        _completionTimer.Stop();

        string scannedValue = _buffer
            .ToString()
            .Trim();

        ResetInputState();

        if (scannedValue.Length < _settings.MinimumScanLength)
        {
            _logger.Warning(
                "Raw-Input-Scan wurde verworfen, weil die " +
                "Mindestlänge nicht erreicht wurde.");

            return;
        }

        ScanCompleted?.Invoke(this, scannedValue);
    }

    private void ResetInputState()
    {
        _completionTimer.Stop();
        _buffer.Clear();
        _lastCharacterTime = DateTime.MinValue;
        _shiftPressed = false;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _completionTimer.Stop();
        _completionTimer.Tick -= CompletionTimer_Tick;
        _completionTimer.Dispose();

        ScanCompleted = null;

        ResetInputState();
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetKeyboardState(
        byte[] keyboardState);

    [DllImport(
        "user32.dll",
        CharSet = CharSet.Unicode)]
    private static extern int ToUnicodeEx(
        uint virtualKeyCode,
        uint scanCode,
        byte[] keyboardState,
        [Out, MarshalAs(UnmanagedType.LPWStr)]
        StringBuilder receivingBuffer,
        int bufferSize,
        uint flags,
        IntPtr keyboardLayout);

    [DllImport("user32.dll")]
    private static extern IntPtr GetKeyboardLayout(
        uint threadId);
}