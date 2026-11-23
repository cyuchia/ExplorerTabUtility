using System;
using System.Threading;
using System.Threading.Tasks;

namespace ExplorerTabUtility.Helpers;

internal static class TransferGate
{
    // An externally opened, guarded window has a native watchdog. A queued
    // request must never outlive that window's permission to transfer.
    internal static async Task<bool> TryEnterAsync(SemaphoreSlim gate, Func<bool>? isCurrent, int timeoutMs = 1500)
    {
        if (isCurrent == null)
        {
            await gate.WaitAsync().ConfigureAwait(false);
            return true;
        }
        if (!isCurrent() || !await gate.WaitAsync(timeoutMs).ConfigureAwait(false)) return false;
        try
        {
            if (isCurrent()) return true;
        }
        catch
        {
            gate.Release();
            throw;
        }
        gate.Release();
        return false;
    }
}
