// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Benchmarks;

// Used only in separate attribution intervals. Primary latency intervals do not enable this counter.
internal sealed class StorageDimensionsAllocationCounter : IDisposable
{
    private static readonly AsyncLocal<StorageDimensionsAllocationCounter> Current = new(Changed);

    [ThreadStatic]
    private static long threadMark;

    private readonly StorageDimensionsAllocationCounter previous = Current.Value;
    private long allocated;
    private bool complete;

    public StorageDimensionsAllocationCounter() => Current.Value = this;

    public long Bytes => Interlocked.Read(ref this.allocated);

    public void Dispose()
    {
        if (this.complete) return;
        if (!ReferenceEquals(Current.Value, this)) throw new InvalidOperationException("Logical allocation context was lost.");
        Current.Value = this.previous;
        this.complete = true;
    }

    public static async Task VerifyAsync()
    {
        // Different concurrent flows must not attribute each other's arrays, including after an await.
        var bytes = await Task.WhenAll(AllocateAsync(10000), AllocateAsync(100000));
        for (var index = 0; index < bytes.Length; index++)
        {
            var expected = index == 0 ? 30000 : 300000;
            if (bytes[index] < expected || bytes[index] > expected + 65536)
                throw new InvalidOperationException("Logical allocation attribution self-check failed.");
        }

        static async Task<long> AllocateAsync(int size)
        {
            using var counter = new StorageDimensionsAllocationCounter();
            var first = new byte[size];
            await Task.Yield();
            var second = new byte[size * 2];
            GC.KeepAlive(first);
            GC.KeepAlive(second);
            counter.Dispose();
            return counter.Bytes;
        }
    }

    private static void Changed(AsyncLocalValueChangedArgs<StorageDimensionsAllocationCounter> change)
    {
        var now = GC.GetAllocatedBytesForCurrentThread();
        if (change.PreviousValue is { complete: false } previous)
            Interlocked.Add(ref previous.allocated, now - threadMark);
        threadMark = now;
    }
}
