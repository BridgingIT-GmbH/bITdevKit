namespace BridgingIT.DevKit.Application.UnitTests.Caching;

using System.Security.Cryptography;
using BridgingIT.DevKit.Application.Caching;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Shouldly;
using Xunit;

public class CacheStreamingTests
{
    /// <summary>Faulting token callbacks cannot escape a deadline timer or suppress the one expiry notification.</summary>
    [Fact]
    public void TransferBudget_ThrowingCancellationCallback_StillNotifiesExpiryOnce()
    {
        var time = new FakeTimeProvider();
        var notifications = 0;
        using var sut = new CacheTransferBudget(time, new() { IdleTimeout = TimeSpan.FromSeconds(1) }, () => notifications++);
        using var registration = sut.Token.Register(() => throw new InvalidOperationException("Faulting notification"));
        sut.Start();

        Should.NotThrow(() => time.Advance(TimeSpan.FromSeconds(2)));
        time.Advance(TimeSpan.FromSeconds(2));

        sut.Token.IsCancellationRequested.ShouldBeTrue();
        sut.HasExpired.ShouldBeTrue();
        notifications.ShouldBe(1);
    }

    /// <summary>Faulting expiry observers cannot escape a deadline timer or restart the expired budget.</summary>
    [Fact]
    public void TransferBudget_ThrowingExpiryObserver_RemainsExpiredWithoutEscapingTheTimer()
    {
        var time = new FakeTimeProvider();
        var notifications = 0;
        using var sut = new CacheTransferBudget(time, new() { IdleTimeout = TimeSpan.FromSeconds(1) }, () =>
        {
            notifications++;
            throw new InvalidOperationException("Faulting expiry observer");
        });
        sut.Start();

        Should.NotThrow(() => time.Advance(TimeSpan.FromSeconds(2)));
        sut.Progress();
        time.Advance(TimeSpan.FromSeconds(2));

        sut.Token.IsCancellationRequested.ShouldBeTrue();
        sut.HasExpired.ShouldBeTrue();
        notifications.ShouldBe(1);
    }

    /// <summary>Callback faults do not free an ignored native read's owned buffer before the native work ends.</summary>
    [Fact]
    public async Task Deadline_ThrowingNativeCancellationCallback_RetainsOccupiedCapacityUntilActualCompletion()
    {
        var time = new FakeTimeProvider();
        var source = new ControlledReadStream(throwOnCancellation: true);
        var setup = Create(source, [7], time);
        await using var handle = setup.Handle;
        var pending = handle.Stream.ReadAsync(new byte[1]).AsTask();

        Should.NotThrow(() => time.Advance(TimeSpan.FromSeconds(2)));
        var error = await Should.ThrowAsync<CacheTransferException>(async () => await pending);
        error.Code.ShouldBe(CacheErrorCode.CoordinationTimeout);
        await handle.DisposeAsync();
        setup.Budget.Used.ShouldBeGreaterThan(0);
        source.Complete(7);
        await setup.Released.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await handle.DisposeAsync();
        setup.Budget.Used.ShouldBe(0);
    }

    [Fact]
    public async Task FullConsumption_VerifiesLogicalBytesAndReportsValidated()
    {
        var time = new FakeTimeProvider();
        var setup = Create(new MemoryStream([1, 2, 3]), [1, 2, 3], time);
        await using var handle = setup.Handle;
        using var output = new MemoryStream();

        await handle.Stream.CopyToAsync(output);
        var terminal = await handle.WaitForCompletionAsync();

        output.ToArray().ShouldBe(new byte[] { 1, 2, 3 });
        terminal.IsSuccess.ShouldBeTrue();
        terminal.Value.ShouldBe(CacheReadCompletion.Validated);
        await handle.DisposeAsync();
        setup.Budget.Used.ShouldBe(0);
    }

    [Fact]
    public async Task CompletionObservation_DoesNotReadOrDrainTheStream()
    {
        var time = new FakeTimeProvider();
        var source = new ControlledReadStream();
        var setup = Create(source, [1], time);
        await using var handle = setup.Handle;
        using var cancellation = new CancellationTokenSource();
        var observation = handle.WaitForCompletionAsync(cancellation.Token).AsTask();

        source.Reads.ShouldBe(0);
        observation.IsCompleted.ShouldBeFalse();
        cancellation.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(async () => await observation);
        source.Reads.ShouldBe(0);
        handle.Stream.CanRead.ShouldBeTrue();
    }

    [Fact]
    public async Task EarlyDisposal_ReportsIncompleteAndReleasesOwnedCapacity()
    {
        var setup = Create(new MemoryStream([1, 2, 3]), [1, 2, 3], new());
        await setup.Handle.DisposeAsync();

        var terminal = await setup.Handle.WaitForCompletionAsync();

        terminal.IsSuccess.ShouldBeTrue();
        terminal.Value.ShouldBe(CacheReadCompletion.Incomplete);
        setup.Budget.Used.ShouldBe(0);
    }

    [Fact]
    public async Task IntegrityFailure_AfterDelivery_RemainsATerminalFailure()
    {
        var setup = Create(new MemoryStream([2]), [1], new());
        await using var handle = setup.Handle;
        var buffer = new byte[8];
        (await handle.Stream.ReadAsync(buffer)).ShouldBe(1);

        await Should.ThrowAsync<IOException>(async () => (await handle.Stream.ReadAsync(buffer)).ShouldBe(0));
        var terminal = await handle.WaitForCompletionAsync();

        terminal.IsFailure.ShouldBeTrue();
        terminal.Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.Corruption);
    }

    [Fact]
    public async Task Timeout_IgnoredNativeCancellation_KeepsItsOwnBufferAndReservationsUntilActualEnd()
    {
        var time = new FakeTimeProvider();
        var source = new ControlledReadStream();
        var setup = Create(source, [42], time);
        await using var handle = setup.Handle;
        var destination = new byte[] { 99 };
        var pending = handle.Stream.ReadAsync(destination).AsTask();
        source.Reads.ShouldBe(1);

        time.Advance(TimeSpan.FromSeconds(2));
        await Should.ThrowAsync<IOException>(async () => await pending);
        (await handle.WaitForCompletionAsync()).IsFailure.ShouldBeTrue();
        destination[0].ShouldBe((byte)99);
        setup.Budget.Used.ShouldBeGreaterThan(0);
        await handle.DisposeAsync();
        setup.Budget.Used.ShouldBeGreaterThan(0);

        source.Complete(42);
        await setup.Released.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await handle.DisposeAsync();

        destination[0].ShouldBe((byte)99);
        setup.Budget.Used.ShouldBe(0);
    }

    [Fact]
    public async Task PartialRange_CannotClaimFullIntegrity()
    {
        var setup = Create(new MemoryStream([1, 2]), [9, 9, 9, 9], new(), partial: true, expectedBytes: 2);
        await using var handle = setup.Handle;
        using var output = new MemoryStream();

        await handle.Stream.CopyToAsync(output);
        var terminal = await handle.WaitForCompletionAsync();

        terminal.Value.ShouldBe(CacheReadCompletion.RangeCompleted);
    }

    private static Setup Create(Stream source, byte[] expected, FakeTimeProvider time, bool partial = false, long? expectedBytes = null)
    {
        var catalog = new ServiceCollection().AddApplicationCaching().BuildCatalog();
        var tasks = new CacheRuntimeTasks(catalog);
        var budget = new ResourceBudget("test-buffer", 1024);
        budget.TryReserve(256, out var reservation).ShouldBeTrue();
        var released = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var metadata = new CacheEntryMetadata
        {
            Identity = new("app", "cards", "v1", "key"), Version = CacheEntryVersion.New(),
            LogicalSizeBytes = expected.Length, PayloadHash = Convert.ToHexStringLower(SHA256.HashData(expected))
        };
        var owned = new CacheReadStream(source, metadata, new byte[16], reservation,
            (_, _) => ValueTask.FromResult(Result.Success()),
            () => { released.TrySetResult(); return ValueTask.CompletedTask; }, tasks, time,
            new() { IdleTimeout = TimeSpan.FromSeconds(1), Lifetime = TimeSpan.FromSeconds(10) }, partial, expectedBytes);
        return new(new(owned, metadata), budget, released);
    }

    private sealed record Setup(CacheReadHandle Handle, ResourceBudget Budget, TaskCompletionSource Released);

    private sealed class ControlledReadStream(bool throwOnCancellation = false) : Stream
    {
        private readonly TaskCompletionSource<int> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private Memory<byte> destination;
        private CancellationTokenRegistration cancellation;

        public int Reads { get; private set; }

        public void Complete(byte value)
        {
            this.destination.Span[0] = value;
            this.completion.TrySetResult(1);
        }

        public override bool CanRead => true;

        public override bool CanWrite => false;

        public override bool CanSeek => false;

        public override long Length => 1;

        public override long Position { get => 0; set => throw new NotSupportedException(); }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            this.Reads++;
            this.destination = buffer;
            if (throwOnCancellation)
            {
                this.cancellation = cancellationToken.Register(() => throw new InvalidOperationException("Faulting native cancellation callback"));
            }

            return new(this.completion.Task); // Deliberately ignores cancellation to model occupied native I/O.
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { this.cancellation.Dispose(); }

            base.Dispose(disposing);
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override void Flush() { }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}