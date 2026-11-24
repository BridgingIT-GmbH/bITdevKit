// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb;

using System.Security.Cryptography;
using System.Text.Json;
using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Common;

public sealed partial class LiteDbCacheStoreProvider
{
    /// <inheritdoc />
    public async ValueTask<Result<ICacheProviderWriteSession>> BeginWriteAsync(CacheProviderWriteRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var admitted = await this.writes.AcquireAsync(this.options.AttemptTimeout, cancellationToken).ConfigureAwait(false);
        if (admitted.IsFailure) { return Result<ICacheProviderWriteSession>.Failure(admitted); }

        ResourceCharge memory = null;
        var transferred = false;
        try
        {
            if (!ResourceCharge.TryReserve(3L * this.options.ChunkSize, out memory, this.transient, this.ResidentFor(this.Definition(request.Identity))))
            { return Result<ICacheProviderWriteSession>.Failure(Error(CacheErrorCode.Capacity, "Staging buffer capacity is exhausted.")); }

            var result = await this.Run(db =>
            {
                var definition = this.Definition(request.Identity);
                if (request.MaximumStoredBytes < 0 || request.Owner.HasValue || (request.Condition.CreateOnly && request.Condition.ExpectedVersion.HasValue))
                { throw Failure(CacheErrorCode.Validation, "Invalid native cache staging preconditions."); }

                this.VerifyFence(db, request.Identity, request.Fence);
                CheckDeadline(request.Deadline, this.timeProvider.GetUtcNow());
                var id = request.OperationId ?? Guid.NewGuid().ToString("N");
                if (!Guid.TryParseExact(id, "N", out _) || this.Rows(db, "receipts").Exists(Query.EQ("_id", id))) { throw Conflict(); }

                var scope = Scope(request.Identity);
                var store = this.Required<StoreState>(db, "state", "store");
                var state = this.Required<CollectionState>(db, "state", scope);
                this.ChargeMetadata(store, state, definition, PayloadCharge * 2);
                var version = CacheEntryVersion.New();
                this.SavePayload(db, new PayloadState { Id = version.Value, Scope = scope, Owner = this.instance,
                    ChunkSize = this.options.ChunkSize, CreatedAt = this.timeProvider.GetUtcNow() });
                this.SaveReceipt(db, id, new WriteReceipt { Version = version, Scope = scope, UpdatedAt = this.timeProvider.GetUtcNow(), Charge = PayloadCharge, Owner = this.instance });
                this.SaveAccounting(db, store, scope, state);
                return new WriteSession(this, request, definition, version, id, admitted.Value, memory);
            }, cancellationToken).ConfigureAwait(false);
            if (result.IsFailure) { return Result<ICacheProviderWriteSession>.Failure(result); }

            transferred = true;
            return Result<ICacheProviderWriteSession>.Success(result.Value);
        }
        catch (LiteDbProviderException ex) { return Result<ICacheProviderWriteSession>.Failure().WithErrors(ex.Errors); }
        finally
        {
            if (!transferred) { memory?.Dispose(); admitted.Value.Dispose(); }
        }
    }

    private ValueTask<Result<bool>> ReserveStageAsync(WriteSession session, int bytes, CancellationToken token) => this.Run(db =>
    {
        var payload = this.Required<PayloadState>(db, "payloads", session.Version.Value);
        if (payload.State != "staging" || payload.Owner != this.instance) { throw Conflict(); }

        CheckDeadline(session.Request.Deadline, this.timeProvider.GetUtcNow());
        var store = this.Required<StoreState>(db, "state", "store");
        var state = this.Required<CollectionState>(db, "state", payload.Scope);
        var payloadMaximum = (long)(this.options.MaxStorageBytes * (1 - this.options.MetadataReserveRatio));
        if (store.Current + store.Retired + store.Staging > payloadMaximum - bytes || bytes < 0 || payload.Length > session.Request.MaximumStoredBytes - bytes || store.Total > this.options.MaxStorageBytes - bytes ||
            state.Total > session.Definition.Policies.Limits.MaxStorageBytes - bytes)
        { throw Failure(CacheErrorCode.Capacity, "Staged bytes exceed a finite native cache storage limit."); }

        payload.Length += bytes;
        store.Staging += bytes; state.Staging += bytes;
        this.SavePayload(db, payload);
        this.SaveAccounting(db, store, payload.Scope, state);
        return true;
    }, token);

    private ValueTask<Result<bool>> AppendAsync(WriteSession session, byte[] bytes, int ordinal, CancellationToken token) => this.Run(db =>
    {
        var payload = this.Required<PayloadState>(db, "payloads", session.Version.Value);
        if (payload.State != "staging" || payload.Owner != this.instance || payload.Chunks != ordinal) { throw Conflict(); }

        this.Rows(db, "chunks").Insert(new BsonDocument { ["_id"] = ChunkId(payload.Id, ordinal), ["Bytes"] = bytes });
        payload.Chunks++;
        this.SavePayload(db, payload);
        return true;
    }, token);

    private ValueTask<Result<CacheWriteOutcome>> PublishAsync(WriteSession session, CacheEntryMetadata supplied, CancellationToken token) => this.Run(db =>
    {
        var payload = this.Required<PayloadState>(db, "payloads", session.Version.Value);
        if (payload.State != "staging" || payload.Length != session.Destination.Length || payload.Chunks != session.Destination.Chunks) { throw Conflict(); }

        this.VerifyFence(db, session.Request.Identity, session.Request.Fence);
        CheckDeadline(session.Request.Deadline, this.timeProvider.GetUtcNow());
        var prepared = this.PrepareMetadata(session, supplied);
        if (prepared.IsFailure) { throw new LiteDbProviderException(prepared); }

        var metadata = prepared.Value.Metadata;
        var previous = this.Entry(db, metadata.Identity);
        CheckCondition(previous, session.Request.Condition, this.timeProvider.GetUtcNow());
        var store = this.Required<StoreState>(db, "state", "store");
        var state = this.Required<CollectionState>(db, "state", payload.Scope);
        if (previous is null && (store.Count >= this.options.MaxEntries || state.Count >= session.Definition.Policies.Limits.MaxEntries))
        { throw Failure(CacheErrorCode.Capacity, "The retained native cache entry count is exhausted."); }

        this.ChargeMetadata(store, state, session.Definition, prepared.Value.ChargeBytes * 2);
        if (previous is not null) { this.Retire(db, previous, store, state); }

        store.Staging -= payload.Length; state.Staging -= payload.Length;
        store.Current += payload.Length; state.Current += payload.Length;
        store.Count++; state.Count++;
        payload.State = "current";
        this.SavePayload(db, payload);
        state.Revision++;
        var sequence = checked(++store.Sequence);
        this.SaveEntry(db, new EntryState { Metadata = metadata, Payload = payload.Id, Sequence = sequence, MetadataChargeBytes = prepared.Value.ChargeBytes });
        var key = this.Get<KeyState>(db, "keys", LogicalKey(metadata.Identity));
        if (key is not null) { key.Sequences[metadata.Identity.ContractVersion] = sequence; this.Put(db, "keys", LogicalKey(metadata.Identity), key); }

        var receipt = this.Required<WriteReceipt>(db, "receipts", session.OperationId);
        receipt.Outcome = new(CacheStorageOutcome.Stored, session.Version, metadata);
        receipt.Completed = true;
        receipt.Charge += prepared.Value.ChargeBytes;
        receipt.UpdatedAt = this.timeProvider.GetUtcNow();
        this.SaveReceipt(db, session.OperationId, receipt);
        this.SaveAccounting(db, store, payload.Scope, state);
        return receipt.Outcome;
    }, token);

    private async ValueTask AbortAsync(WriteSession session)
    {
        _ = await this.Run(db =>
        {
            var payload = this.Get<PayloadState>(db, "payloads", session.Version.Value);
            if (payload?.State != "staging") { return true; }

            var store = this.Required<StoreState>(db, "state", "store");
            var state = this.Required<CollectionState>(db, "state", payload.Scope);
            store.Staging -= payload.Length; state.Staging -= payload.Length;
            store.Retired += payload.Length; state.Retired += payload.Length;
            payload.State = "retired";
            this.SavePayload(db, payload);
            var receipt = this.Required<WriteReceipt>(db, "receipts", session.OperationId);
            receipt.Completed = true;
            receipt.Outcome = new(CacheStorageOutcome.Failed);
            receipt.UpdatedAt = this.timeProvider.GetUtcNow();
            this.SaveReceipt(db, session.OperationId, receipt);
            this.SaveAccounting(db, store, payload.Scope, state);
            return true;
        }, CancellationToken.None).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public ValueTask<Result<CacheWriteOutcome>> ReconcileAsync(string operationId, CancellationToken cancellationToken = default) => this.Run(db =>
    {
        var receipt = this.Get<WriteReceipt>(db, "receipts", operationId) ?? throw Failure(CacheErrorCode.NotFound, "The retained write receipt is unavailable.");
        if (!this.AllowedScope(receipt.Scope)) { throw Failure(CacheErrorCode.NotFound, "The retained write receipt is unavailable in this catalog."); }

        return receipt.Completed ? receipt.Outcome : new CacheWriteOutcome(CacheStorageOutcome.Indeterminate);
    }, cancellationToken);

    private void Retire(LiteDatabase db, EntryState entry, StoreState store, CollectionState state)
    {
        this.Rows(db, "entries").Delete(entry.Metadata.Identity.Digest);
        var payload = this.Required<PayloadState>(db, "payloads", entry.Payload);
        payload.State = "retired";
        this.SavePayload(db, payload);
        store.Current -= payload.Length; state.Current -= payload.Length;
        store.Retired += payload.Length; state.Retired += payload.Length;
        store.Count--; state.Count--;
        store.Metadata -= entry.MetadataChargeBytes; state.Metadata -= entry.MetadataChargeBytes;
    }

    private static void CheckCondition(EntryState previous, CacheWriteCondition condition, DateTimeOffset now)
    {
        if ((condition.ExpectedVersion is { } expected && (previous is null || !Valid(previous.Metadata, now) || previous.Metadata.Version != expected)) ||
            (condition.CreateOnly && previous is not null && Valid(previous.Metadata, now) && !previous.Metadata.IsNotFound)) { throw Conflict(); }
    }

    private static void CheckDeadline(DateTimeOffset? deadline, DateTimeOffset now)
    {
        if (deadline <= now) { throw Failure(CacheErrorCode.SourceTimeout, "The fixed cache publication deadline elapsed."); }
    }

    private async ValueTask<Result<byte[]>> ReadChunkAsync(string payloadId, int ordinal, CancellationToken token) => await this.Run(db =>
    {
        var row = this.Rows(db, "chunks").FindById(ChunkId(payloadId, ordinal));
        return row?["Bytes"].AsBinary ?? throw Failure(CacheErrorCode.Corruption, "An immutable cache payload chunk is missing.");
    }, token).ConfigureAwait(false);

    private async ValueTask ReleaseReaderAsync(string payloadId)
    {
        _ = await this.Run(db =>
        {
            var payload = this.Get<PayloadState>(db, "payloads", payloadId);
            if (payload?.PinOwner == this.instance && payload.Pins > 0) { payload.Pins--; this.SavePayload(db, payload); }

            return true;
        }, CancellationToken.None).ConfigureAwait(false);
    }

    private static string ChunkId(string payload, int ordinal) => payload + ":" + ordinal.ToString("D10", System.Globalization.CultureInfo.InvariantCulture);
    private void SaveReceipt(LiteDatabase db, string id, WriteReceipt receipt)
    {
        this.Put(db, "receipts", id, receipt, receipt.Scope, receipt.UpdatedAt.UtcTicks);
        var row = this.Rows(db, "receipts").FindById(id);
        row["Terminal"] = receipt.Completed;
        row["Owner"] = receipt.Owner;
        row["Completed"] = receipt.Completed;
        this.Rows(db, "receipts").Update(row);
    }

    private sealed class WriteReceipt
    {
        public CacheEntryVersion Version { get; set; }

        public string Owner { get; set; }

        public string Scope { get; set; }

        public bool Completed { get; set; }

        public CacheWriteOutcome Outcome { get; set; }

        public DateTimeOffset UpdatedAt { get; set; }

        public long Charge { get; set; }
    }

    private sealed class WriteSession : ICacheProviderWriteSession
    {
        private readonly LiteDbCacheStoreProvider owner;
        private readonly CacheAdmissionLease admission;
        private readonly ResourceCharge memory;
        private readonly Lock gate = new();
        private Task<Result<CacheWriteOutcome>> publication;
        private Task disposal;
        private bool disposed;

        public WriteSession(LiteDbCacheStoreProvider owner, CacheProviderWriteRequest request, CacheProviderCollection definition, CacheEntryVersion version,
            string operationId, CacheAdmissionLease admission, ResourceCharge memory)
        {
            this.owner = owner; this.Request = request; this.Definition = definition; this.Version = version; this.OperationId = operationId;
            this.admission = admission; this.memory = memory; this.Destination = new(owner, this);
        }

        public CacheProviderWriteRequest Request { get; }

        public CacheProviderCollection Definition { get; }

        public StagingStream Destination { get; }

        public Stream Stream => this.Destination;
        public string OperationId { get; }

        public CacheEntryVersion Version { get; }

        public async ValueTask<Result<Stream>> OpenStagedReadAsync(CancellationToken cancellationToken = default)
        {
            lock (this.gate) { if (this.disposed || this.publication is not null) { return Result<Stream>.Failure(Error(CacheErrorCode.Contract, "The staging session is no longer readable.")); } }

            await this.Destination.SealAsync(cancellationToken).ConfigureAwait(false);
            if (!ResourceCharge.TryReserve(2L * this.owner.options.ChunkSize, out var memory, this.owner.transient, this.owner.ResidentFor(this.Definition)))
            { return Result<Stream>.Failure(Error(CacheErrorCode.Capacity, "Staged reader buffer capacity is exhausted.")); }

            var result = await this.owner.Run(db =>
            {
                var payload = this.owner.Required<PayloadState>(db, "payloads", this.Version.Value);
                if (payload.State != "staging") { throw Conflict(); }

                payload.Pins++; payload.PinOwner = this.owner.instance;
                this.owner.SavePayload(db, payload);
                return (Stream)new PayloadStream(this.owner, payload, memory);
            }, cancellationToken).ConfigureAwait(false);
            if (result.IsFailure) { memory.Dispose(); }

            return result;
        }

        public ValueTask<Result<CacheWriteOutcome>> PublishAsync(CacheEntryMetadata metadata, CancellationToken cancellationToken = default)
        {
            lock (this.gate)
            {
                if (this.disposed || this.publication is not null) { return ValueTask.FromResult(Result<CacheWriteOutcome>.Failure(Error(CacheErrorCode.Contract, "A staged generation permits one publication attempt."))); }

                this.publication = this.PublishCoreAsync(metadata, cancellationToken);
                return new(this.publication);
            }
        }

        private async Task<Result<CacheWriteOutcome>> PublishCoreAsync(CacheEntryMetadata metadata, CancellationToken token)
        {
            try
            {
                await this.Destination.SealAsync(token).ConfigureAwait(false);
                return await this.owner.PublishAsync(this, metadata, token).ConfigureAwait(false);
            }
            catch (CacheTransferException ex) { return Result<CacheWriteOutcome>.Failure(Error(ex.Code, "The staged cache transfer could not complete.")); }
            catch (InvalidOperationException) { return Result<CacheWriteOutcome>.Failure(Error(CacheErrorCode.Contract, "The staging stream failed or was prematurely closed.")); }
        }

        public ValueTask DisposeAsync()
        {
            lock (this.gate)
            {
                if (this.disposal is not null) { return new(this.disposal); }

                this.disposed = true;
                return new(this.disposal = this.DisposeCoreAsync(this.publication));
            }
        }

        private async Task DisposeCoreAsync(Task<Result<CacheWriteOutcome>> pending)
        {
            try
            {
                if (pending is not null) { try { await pending.ConfigureAwait(false); } catch { /* Native receipt decides whether abort is safe. */ } }

                await this.Destination.DisposeAsync().ConfigureAwait(false);
                await this.owner.AbortAsync(this).ConfigureAwait(false);
            }
            finally { this.admission.Dispose(); this.memory.Dispose(); }
        }
    }

    private sealed class StagingStream(LiteDbCacheStoreProvider owner, WriteSession session) : Stream
    {
        private readonly SemaphoreSlim gate = new(1);
        private readonly byte[] buffer = new byte[owner.options.ChunkSize];
        private readonly IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        private int buffered;
        private long length;
        private bool sealedStream;
        private bool failed;
        private bool disposed;
        public int Chunks { get; private set; }

        public string StoredHash { get; private set; }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => !this.sealedStream && !this.disposed && !this.failed;
        public override long Length => this.length;
        public override long Position { get => this.length; set => throw new NotSupportedException(); }

        public override void Write(byte[] buffer, int offset, int count) => this.WriteAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => this.WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> source, CancellationToken cancellationToken = default)
        {
            await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (!this.CanWrite) { throw new InvalidOperationException("The cache staging stream is closed or sealed."); }

                var reserved = await owner.ReserveStageAsync(session, source.Length, cancellationToken).ConfigureAwait(false);
                Check(reserved);
                this.length += source.Length;
                this.hash.AppendData(source.Span);
                while (!source.IsEmpty)
                {
                    var take = Math.Min(this.buffer.Length - this.buffered, source.Length);
                    source[..take].CopyTo(this.buffer.AsMemory(this.buffered));
                    this.buffered += take; source = source[take..];
                    if (this.buffered == this.buffer.Length) { await this.FlushChunkAsync(cancellationToken).ConfigureAwait(false); }
                }
            }
            catch { this.failed = true; throw; }
            finally { this.gate.Release(); }
        }

        public async Task SealAsync(CancellationToken token)
        {
            await this.gate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                if (this.disposed || this.failed) { throw new InvalidOperationException("The cache staging stream cannot be sealed."); }

                if (this.sealedStream) { return; }

                if (this.buffered > 0) { await this.FlushChunkAsync(token).ConfigureAwait(false); }

                this.StoredHash = Convert.ToHexStringLower(this.hash.GetHashAndReset());
                this.sealedStream = true;
            }
            catch { this.failed = true; throw; }
            finally { this.gate.Release(); }
        }

        private async Task FlushChunkAsync(CancellationToken token)
        {
            Check(await owner.AppendAsync(session, this.buffer.AsSpan(0, this.buffered).ToArray(), this.Chunks, token).ConfigureAwait(false));
            this.Chunks++; this.buffered = 0;
        }

        public override void Flush() { ObjectDisposedException.ThrowIf(this.disposed, this); }

        public override Task FlushAsync(CancellationToken cancellationToken) { cancellationToken.ThrowIfCancellationRequested(); this.Flush(); return Task.CompletedTask; }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        protected override void Dispose(bool disposing)
        {
            if (disposing && !this.disposed) { this.disposed = true; this.hash.Dispose(); }

            base.Dispose(disposing);
        }
    }

    private sealed class PayloadStream(LiteDbCacheStoreProvider owner, PayloadState payload, ResourceCharge memory = null) : Stream
    {
        private readonly SemaphoreSlim gate = new(1);
        private long position;
        private int ordinal = -1;
        private byte[] chunk;
        private int disposed;
        public override bool CanRead => Volatile.Read(ref this.disposed) == 0;
        public override bool CanSeek => this.CanRead;
        public override bool CanWrite => false;
        public override long Length => payload.Length;
        public override long Position { get => this.position; set => this.Seek(value, SeekOrigin.Begin); }

        public override int Read(byte[] buffer, int offset, int count) => this.ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => this.ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        public override async ValueTask<int> ReadAsync(Memory<byte> destination, CancellationToken cancellationToken = default)
        {
            await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                ObjectDisposedException.ThrowIf(!this.CanRead, this);
                if (destination.IsEmpty || this.position >= this.Length) { return 0; }

                var selected = checked((int)(this.position / payload.ChunkSize));
                if (this.ordinal != selected)
                {
                    var result = await owner.ReadChunkAsync(payload.Id, selected, cancellationToken).ConfigureAwait(false);
                    Check(result);
                    this.chunk = result.Value; this.ordinal = selected;
                }

                var offset = (int)(this.position % payload.ChunkSize);
                var take = (int)Math.Min(Math.Min(destination.Length, this.chunk.Length - offset), this.Length - this.position);
                if (take <= 0) { throw new CacheTransferException(CacheErrorCode.Corruption); }

                this.chunk.AsMemory(offset, take).CopyTo(destination);
                this.position += take;
                return take;
            }
            finally { this.gate.Release(); }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            ObjectDisposedException.ThrowIf(!this.CanRead, this);
            var next = checked(origin switch { SeekOrigin.Begin => offset, SeekOrigin.Current => this.position + offset, SeekOrigin.End => this.Length + offset, _ => throw new ArgumentException("Unknown seek origin.", nameof(origin)) });
            if (next < 0 || next > this.Length) { throw new IOException("The native cache seek is outside its immutable snapshot."); }

            return this.position = next;
        }

        public override void Flush() { }

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref this.disposed, 1) != 0) { return; }

            await this.gate.WaitAsync().ConfigureAwait(false);
            try { await owner.ReleaseReaderAsync(payload.Id).ConfigureAwait(false); this.chunk = null; }
            finally { memory?.Dispose(); this.gate.Release(); }

            GC.SuppressFinalize(this);
        }

        protected override void Dispose(bool disposing) { if (disposing) { this.DisposeAsync().AsTask().GetAwaiter().GetResult(); } base.Dispose(disposing); }
    }

    private sealed class ReadHandle : ICacheProviderRangeReadHandle
    {
        private readonly PayloadStream stream;
        private readonly CacheAdmissionLease admission;
        private readonly ResourceCharge memory;
        private int disposed;
        public ReadHandle(LiteDbCacheStoreProvider owner, PayloadState payload, CacheEntryMetadata metadata, CacheAdmissionLease admission, ResourceCharge memory)
        {
            this.stream = new(owner, payload); this.Metadata = metadata; this.admission = admission; this.memory = memory;
        }

        public Stream Stream => this.stream;
        public CacheEntryMetadata Metadata { get; }

        public ValueTask<Result> PositionForRangeAsync(long storedOffset, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (storedOffset < 0 || storedOffset > this.stream.Length) { return ValueTask.FromResult(Result.Failure(Error(CacheErrorCode.Validation, "The range offset is outside the stored snapshot."))); }

            this.stream.Seek(storedOffset, SeekOrigin.Begin);
            return ValueTask.FromResult(Result.Success());
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref this.disposed, 1) != 0) { return; }

            try { await this.stream.DisposeAsync().ConfigureAwait(false); }
            finally { this.admission.Dispose(); this.memory.Dispose(); }
        }
    }

    private static void Check(IResult result)
    {
        if (result.IsFailure) { throw new CacheTransferException(result.Errors.OfType<CacheError>().FirstOrDefault()?.Code ?? CacheErrorCode.ProviderUnavailable); }
    }

    private readonly record struct PreparedMetadata(CacheEntryMetadata Metadata, long ChargeBytes);
    private static bool ValidHash(string hash) => hash?.Length == 64 && hash.All(char.IsAsciiHexDigit);
    private Result<PreparedMetadata> PrepareMetadata(WriteSession session, CacheEntryMetadata supplied)
    {
        if (supplied is null || supplied.Identity != session.Request.Identity || supplied.Version != session.Version ||
            supplied.StoredSizeBytes != session.Destination.Length || supplied.LogicalSizeBytes < 0 || supplied.EncodedSizeBytes < 0 ||
            supplied.DecodedSizeBytes is < 0 || supplied.ItemCount is < 0 || supplied.Dimensions is null ||
            supplied.Dimensions.Count > 16 || !Enum.IsDefined(supplied.Priority) || supplied.EnvelopeVersion <= 0 ||
            supplied.CodecVersion <= 0)
        {
            return Result<PreparedMetadata>.Failure(new CacheError(CacheErrorCode.Contract, "Invalid staged cache metadata or snapshot identity."));
        }

        var state = session.Definition;
        var limits = state.Policies.Limits;
        if (supplied.LogicalSizeBytes > limits.MaxLogicalBytes || supplied.EncodedSizeBytes > limits.MaxEncodedBytes ||
            supplied.DecodedSizeBytes > limits.MaxDecodedBytes)
        {
            return Result<PreparedMetadata>.Failure(Error(CacheErrorCode.Capacity, "Native cache entry limits are exhausted."));
        }

        var absolute = supplied.AbsoluteExpiresAt - supplied.PublishedAt;
        var initial = supplied.ExpiresAt - supplied.PublishedAt;
        var freshness = state.Policies.Expiration.SlidingEnabled ? state.Policies.Expiration.AbsoluteCap : state.Policies.Expiration.AbsoluteTtl;
        if (absolute <= TimeSpan.Zero || absolute > freshness || initial <= TimeSpan.Zero || initial > absolute ||
            (supplied.SlidingIdleTimeout.HasValue && (!state.Policies.Expiration.SlidingEnabled ||
                supplied.SlidingIdleTimeout.Value <= TimeSpan.Zero || supplied.SlidingIdleTimeout.Value > state.Policies.Expiration.SlidingIdleTimeout)) ||
            (supplied.IsNotFound && (!state.Policies.NegativeCaching.Enabled || absolute > state.Policies.NegativeCaching.Ttl ||
                supplied.StoredSizeBytes != 0 || supplied.EncodedSizeBytes != 0 || supplied.LogicalSizeBytes != 0)))
        {
            return Result<PreparedMetadata>.Failure(new CacheError(CacheErrorCode.Contract, "Entry freshness or absence policy exceeds the collection contract."));
        }

        try
        {
            var definition = state;
            if (CacheIdentityNormalizer.Name(supplied.ContractId) != definition.ContractId ||
                CacheIdentityNormalizer.Key(supplied.DisplayKey ?? supplied.Identity.Key) != supplied.Identity.Key)
            {
                return Result<PreparedMetadata>.Failure(new CacheError(CacheErrorCode.Contract, "Stored value alias or display identity does not match its binding."));
            }

            if (supplied.CodecId is not null)
            {
                _ = CacheIdentityNormalizer.Name(supplied.CodecId);
            }

            if (supplied.Compression is not null)
            {
                _ = CacheIdentityNormalizer.Name(supplied.Compression);
            }

            if ((supplied.IsNotFound && (supplied.PayloadHash is not null || supplied.StoredHash is not null)) ||
                (!supplied.IsNotFound && (!ValidHash(supplied.PayloadHash) || !ValidHash(supplied.StoredHash))))
            {
                return Result<PreparedMetadata>.Failure(new CacheError(CacheErrorCode.Contract, "Invalid entry integrity metadata."));
            }

            var dimensions = CacheBoundedSnapshot.Copy(supplied.Dimensions, 16, nameof(supplied.Dimensions));
            var dimensionsValid = CacheSelectionValidator.ValidateDimensions(dimensions, definition.Dimensions);
            if (dimensionsValid.IsFailure)
            {
                return Result<PreparedMetadata>.Failure(dimensionsValid);
            }

            var characters = supplied.Identity.Key.Length + (supplied.DisplayKey?.Length ?? 0) +
                (supplied.ContentType?.Length ?? 0) + dimensions.Sum(d => d.Name.Length + d.Values.Sum(v => v.CanonicalValue?.Length ?? 0));
            if (characters > limits.MaxEntryMetadataBytes || supplied.ContentType?.Length > 256 ||
                (supplied.ContentType is not null && !System.Net.Http.Headers.MediaTypeHeaderValue.TryParse(supplied.ContentType, out _)))
            {
                return Result<PreparedMetadata>.Failure(new CacheError(CacheErrorCode.Validation, "Entry metadata exceeds its portable bounds."));
            }

            if (!supplied.IsNotFound && (supplied.StoredHash?.Length != 64 ||
                !StringComparer.OrdinalIgnoreCase.Equals(supplied.StoredHash, session.Destination.StoredHash)))
            {
                return Result<PreparedMetadata>.Failure(new CacheError(CacheErrorCode.Corruption, "Staged representation integrity failed."));
            }

            var now = this.timeProvider.GetUtcNow();
            var metadata = supplied with
            {
                Dimensions = dimensions, ContractId = definition.ContractId,
                DisplayKey = supplied.DisplayKey ?? supplied.Identity.Key,
                PublishedAt = now, LastAccessAt = supplied.HasForegroundAccess ? Min(supplied.LastAccessAt, now) : now,
                AbsoluteExpiresAt = now.Add(absolute), ExpiresAt = now.Add(initial),
                Priority = supplied.IsNotFound ? CacheEvictionPriority.Low : supplied.Priority
            };
            if (!this.transient.TryReserve(1024 * 1024, out var scratch))
            {
                return Result<PreparedMetadata>.Failure(Error(CacheErrorCode.Capacity, "Native cache entry limits are exhausted."));
            }

            long serializedBytes;
            using (scratch)
            {
                serializedBytes = JsonSerializer.SerializeToUtf8Bytes(metadata).LongLength;
                if (serializedBytes + 128 > limits.MaxEntryMetadataBytes)
                {
                    return Result<PreparedMetadata>.Failure(new CacheError(CacheErrorCode.Validation, "Final serialized entry metadata exceeds its byte bound."));
                }
            }

            return Result<PreparedMetadata>.Success(new(metadata, Math.Max(4096, checked(serializedBytes * 4))));
        }
        catch (ArgumentException)
        {
            return Result<PreparedMetadata>.Failure(new CacheError(CacheErrorCode.Validation, "Entry metadata contains invalid identifiers or values."));
        }
    }

}
