using System.Runtime.InteropServices;
using System.Text;

namespace ScannerUrlOpener;

internal sealed class RawInputWindow : NativeWindow, IDisposable
{
    private const int WmInput = 0x00FF;

    private const uint RidInput = 0x10000003;
    private const uint RidiDeviceName = 0x20000007;

    private const uint RidevInputSink = 0x00000100;

    private const uint RimTypeKeyboard = 1;

    private const ushort HidUsagePageGeneric = 0x01;
    private const ushort HidUsageGenericKeyboard = 0x06;

    private const ushort RiKeyBreak = 0x0001;

    private bool _disposed;

    public event EventHandler<RawInputKeyEventArgs>? KeyReceived;

    public RawInputWindow()
    {
        CreateHandle(new CreateParams
        {
            Caption = "ScannerUrlOpener.RawInputWindow"
        });

        RegisterKeyboardInput();
    }

    private void RegisterKeyboardInput()
    {
        RawInputDevice[] devices =
        [
            new RawInputDevice
            {
                UsagePage = HidUsagePageGeneric,
                Usage = HidUsageGenericKeyboard,
                Flags = RidevInputSink,
                TargetWindow = Handle
            }
        ];

        bool registered = RegisterRawInputDevices(
            devices,
            (uint)devices.Length,
            (uint)Marshal.SizeOf<RawInputDevice>());

        if (!registered)
        {
            int errorCode = Marshal.GetLastWin32Error();

            throw new InvalidOperationException(
                "Raw Input konnte nicht registriert werden. " +
                $"Windows-Fehler: {errorCode}");
        }
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == WmInput)
        {
            ProcessRawInput(message.LParam);
        }

        base.WndProc(ref message);
    }

    private void ProcessRawInput(IntPtr rawInputHandle)
    {
        uint dataSize = 0;
        uint headerSize =
            (uint)Marshal.SizeOf<RawInputHeader>();

        uint firstResult = GetRawInputData(
            rawInputHandle,
            RidInput,
            IntPtr.Zero,
            ref dataSize,
            headerSize);

        if (firstResult != 0 || dataSize == 0)
        {
            return;
        }

        IntPtr buffer = Marshal.AllocHGlobal((int)dataSize);

        try
        {
            uint receivedSize = dataSize;

            uint result = GetRawInputData(
                rawInputHandle,
                RidInput,
                buffer,
                ref receivedSize,
                headerSize);

            if (result == uint.MaxValue ||
                result != receivedSize)
            {
                return;
            }

            RawInputHeader header =
                Marshal.PtrToStructure<RawInputHeader>(buffer);

            if (header.Type != RimTypeKeyboard)
            {
                return;
            }

            IntPtr keyboardPointer = IntPtr.Add(
                buffer,
                Marshal.SizeOf<RawInputHeader>());

            RawKeyboard keyboard =
                Marshal.PtrToStructure<RawKeyboard>(
                    keyboardPointer);

            bool keyReleased =
                (keyboard.Flags & RiKeyBreak) != 0;

            if (keyboard.VirtualKey == 255)
            {
                return;
            }

            string deviceName =
                GetDeviceName(header.Device);

            KeyReceived?.Invoke(
                this,
                new RawInputKeyEventArgs(
                    header.Device,
                    deviceName,
                    keyboard.VirtualKey,
                    keyboard.MakeCode,
                    keyReleased));
            }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static string GetDeviceName(
        IntPtr deviceHandle)
    {
        uint characterCount = 0;

        GetRawInputDeviceInfo(
            deviceHandle,
            RidiDeviceName,
            null,
            ref characterCount);

        if (characterCount == 0)
        {
            return "Unbekanntes Gerät";
        }

        StringBuilder deviceName =
            new((int)characterCount);

        uint result = GetRawInputDeviceInfo(
            deviceHandle,
            RidiDeviceName,
            deviceName,
            ref characterCount);

        return result == uint.MaxValue
            ? "Unbekanntes Gerät"
            : deviceName.ToString();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        KeyReceived = null;
        DestroyHandle();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputDevice
    {
        public ushort UsagePage;
        public ushort Usage;
        public uint Flags;
        public IntPtr TargetWindow;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputHeader
    {
        public uint Type;
        public uint Size;
        public IntPtr Device;
        public IntPtr WParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawKeyboard
    {
        public ushort MakeCode;
        public ushort Flags;
        public ushort Reserved;
        public ushort VirtualKey;
        public uint Message;
        public uint ExtraInformation;
    }

    [DllImport(
        "user32.dll",
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterRawInputDevices(
        RawInputDevice[] devices,
        uint deviceCount,
        uint structureSize);

    [DllImport(
        "user32.dll",
        SetLastError = true)]
    private static extern uint GetRawInputData(
        IntPtr rawInputHandle,
        uint command,
        IntPtr data,
        ref uint dataSize,
        uint headerSize);

    [DllImport(
        "user32.dll",
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    private static extern uint GetRawInputDeviceInfo(
        IntPtr deviceHandle,
        uint command,
        StringBuilder? data,
        ref uint dataSize);
}