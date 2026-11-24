// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.LiteDB.ActiveEntity;

using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Common;

[ActiveEntityFeatures(ActiveEntityFeatures.QueryDsl)]
public partial class LedgerEntry : ActiveEntity<LedgerEntry, Guid>, IConcurrency, IAuditable
{
    /// <summary>Gets the callback recorder for the current test execution flow.</summary>
    public static AsyncLocal<List<string>> CallbackStages { get; } = new();

    public string Name { get; set; }

    public int Amount { get; set; }

    public Guid ConcurrencyVersion { get; set; }

    public DateTime RecordedAt { get; set; }

    public decimal Balance { get; set; }

    public byte[] Content { get; set; }

    public LedgerAddress Address { get; set; }

    public AuditState AuditState { get; set; } = new();

    public LedgerItem<Guid> Reference { get; set; }

    protected override Task<Result> OnBeforeUpsertAsync(IActiveEntityEntityProvider<LedgerEntry, Guid> provider, CancellationToken ct)
    {
        CallbackStages.Value?.Add("before-upsert");
        return Task.FromResult(Result.Success());
    }

    protected override Task<Result> OnBeforeInsertAsync(IActiveEntityEntityProvider<LedgerEntry, Guid> provider, CancellationToken ct)
    {
        CallbackStages.Value?.Add("before-insert");
        return Task.FromResult(Result.Success());
    }

    protected override Task<Result> OnAfterInsertAsync(IActiveEntityEntityProvider<LedgerEntry, Guid> provider, CancellationToken ct)
    {
        CallbackStages.Value?.Add("after-insert");
        return Task.FromResult(Result.Success());
    }

    protected override Task<Result> OnAfterUpsertAsync(IActiveEntityEntityProvider<LedgerEntry, Guid> provider, CancellationToken ct)
    {
        CallbackStages.Value?.Add("after-upsert");
        return Task.FromResult(Result.Success());
    }
}

public class LedgerAddress
{
    public string City { get; set; }
}

[TypedEntityId<Guid>]
public partial class TypedLedgerEntry : ActiveEntity<TypedLedgerEntry, TypedLedgerEntryId>
{
    public string Name { get; set; }
}

[TypedEntityId<int>]
public partial class IntegerLedgerEntry : ActiveEntity<IntegerLedgerEntry, IntegerLedgerEntryId>
{
    public string Name { get; set; }
}

[TypedEntityId<long>]
public partial class LongLedgerEntry : ActiveEntity<LongLedgerEntry, LongLedgerEntryId>
{
    public string Name { get; set; }
}

public partial class StringLedgerEntry : ActiveEntity<StringLedgerEntry, StringLedgerEntryId>
{
    public string Name { get; set; }
}

// The current string-ID generator emits duplicate conversions. Exercise its public ID shape without changing it.
public class StringLedgerEntryId : EntityId<string>
{
    public override string Value { get; protected set; }

    public static StringLedgerEntryId Create(string value) => new() { Value = value };

    protected override IEnumerable<object> GetAtomicValues() => [this.Value];
}

public class LedgerItem<TId> : ActiveEntity<LedgerItem<TId>, TId>
{
    public string Name { get; set; }
}

public class LedgerEvent : DomainEventBase;
