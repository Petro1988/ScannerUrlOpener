namespace ScannerUrlOpener;

internal sealed class RawInputKeyEventArgs : EventArgs
{
    public RawInputKeyEventArgs(
        IntPtr deviceHandle,
        string deviceName,
        ushort virtualKey,
        ushort scanCode,
        bool isKeyReleased)
    {
        DeviceHandle = deviceHandle;
        DeviceName = deviceName;
        VirtualKey = virtualKey;
        ScanCode = scanCode;
        IsKeyReleased = isKeyReleased;
    }

    public IntPtr DeviceHandle { get; }

    public string DeviceName { get; }

    public ushort VirtualKey { get; }

    public ushort ScanCode { get; }

    public bool IsKeyReleased { get; }
}