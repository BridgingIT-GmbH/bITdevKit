// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework.Caching;

using System.Data.Common;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Caching;
using BridgingIT.DevKit.Infrastructure.Tests.Caching;
using BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework.Auditing.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

public abstract class EntityFrameworkCacheStoreTestsBase(AuditRelationalFixture fixture)
{
    protected abstract string Engine { get; }

    /// <summary>Checks bounded token, metadata and ownership projections execute on each real relational engine.</summary>
    [Fact]
    public async Task PersistedScalars_ActualEngine_UsesBoundedProjectionAndPreservesTransactionalWrites()
    {
        await using var store = await this.CreateAsync();
        await store.WriteAsync("bounded-scalars", [1, 2, 3]);
        store.Commands.Reset();

        var read = await store.Provider.LookupAsync(store.Identity("bounded-scalars"), new(ObserveAccess: false));
        read.IsSuccess.ShouldBeTrue(Errors(read));
        read.Value.State.ShouldBe(CacheReadValidationState.Valid);
        var request = new CacheOwnershipRequest(store.Identity("owned-scalars"), "owner", "interest", DateTimeOffset.UtcNow.AddSeconds(25), TimeSpan.FromSeconds(30));
        var owned = await store.Provider.AcquireOwnershipAsync(request);
        owned.IsSuccess.ShouldBeTrue(Errors(owned));
        (await store.Provider.ObserveOwnershipAsync(request, owned.Value.FlightId)).IsSuccess.ShouldBeTrue();

        var queries = store.Commands.Queries.Where(command => command.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)).ToArray();
        queries.Where(command => command.Contains("KeyPrefix", StringComparison.Ordinal)).ShouldNotBeEmpty();
        queries.Where(command => command.Contains("NodeId", StringComparison.Ordinal)).ShouldNotBeEmpty();
        queries.Where(command => command.Contains("KeyPrefix", StringComparison.Ordinal) || command.Contains("NodeId", StringComparison.Ordinal))
            .ShouldAllBe(command => command.Contains("substr", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Verifies each actual engine limits persisted text on the server, including oversized trailing spaces.</summary>
    /// <param name="kind">The metadata family read through its public provider operation.</param>
    [Theory]
    [InlineData("entry")]
    [InlineData("schema")]
    [InlineData("operation")]
    public async Task PersistedJson_OversizedTrailingSpaces_UsesBoundedProjectionAndControlledFailure(string kind)
    {
        await using var store = await this.CreateAsync();
        await store.WriteAsync("bounded-json", [1]);
        var request = new CacheProviderInvalidation("test", "items", CacheSelection.Pattern("*"), DateTimeOffset.UtcNow);
        var created = await store.Provider.SaveOperationAsync(new()
        {
            Id = Guid.NewGuid().ToString("N"), Collection = "items", Cutoff = request.Cutoff, Request = request, State = CacheOperationState.Queued
        });
        created.IsSuccess.ShouldBeTrue(Errors(created));
        store.Commands.Reset();

        if (kind == "entry")
        {
            var result = await store.Provider.LookupAsync(store.Identity("bounded-json"), new(ObserveAccess: false));
            result.IsSuccess.ShouldBeTrue(Errors(result));
        }
        else if (kind == "schema")
        {
            var result = await store.Provider.QueryAsync(new("test", "items", CacheSelection.Pattern("*"), DateTimeOffset.UtcNow));
            result.IsSuccess.ShouldBeTrue(Errors(result));
        }
        else
        {
            var result = await store.Provider.GetOperationsAsync();
            result.IsSuccess.ShouldBeTrue(Errors(result));
            result.Value.Single().Id.ShouldBe(created.Value.Id);
        }

        var column = kind == "schema" ? "SchemaJson" : "MetadataJson";
        var queries = store.Commands.Queries.Where(command => command.Contains(column, StringComparison.Ordinal)).ToArray();
        queries.ShouldNotBeEmpty();
        queries.ShouldAllBe(command => command.Contains("substr", StringComparison.OrdinalIgnoreCase));
        await using (var corrupt = store.Context())
        {
            var oversized = new string(' ', 2 * 1024 * 1024);
            if (kind == "entry") { await corrupt.CacheEntries.ExecuteUpdateAsync(set => set.SetProperty(row => row.MetadataJson, row => row.MetadataJson + oversized)); }
            else if (kind == "schema") { await corrupt.CacheContracts.ExecuteUpdateAsync(set => set.SetProperty(row => row.SchemaJson, oversized)); }
            else { await corrupt.CacheOperations.ExecuteUpdateAsync(set => set.SetProperty(row => row.MetadataJson, oversized)); }
        }

        CacheErrorCode code;
        if (kind == "entry")
        {
            code = (await store.Provider.LookupAsync(store.Identity("bounded-json"), new(ObserveAccess: false))).Errors.OfType<CacheError>().Single().Code;
            var page = await store.Provider.QueryAsync(new("test", "items", CacheSelection.Pattern("*"), DateTimeOffset.UtcNow));
            page.IsFailure.ShouldBeTrue(Errors(page));
            page.Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.Corruption);
        }
        else if (kind == "schema")
        {
            code = (await store.Provider.QueryAsync(new("test", "items", CacheSelection.Pattern("*"), DateTimeOffset.UtcNow))).Errors.OfType<CacheError>().Single().Code;
        }
        else
        {
            code = (await store.Provider.GetOperationsAsync()).Errors.OfType<CacheError>().Single().Code;
        }

        code.ShouldBe(kind == "schema" ? CacheErrorCode.Compatibility : CacheErrorCode.Corruption);
    }

    [Fact]
    public async Task OwnershipJson_TracksRemoteUnicodeInterestsAndTheirExactMetadataCharges()
    {
        await using var store = await this.CreateAsync();
        await using var peer = new EntityFrameworkCacheStoreProvider("json-peer", new() { Engine = this.Engine }, store.Context);
        (await peer.InitializeAsync([store.Definition])).IsSuccess.ShouldBeTrue();
        var identity = store.Identity("shared-json");
        var deadline = DateTimeOffset.UtcNow.AddSeconds(25);
        var owned = await store.Provider.AcquireOwnershipAsync(new(identity, "owner", "owner", deadline, TimeSpan.FromSeconds(30)));
        owned.IsSuccess.ShouldBeTrue(Errors(owned));
        var before = (await store.Provider.GetQuotaAsync()).Value.MetadataBytes;
        var joined = await peer.AcquireOwnershipAsync(new(identity, "peer", "naïve:peer", deadline, TimeSpan.FromSeconds(30)));
        joined.IsSuccess.ShouldBeTrue(Errors(joined));
        joined.Value.State.ShouldBe(CacheOwnershipState.Pending);
        (await store.Provider.GetQuotaAsync()).Value.MetadataBytes.ShouldBe(before + CacheOwnershipInterestState.InterestMetadataBytes);
        await using (var verify = store.Context())
        {
            var json = (await verify.CacheOwnerships.SingleAsync()).InterestsJson;
            json.ShouldAllBe(character => character <= 127);
            CacheOwnershipInterestState.Read(json).Select(interest => interest.InterestId).ShouldContain("naïve:peer");
        }

        (await store.Provider.ReleaseInterestAsync(identity, owned.Value.FlightId, "owner")).IsSuccess.ShouldBeTrue();
        (await store.Provider.RenewOwnershipAsync(identity, owned.Value.Owner.Value)).Value.State.ShouldBe(CacheOwnershipState.Owned);
        (await peer.ReleaseInterestAsync(identity, owned.Value.FlightId, "naïve:peer")).IsSuccess.ShouldBeTrue();
        (await store.Provider.RenewOwnershipAsync(identity, owned.Value.Owner.Value)).Value.State.ShouldBe(CacheOwnershipState.Lost);
    }

    [Fact]
    public async Task RefreshCandidates_UseBoundedBinaryKeysetsAndPersistConditionalBackoff()
    {
        var clock = new Clock(DateTimeOffset.UtcNow);
        await using var store = await this.CreateAsync(timeProvider: clock);
        var first = await store.WriteAsync("refresh-one", [1], foreground: true);
        await store.WriteAsync("refresh-two", [2], foreground: true);
        await store.WriteAsync("inactive", [3]);
        // TTL starts at publication, so age published entries instead of backdating their input metadata.
        clock.Advance(TimeSpan.FromSeconds(49));
        var query = new CacheRefreshQuery("test", "items", "1", clock.GetUtcNow(), TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(10), Limit: 1);
        var selected = new List<CacheEntryMetadata>();
        var pages = 0;
        do
        {
            var result = await store.Provider.QueryRefreshAsync(query);
            result.IsSuccess.ShouldBeTrue(Errors(result));
            result.Value.CandidatesExamined.ShouldBeLessThanOrEqualTo(1);
            selected.AddRange(result.Value.Entries);
            query = query with { Continuation = result.Value.Continuation };
            (++pages <= 4).ShouldBeTrue();
        } while (query.Continuation is not null);

        selected.Select(e => e.Identity.Key).OrderBy(k => k).ShouldBe(new[] { "REFRESH-ONE", "REFRESH-TWO" });
        var identity = store.Identity("refresh-one");
        (await store.Provider.RecordRefreshAttemptAsync(new(identity, first.Version, true, clock.GetUtcNow()))).IsSuccess.ShouldBeTrue();
        var lookup = await store.Provider.LookupAsync(identity, new(ObserveAccess: false));
        lookup.Value.Metadata.RefreshFailureCount.ShouldBe(1);
        lookup.Value.Metadata.RefreshNotBefore.ShouldNotBeNull();
        var backedOff = await store.Provider.QueryRefreshAsync(query with { Limit = 10 });
        backedOff.IsSuccess.ShouldBeTrue(Errors(backedOff));
        backedOff.Value.Entries.Select(e => e.Identity.Key).ShouldBe(new[] { "REFRESH-TWO" });
        (await store.Provider.RecordRefreshAttemptAsync(new(identity, first.Version, false, clock.GetUtcNow()))).IsSuccess.ShouldBeTrue();
        (await store.Provider.QueryRefreshAsync(query with { Limit = 10 })).Value.Entries.Count.ShouldBe(2);
    }

    [Fact]
    public async Task MigrationsAndCatalog_DeployAndRejectIncompatibleNativeProfiles()
    {
        await using var store = await this.CreateAsync();
        await using var context = store.Context();
        context.Database.HasPendingModelChanges().ShouldBeFalse();
        store.Provider.Capabilities.LocalHostOnly.ShouldBe(this.Engine == "Sqlite");
        store.Provider.Capabilities.NativeAsyncIO.ShouldBe(this.Engine != "Sqlite");
        await using var incompatible = new EntityFrameworkCacheStoreProvider("other", new() { Engine = this.Engine, MaxStorageBytes = CacheDefaults.GiB }, store.Context);

        (await incompatible.InitializeAsync([store.Definition])).Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.Compatibility);
        (await context.Database.GetAppliedMigrationsAsync()).ShouldContain("202611100001_TestHostCacheInitial");
    }

    [Fact]
    public async Task SmallHit_IsOneIndexedSnapshotQueryWithoutStatisticsWrites()
    {
        await using var store = await this.CreateAsync();
        var written = await store.WriteAsync("Straße:İ:é", [1, 2, 3]);
        store.Commands.Reset();

        var result = await store.Provider.LookupAsync(store.Identity("straße:İ:É"), new(OpenPayload: true));

        result.IsSuccess.ShouldBeTrue(Errors(result));
        await using var handle = result.Value.Handle;
        result.Value.Metadata.Version.ShouldBe(written.Version);
        store.Commands.Reads.ShouldBe(1);
        store.Commands.Writes.ShouldBe(0);
        using var bytes = new MemoryStream();
        await handle.Stream.CopyToAsync(bytes);
        bytes.ToArray().ShouldBe(new byte[] { 1, 2, 3 });
    }

    [Fact]
    public async Task PortableSelections_PreserveNullOmissionSetsSignedIntegersAndUtcTicks()
    {
        CacheDimensionSchema[] schema = [new("tag", CacheDimensionKind.String, CacheDimensionForm.Set, true),
            new("count", CacheDimensionKind.Integer, CacheDimensionForm.Scalar), new("window", CacheDimensionKind.Timestamp, CacheDimensionForm.Interval)];
        await using var store = await this.CreateAsync(schema);
        var start = DateTimeOffset.Parse("2026-01-01T00:00:00.0000001Z");
        var end = start.AddTicks(2);
        await store.WriteAsync("prefix:one", [1], [CacheDimension.Set("tag", CacheDimensionKind.String, null, "sale"), CacheDimension.Scalar("count", long.MinValue), CacheDimension.Interval("window", start, end)]);
        await store.WriteAsync("prefix:two", [2], [CacheDimension.Set("tag", CacheDimensionKind.String, "other"), CacheDimension.Scalar("count", long.MaxValue), CacheDimension.Interval("window", end, end.AddTicks(2))]);
        await store.WriteAsync("omitted", [3]);
        var selection = CacheSelection.And(CacheSelection.Prefix("PREFIX:"), CacheSelection.Dimension("tag").ContainsAny(CacheDimensionKind.String, new object[] { null }),
            CacheSelection.Dimension("count").EqualTo(long.MinValue), CacheSelection.Dimension("window").Overlaps(start, end));

        var page = await store.Provider.QueryAsync(new("test", "items", selection, DateTimeOffset.UtcNow));

        page.IsSuccess.ShouldBeTrue(Errors(page));
        page.Value.CountIsExact.ShouldBeTrue();
        page.Value.Entries.Select(e => e.Identity.Key).ShouldBe(new[] { "PREFIX:ONE" });
        var omitted = await store.Provider.QueryAsync(new("test", "items", CacheSelection.Dimension("tag").IsAbsent(), DateTimeOffset.UtcNow));
        omitted.Value.Entries.Select(e => e.Identity.Key).ShouldBe(new[] { "OMITTED" });
    }

    [Fact]
    public async Task ConditionalReplacement_KeepsOldProtectedReaderAndReconcilesTheExactPublication()
    {
        await using var store = await this.CreateAsync();
        var original = Enumerable.Range(0, 180000).Select(i => (byte)i).ToArray();
        var old = await store.WriteAsync("key", original);
        var acquired = await store.Provider.LookupAsync(store.Identity("key"), new(OpenPayload: true));
        acquired.IsSuccess.ShouldBeTrue(Errors(acquired));
        await using var reader = acquired.Value.Handle;
        var current = await store.WriteAsync("key", [9], condition: new(old.Version));
        var stale = await store.Provider.RemoveAsync(store.Identity("key"), new(old.Version));
        stale.Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.Conflict);

        using var read = new MemoryStream();
        await reader.Stream.CopyToAsync(read);
        read.ToArray().ShouldBe(original);
        var reconciled = await store.Provider.ReconcileAsync(current.Operation);
        reconciled.Value.Version.ShouldBe(current.Version);
        (await store.Provider.GetQuotaAsync()).Value.RetiredBytes.ShouldBe(original.Length);
        await reader.DisposeAsync();
        (await store.Provider.MaintainAsync(new(100, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddSeconds(5)))).IsSuccess.ShouldBeTrue();
        (await store.Provider.GetQuotaAsync()).Value.RetiredBytes.ShouldBe(0);
    }

    [Fact]
    public async Task IndependentInstances_ContendForOneAuthoritativeOwnerWithoutDuplicateSourceAuthority()
    {
        await using var store = await this.CreateAsync();
        await using var peer = new EntityFrameworkCacheStoreProvider("peer", store.Options, store.Context);
        (await peer.InitializeAsync([store.Definition])).IsSuccess.ShouldBeTrue();
        var identity = store.Identity("expensive");
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var a = Task.Run(async () => { await start.Task; return await store.Provider.AcquireOwnershipAsync(Request("first")); });
        var b = Task.Run(async () => { await start.Task; return await peer.AcquireOwnershipAsync(Request("second")); });

        start.SetResult();
        var results = await Task.WhenAll(a, b);

        results.ShouldAllBe(r => r.IsSuccess, string.Join(",", results.Select(r => Errors(r))));
        results.Count(r => r.Value.State == CacheOwnershipState.Owned).ShouldBe(1);
        results.Count(r => r.Value.State == CacheOwnershipState.Pending).ShouldBe(1);
        results[0].Value.FlightId.ShouldBe(results[1].Value.FlightId);
        var owner = results.Single(r => r.Value.Owner.HasValue).Value;
        (await peer.RemoveAsync(identity)).IsSuccess.ShouldBeTrue();
        (await store.Provider.RenewOwnershipAsync(identity, owner.Owner.Value)).Value.State.ShouldBe(CacheOwnershipState.Lost);
        (await store.Provider.BeginWriteAsync(new(identity, owner.Fence, default, 65536, owner.Owner))).IsFailure.ShouldBeTrue();

        CacheOwnershipRequest Request(string node) => new(identity, node, node, DateTimeOffset.UtcNow.AddSeconds(25), TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task CrossContractInvalidation_FencesLateUnknownDimensionsAndDeletesOnlySelectedVersions()
    {
        await using var store = await this.CreateAsync([new("group", CacheDimensionKind.String, CacheDimensionForm.Scalar)]);
        await store.WriteAsync("one", [1], [CacheDimension.Scalar("group", "a")]);
        await store.WriteAsync("two", [2], [CacheDimension.Scalar("group", "b")]);
        var identity = store.Identity("late");
        var captured = await store.Provider.CaptureFenceAsync(identity);

        var result = await store.Provider.InvalidateAsync(new("test", "items", CacheSelection.Dimension("group").EqualTo("a"), DateTimeOffset.UtcNow));

        result.IsSuccess.ShouldBeTrue(Errors(result));
        result.Value.AffectedCount.ShouldBe(1);
        (await store.Provider.BeginWriteAsync(new(identity, captured.Value, new(CreateOnly: true), 65536))).IsFailure.ShouldBeTrue();
        (await store.Provider.LookupAsync(store.Identity("two"), new())).Value.State.ShouldBe(CacheReadValidationState.Valid);
        (await store.Provider.LookupAsync(store.Identity("one"), new())).Value.State.ShouldBe(CacheReadValidationState.Missing);
    }

    [Fact]
    public async Task CommitAcknowledgementLost_ReconcilesWithoutReplayingInputOrPublication()
    {
        var fault = new LostAcknowledgement();
        await using var store = await this.CreateAsync(fault: fault);
        var identity = store.Identity("uncertain");
        var fence = (await store.Provider.CaptureFenceAsync(identity)).Value;
        await using var session = (await store.Provider.BeginWriteAsync(new(identity, fence, default, 65536))).Value;
        await session.Stream.WriteAsync(new byte[] { 1 });
        await using (var staged = (await session.OpenStagedReadAsync()).Value) { await staged.CopyToAsync(Stream.Null); }

        var now = DateTimeOffset.UtcNow;
        var hash = Convert.ToHexStringLower(SHA256.HashData(new byte[] { 1 }));
        fault.Armed = true;
        var uncertain = await session.PublishAsync(new() { Identity = identity, DisplayKey = "uncertain", ContractId = "RAW", Version = session.Version,
            PublishedAt = now, ExpiresAt = now.AddMinutes(1), AbsoluteExpiresAt = now.AddMinutes(1), LogicalSizeBytes = 1, EncodedSizeBytes = 1, StoredSizeBytes = 1,
            CodecId = "binary", Compression = "none", PayloadHash = hash, StoredHash = hash });
        var confirmed = await store.Provider.ReconcileAsync(session.OperationId);

        uncertain.Value.StorageOutcome.ShouldBe(CacheStorageOutcome.Indeterminate);
        confirmed.Value.StorageOutcome.ShouldBe(CacheStorageOutcome.Stored);
        confirmed.Value.Version.ShouldBe(session.Version);
        fault.Failures.ShouldBe(1);
        await using var context = store.Context();
        (await context.Set<CacheEntryEntity>().CountAsync()).ShouldBe(1);
        (await context.Set<CachePayloadEntity>().CountAsync()).ShouldBe(1);
        await store.Provider.ReleaseFenceAsync(identity, fence);
    }

    private static string Errors(IResult result) => string.Join(", ", result.Errors.Select(e => e.Message));

    private async Task<Store> CreateAsync(IReadOnlyList<CacheDimensionSchema> dimensions = null, IInterceptor fault = null,
        TimeProvider timeProvider = null)
    {
        var connections = await fixture.CreateDatabaseAsync(this.Engine);
        var store = new Store(this.Engine, connections, dimensions ?? [], fault, timeProvider);
        await using (var bootstrap = store.Context())
        {
            await bootstrap.Database.MigrateAsync();
            if (this.Engine == "Sqlite") { await bootstrap.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;"); }
        }

        var initialized = await store.Provider.InitializeAsync([store.Definition]);
        initialized.IsSuccess.ShouldBeTrue(Errors(initialized));
        return store;
    }

    private sealed class Store : IAsyncDisposable
    {
        private readonly string engine;
        private readonly Func<DbConnection> connections;
        private readonly IInterceptor fault;
        private readonly TimeProvider time;

        internal Store(string engine, Func<DbConnection> connections, IReadOnlyList<CacheDimensionSchema> dimensions, IInterceptor fault,
            TimeProvider timeProvider)
        {
            this.engine = engine;
            this.connections = connections;
            this.fault = fault;
            this.time = timeProvider ?? TimeProvider.System;
            this.Options = new() { Kind = "EntityFramework", Engine = engine, ChunkSize = 65536 };
            this.Definition = new("test", "items", "1", "RAW", dimensions, new() { Dimensions = dimensions });
            this.Provider = new("database", this.Options, this.Context, this.time);
        }

        internal CacheProviderOptions Options { get; }

        internal CacheProviderCollection Definition { get; }

        internal EntityFrameworkCacheStoreProvider Provider { get; }

        internal Commands Commands { get; } = new();

        internal CacheTestHostDbContext Context()
        {
            var builder = new DbContextOptionsBuilder<CacheTestHostDbContext>();
            var connection = this.connections();
            switch (this.engine)
            {
                case "SqlServer": builder.UseSqlServer(connection, true, migration => migration.MigrationsAssembly(typeof(CacheTestHostDbContext).Assembly.GetName().Name)); break;
                case "Postgres": builder.UseNpgsql(connection, true, migration => migration.MigrationsAssembly(typeof(CacheTestHostDbContext).Assembly.GetName().Name)); break;
                default: builder.UseSqlite(connection, true, migration => migration.MigrationsAssembly(typeof(CacheTestHostDbContext).Assembly.GetName().Name)); break;
            }

            builder.AddInterceptors(this.Commands);
            if (this.fault is not null) { builder.AddInterceptors(this.fault); }

            var context = new CacheTestHostDbContext(builder.Options);
            if (this.engine == "Sqlite")
            {
                context.Database.OpenConnection();
                using var command = connection.CreateCommand();
                command.CommandText = "PRAGMA busy_timeout=5000;";
                command.ExecuteNonQuery();
            }

            return context;
        }

        internal CacheEntryIdentity Identity(string key) => new("test", "items", "1", key);

        internal async Task<(CacheEntryVersion Version, string Operation)> WriteAsync(string key, byte[] bytes,
            IReadOnlyList<CacheDimension> dimensions = null, CacheWriteCondition condition = default, bool foreground = false)
        {
            var identity = this.Identity(key);
            var fence = await this.Provider.CaptureFenceAsync(identity);
            fence.IsSuccess.ShouldBeTrue(Errors(fence));
            try
            {
                var started = await this.Provider.BeginWriteAsync(new(identity, fence.Value, condition, Math.Max(1, bytes.Length)));
                started.IsSuccess.ShouldBeTrue(Errors(started));
                await using var session = started.Value;
                await session.Stream.WriteAsync(bytes);
                var now = this.time.GetUtcNow();
                var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
                var published = await session.PublishAsync(new() { Identity = identity, DisplayKey = key, Version = session.Version, ContractId = "RAW",
                    PublishedAt = now, ExpiresAt = now.AddMinutes(1), AbsoluteExpiresAt = now.AddMinutes(1), LastAccessAt = now, HasForegroundAccess = foreground,
                    Dimensions = dimensions ?? [], LogicalSizeBytes = bytes.Length, EncodedSizeBytes = bytes.Length, StoredSizeBytes = bytes.Length,
                    CodecId = "binary", Compression = "none", PayloadHash = hash, StoredHash = hash });
                published.IsSuccess.ShouldBeTrue(Errors(published));
                return (session.Version, session.OperationId);
            }
            finally { (await this.Provider.ReleaseFenceAsync(identity, fence.Value)).IsSuccess.ShouldBeTrue(); }
        }

        public ValueTask DisposeAsync() => this.Provider.DisposeAsync();
    }

    /// <summary>Controls UTC entry age independently of database operation duration.</summary>
    /// <param name="now">The initial publication time.</param>
    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset now = now;

        /// <inheritdoc />
        public override DateTimeOffset GetUtcNow() => this.now;

        /// <summary>Advances the publication clock without waiting on wall time.</summary>
        /// <param name="elapsed">The elapsed lifetime to simulate.</param>
        public void Advance(TimeSpan elapsed) => this.now += elapsed;
    }

    private sealed class Commands : DbCommandInterceptor
    {
        internal int Reads;
        internal int Writes;

        public ConcurrentQueue<string> Queries { get; } = new();

        internal void Reset() { this.Reads = 0; this.Writes = 0; this.Queries.Clear(); }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            this.Queries.Enqueue(command.CommandText);
            if (command.CommandText.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)) { Interlocked.Increment(ref this.Reads); }
            else { Interlocked.Increment(ref this.Writes); }

            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref this.Writes);
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    private sealed class LostAcknowledgement : DbTransactionInterceptor
    {
        internal bool Armed { get; set; }

        internal int Failures { get; private set; }

        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
        {
            if (this.Armed)
            {
                this.Armed = false;
                this.Failures++;
                throw new AcknowledgementException();
            }

            return Task.CompletedTask;
        }
    }

    private sealed class AcknowledgementException : DbException;
}