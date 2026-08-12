namespace ScannerUrlOpener.Input;

internal sealed class RawInputKeyEventArgs : EventArgs
{
    public RawInputKeyEventArgs(
        nint deviceHandle,
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

    public nint DeviceHandle { get; }

    public string DeviceName { get; }

    public ushort VirtualKey { get; }

    public ushort ScanCode { get; }

    public bool IsKeyReleased { get; }
}