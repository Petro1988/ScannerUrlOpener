using System;

namespace ScannerUrlOpener;

internal sealed class SingleInstanceGuard : IDisposable
{
    private readonly Mutex _mutex;
    private readonly bool _ownsMutex;
    private bool _disposed;

    public SingleInstanceGuard(string applicationIdentifier)
    {
        if (string.IsNullOrWhiteSpace(applicationIdentifier))
        {
            throw new ArgumentException(
                "Die Anwendungskennung darf nicht leer sein.",
                nameof(applicationIdentifier));
        }

        /*
         * Local\ bedeutet:
         * Die Anwendung darf pro angemeldeter Windows-Sitzung
         * nur einmal ausgeführt werden.
         */
        string mutexName =
     $@"Local\{applicationIdentifier.Trim()}";

        _mutex = new Mutex(
            initiallyOwned: true,
            name: mutexName,
            createdNew: out bool createdNew);

        _ownsMutex = createdNew;
    }

    public bool IsPrimaryInstance => _ownsMutex;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_ownsMutex)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                /*
                 * Der Mutex wurde bereits freigegeben oder wird
                 * nicht mehr von diesem Thread gehalten.
                 */
            }
        }

        _mutex.Dispose();
    }
}