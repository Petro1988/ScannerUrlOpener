using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace ScannerUrlOpener;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        using ScannerApplicationContext context = new();
        Application.Run(context);
    }
}

internal sealed class ScannerApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ScannerKeyboardHook _scannerHook;

    public ScannerApplicationContext()
    {
        ContextMenuStrip menu = new();

        ToolStripMenuItem statusItem = new("Scanner URL Opener ist aktiv")
        {
            Enabled = false
        };

        ToolStripMenuItem testItem = new("Testseite öffnen");
        testItem.Click += (_, _) =>
        {
            UrlLauncher.OpenUrl(
                "http://inventory/LOGINventory/details.aspx?invnr=ASSET-3");
        };

        ToolStripMenuItem exitItem = new("Beenden");
        exitItem.Click += (_, _) => ExitThread();

        menu.Items.Add(statusItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(testItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);

        _notifyIcon = new NotifyIcon
        {
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath)
                ?? SystemIcons.Application,
            Text = "Scanner URL Opener",
            Visible = true,
            ContextMenuStrip = menu
        };

        _scannerHook = new ScannerKeyboardHook();
        _scannerHook.UrlScanned += ScannerHook_UrlScanned;
        _scannerHook.Start();

        _notifyIcon.ShowBalloonTip(
            2000,
            "Scanner URL Opener",
            "Die Anwendung wartet auf einen HTTP- oder HTTPS-Scan.",
            ToolTipIcon.Info);
    }

    private void ScannerHook_UrlScanned(object? sender, string scannedUrl)
    {
        UrlLauncher.OpenUrl(scannedUrl);

        _notifyIcon.ShowBalloonTip(
            1500,
            "Link geöffnet",
            scannedUrl,
            ToolTipIcon.Info);
    }

    protected override void ExitThreadCore()
    {
        _scannerHook.UrlScanned -= ScannerHook_UrlScanned;
        _scannerHook.Dispose();

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();

        base.ExitThreadCore();
    }
}

internal sealed class ScannerKeyboardHook : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmSysKeyDown = 0x0104;

    private const int VkReturn = 0x0D;
    private const int VkShift = 0x10;
    private const int VkLShift = 0xA0;
    private const int VkRShift = 0xA1;

    private const int MaxScanLength = 2048;

    /*
     * Scanner senden Zeichen normalerweise wesentlich schneller als ein Mensch.
     * Wenn zwischen zwei Zeichen mehr als 100 ms liegen, wird ein neuer
     * Eingabepuffer begonnen.
     */
    private static readonly TimeSpan MaximumCharacterDelay =
        TimeSpan.FromMilliseconds(100);

    /*
     * Mindestlänge zur Vermeidung versehentlicher Erkennung sehr kurzer Eingaben.
     */
    private const int MinimumScanLength = 10;

    private readonly StringBuilder _buffer = new();
    private readonly LowLevelKeyboardProc _hookCallback;

    private IntPtr _hookHandle;
    private DateTime _lastCharacterTime = DateTime.MinValue;
    private bool _shiftPressed;

    public event EventHandler<string>? UrlScanned;

    public ScannerKeyboardHook()
    {
        _hookCallback = HookCallback;
    }

    public void Start()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            return;
        }

        using Process currentProcess = Process.GetCurrentProcess();

        ProcessModule? module = currentProcess.MainModule;

        if (module == null)
        {
            throw new InvalidOperationException(
                "Das aktuelle Prozessmodul konnte nicht ermittelt werden.");
        }

        IntPtr moduleHandle = GetModuleHandle(module.ModuleName);

        _hookHandle = SetWindowsHookEx(
            WhKeyboardLl,
            _hookCallback,
            moduleHandle,
            0);

        if (_hookHandle == IntPtr.Zero)
        {
            int errorCode = Marshal.GetLastWin32Error();

            throw new InvalidOperationException(
                $"Der Tastatur-Hook konnte nicht eingerichtet werden. " +
                $"Windows-Fehler: {errorCode}");
        }
    }

    private IntPtr HookCallback(
        int nCode,
        IntPtr wParam,
        IntPtr lParam)
    {
        if (nCode >= 0 &&
            (wParam == (IntPtr)WmKeyDown ||
             wParam == (IntPtr)WmSysKeyDown))
        {
            KbdLlHookStruct keyboardData =
                Marshal.PtrToStructure<KbdLlHookStruct>(lParam);

            int virtualKey = unchecked((int)keyboardData.VirtualKeyCode);

            ProcessKey(virtualKey, keyboardData.ScanCode);
        }

        return CallNextHookEx(
            _hookHandle,
            nCode,
            wParam,
            lParam);
    }

    private void ProcessKey(int virtualKey, uint scanCode)
    {
        if (virtualKey == VkShift ||
            virtualKey == VkLShift ||
            virtualKey == VkRShift)
        {
            _shiftPressed = true;
            return;
        }

        DateTime currentTime = DateTime.UtcNow;

        if (_lastCharacterTime != DateTime.MinValue &&
            currentTime - _lastCharacterTime > MaximumCharacterDelay)
        {
            _buffer.Clear();
        }

        _lastCharacterTime = currentTime;

        if (virtualKey == VkReturn)
        {
            ProcessCompletedInput();
            return;
        }

        string character = TranslateKey(virtualKey, scanCode);

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

        if (_buffer.Length > MaxScanLength)
        {
            _buffer.Clear();
        }

        _shiftPressed = false;
    }

    private string TranslateKey(int virtualKey, uint scanCode)
    {
        byte[] keyboardState = new byte[256];

        if (!GetKeyboardState(keyboardState))
        {
            return string.Empty;
        }

        if (_shiftPressed)
        {
            keyboardState[VkShift] = 0x80;
            keyboardState[VkLShift] = 0x80;
        }

        StringBuilder translatedValue = new(8);

        IntPtr keyboardLayout = GetKeyboardLayout(0);

        int result = ToUnicodeEx(
            (uint)virtualKey,
            scanCode,
            keyboardState,
            translatedValue,
            translatedValue.Capacity,
            0,
            keyboardLayout);

        return result > 0
            ? translatedValue.ToString(0, result)
            : string.Empty;
    }

    private void ProcessCompletedInput()
    {
        string value = _buffer.ToString().Trim();

        _buffer.Clear();
        _lastCharacterTime = DateTime.MinValue;
        _shiftPressed = false;

        if (value.Length < MinimumScanLength)
        {
            return;
        }

        if (!Uri.TryCreate(
                value,
                UriKind.Absolute,
                out Uri? parsedUri))
        {
            return;
        }

        bool allowedProtocol =
            parsedUri.Scheme.Equals(
                Uri.UriSchemeHttp,
                StringComparison.OrdinalIgnoreCase) ||
            parsedUri.Scheme.Equals(
                Uri.UriSchemeHttps,
                StringComparison.OrdinalIgnoreCase);

        if (!allowedProtocol)
        {
            return;
        }

        UrlScanned?.Invoke(this, parsedUri.AbsoluteUri);
    }

    public void Dispose()
    {
        if (_hookHandle == IntPtr.Zero)
        {
            return;
        }

        UnhookWindowsHookEx(_hookHandle);
        _hookHandle = IntPtr.Zero;
    }

    private delegate IntPtr LowLevelKeyboardProc(
        int nCode,
        IntPtr wParam,
        IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KbdLlHookStruct
    {
        public uint VirtualKeyCode;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [DllImport(
        "user32.dll",
        SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(
        int hookId,
        LowLevelKeyboardProc callback,
        IntPtr moduleHandle,
        uint threadId);

    [DllImport(
        "user32.dll",
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(
        IntPtr hookHandle);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(
        IntPtr hookHandle,
        int nCode,
        IntPtr wParam,
        IntPtr lParam);

    [DllImport(
        "kernel32.dll",
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    private static extern IntPtr GetModuleHandle(
        string? moduleName);

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
        [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder receivingBuffer,
        int bufferSize,
        uint flags,
        IntPtr keyboardLayout);

    [DllImport("user32.dll")]
    private static extern IntPtr GetKeyboardLayout(
        uint threadId);
}

internal static class UrlLauncher
{
    public static void OpenUrl(string value)
    {
        if (!Uri.TryCreate(
                value,
                UriKind.Absolute,
                out Uri? parsedUri))
        {
            return;
        }

        bool allowedProtocol =
            parsedUri.Scheme.Equals(
                Uri.UriSchemeHttp,
                StringComparison.OrdinalIgnoreCase) ||
            parsedUri.Scheme.Equals(
                Uri.UriSchemeHttps,
                StringComparison.OrdinalIgnoreCase);

        if (!allowedProtocol)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = parsedUri.AbsoluteUri,
                UseShellExecute = true
            });
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"Der Browser konnte nicht geöffnet werden.\n\n" +
                exception.Message,
                "Scanner URL Opener",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
