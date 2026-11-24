// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Caching;

using System.Data.Common;
using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

/// <summary>Verifies persisted metadata is bounded by database projections before managed materialization.</summary>
public sealed class CachePersistedJsonTests
{
    /// <summary>Checks metadata-only validation and payload acquisition use bounded JSON and reject oversized persisted text.</summary>
    /// <param name="openPayload">Whether the same snapshot also acquires inline payload bytes.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Lookup_PersistedMetadata_UsesBoundedProjectionAndRejectsOversizedText(bool openPayload)
    {
        var commands = new Commands();
        await using var fixture = await CacheRelationalTestStore.CreateAsync(interceptor: commands);
        (await fixture.WriteAsync("value", [1, 2, 3])).IsSuccess.ShouldBeTrue();
        commands.Queries.Clear();

        var valid = await fixture.Provider.LookupAsync(fixture.Identity("value"), new(OpenPayload: openPayload, ObserveAccess: false));

        valid.IsSuccess.ShouldBeTrue(Errors(valid));
        if (valid.Value.Handle is not null) { await valid.Value.Handle.DisposeAsync(); }

        AssertBounded(commands.Queries, "MetadataJson");
        await using (var corrupt = fixture.Context())
        {
            var oversized = new string(' ', 2 * 1024 * 1024);
            await corrupt.CacheEntries.ExecuteUpdateAsync(set => set.SetProperty(entry => entry.MetadataJson, oversized));
        }

        var result = await fixture.Provider.LookupAsync(fixture.Identity("value"), new(OpenPayload: openPayload, ObserveAccess: false));

        result.Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.Corruption);
    }

    /// <summary>Checks retained schema iteration projects a finite prefix and rejects corruption before selection evaluation.</summary>
    [Fact]
    public async Task Query_PersistedSchema_UsesBoundedProjectionAndRejectsOversizedText()
    {
        var commands = new Commands();
        await using var fixture = await CacheRelationalTestStore.CreateAsync(interceptor: commands);
        commands.Queries.Clear();
        var query = new CacheMetadataQuery("test", "items", CacheSelection.Pattern("*"), DateTimeOffset.UtcNow);

        (await fixture.Provider.QueryAsync(query)).IsSuccess.ShouldBeTrue();

        AssertBounded(commands.Queries, "SchemaJson");
        await using (var corrupt = fixture.Context())
        {
            var oversized = new string(' ', 2 * 1024 * 1024);
            await corrupt.CacheContracts.ExecuteUpdateAsync(set => set.SetProperty(contract => contract.SchemaJson, oversized));
        }

        var result = await fixture.Provider.QueryAsync(query);

        result.Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.Compatibility);
    }

    /// <summary>Checks provider startup reads bounded retained schemas without loading unused serialized policy snapshots.</summary>
    [Fact]
    public async Task Initialize_RetainedContract_ProjectsBoundedSchemaWithoutUnusedPolicyJson()
    {
        var commands = new Commands();
        await using var fixture = await CacheRelationalTestStore.CreateAsync(interceptor: commands);
        await using var first = new EntityFrameworkCacheStoreProvider("first", new() { ChunkSize = 65536 }, fixture.Context);
        commands.Queries.Clear();

        (await first.InitializeAsync([fixture.Definition])).IsSuccess.ShouldBeTrue();

        AssertBounded(commands.Queries, "SchemaJson");
        commands.Queries.ShouldAllBe(command => !command.Contains("PolicyJson", StringComparison.Ordinal));
        await using (var corrupt = fixture.Context())
        {
            var oversized = new string(' ', 2 * 1024 * 1024);
            await corrupt.CacheContracts.ExecuteUpdateAsync(set => set.SetProperty(contract => contract.SchemaJson, oversized));
        }

        await using var second = new EntityFrameworkCacheStoreProvider("second", new() { ChunkSize = 65536 }, fixture.Context);
        var result = await second.InitializeAsync([fixture.Definition]);

        result.Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.Compatibility);
    }

    /// <summary>Checks operation lookup and listing select bounded JSON without changing valid status records.</summary>
    /// <param name="list">Whether to list a page rather than retrieve one exact operation.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Operations_PersistedMetadata_UsesBoundedProjectionAndRejectsOversizedText(bool list)
    {
        var commands = new Commands();
        await using var fixture = await CacheRelationalTestStore.CreateAsync(interceptor: commands);
        var request = new CacheProviderInvalidation("test", "items", CacheSelection.Pattern("*"), DateTimeOffset.UtcNow);
        var created = await fixture.Provider.SaveOperationAsync(new()
        {
            Id = Guid.NewGuid().ToString("N"), Collection = "items", Cutoff = request.Cutoff, Request = request, State = CacheOperationState.Queued
        });
        created.IsSuccess.ShouldBeTrue(Errors(created));
        commands.Queries.Clear();

        if (list) { (await fixture.Provider.GetOperationsAsync()).Value.Single().Id.ShouldBe(created.Value.Id); }
        else { (await fixture.Provider.GetOperationAsync(created.Value.Id)).Value.Id.ShouldBe(created.Value.Id); }

        AssertBounded(commands.Queries, "MetadataJson");
        await using (var corrupt = fixture.Context())
        {
            var oversized = new string(' ', 2 * 1024 * 1024);
            await corrupt.CacheOperations.ExecuteUpdateAsync(set => set.SetProperty(operation => operation.MetadataJson, oversized));
        }

        var code = list
            ? (await fixture.Provider.GetOperationsAsync()).Errors.OfType<CacheError>().Single().Code
            : (await fixture.Provider.GetOperationAsync(created.Value.Id)).Errors.OfType<CacheError>().Single().Code;

        code.ShouldBe(CacheErrorCode.Corruption);
    }

    /// <summary>Checks an operation page cannot read persisted records without the declared memory admission.</summary>
    [Fact]
    public async Task OperationPage_InsufficientResidentBudget_RejectsBeforeReadingRows()
    {
        var commands = new Commands();
        await using var fixture = await CacheRelationalTestStore.CreateAsync(provider: new() { ChunkSize = 65536, MaxResidentBytes = 256 * CacheDefaults.KiB }, interceptor: commands);
        commands.Queries.Clear();

        var result = await fixture.Provider.GetOperationsAsync();

        result.Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.Capacity);
        commands.Queries.ShouldBeEmpty();
    }

    private static string Errors(IResult result) => string.Join(", ", result.Errors.Select(error => error.Message));

    private static void AssertBounded(IReadOnlyList<string> commands, string column)
    {
        var reads = commands.Where(command => command.Contains(column, StringComparison.Ordinal)).ToArray();
        reads.ShouldNotBeEmpty();
        reads.ShouldAllBe(command => command.Contains("substr", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class Commands : DbCommandInterceptor
    {
        public List<string> Queries { get; } = [];

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            this.Queries.Add(command.CommandText);
            return ValueTask.FromResult(result);
        }
    }
}