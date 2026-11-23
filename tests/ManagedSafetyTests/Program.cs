using ExplorerTabUtility.Helpers;

static void Check(bool condition, string name)
{
    if (!condition) throw new Exception(name);
    Console.WriteLine($"PASS {name}");
}

using (var gate = new SemaphoreSlim(1, 1))
{
    Check(!await TransferGate.TryEnterAsync(gate, () => false), "expired request never acquires gate");
    Check(gate.CurrentCount == 1, "expired request leaves permit unchanged");
    Check(await TransferGate.TryEnterAsync(gate, () => true), "valid request obtains gate");
    Check(gate.CurrentCount == 0, "valid request owns exactly one permit");
    gate.Release();
}
using (var gate = new SemaphoreSlim(0, 1))
{
    Check(!await TransferGate.TryEnterAsync(gate, () => true, 40), "busy destination queue times out");
    gate.Release();
    await Task.Delay(50);
    Check(gate.CurrentCount == 1, "timed-out waiter cannot steal a future permit");
}
using (var gate = new SemaphoreSlim(0, 1))
{
    bool valid = true;
    var request = TransferGate.TryEnterAsync(gate, () => Volatile.Read(ref valid));
    Volatile.Write(ref valid, false);
    gate.Release();
    Check(!await request, "request invalidated while queued is rejected");
    Check(gate.CurrentCount == 1, "post-acquisition invalidation releases permit");
}
using (var gate = new SemaphoreSlim(1, 1))
{
    int checks = 0;
    try
    {
        await TransferGate.TryEnterAsync(gate, () => ++checks == 1 ? true : throw new InvalidOperationException("guard unavailable"));
        throw new Exception("Expected guard failure");
    }
    catch (InvalidOperationException) { }
    Check(gate.CurrentCount == 1, "guard exception cannot strand semaphore");
    Check(await TransferGate.TryEnterAsync(gate, null), "normal direct command still obtains gate");
    gate.Release();
}
