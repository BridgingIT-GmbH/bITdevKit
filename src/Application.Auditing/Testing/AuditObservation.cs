// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

using System.Collections.Immutable;
using System.Text.Json;
using BridgingIT.DevKit.Common;

/// <summary>Bounded test-owned original evidence with explicit persistence confirmation.</summary>
/// <example><code>observation.AssertPresent(record => record.Key == "customers.rename");</code></example>
public sealed record AuditObservation
{
    /// <summary>The cumulative acceptance-boundary flush outcome.</summary>
    /// <example><code>var flushed = observation.Flush.IsSuccess;</code></example>
    public Result<AuditFlushResult> Flush { get; init; }

    /// <summary>All selected original stored captures, or a safe failure without a partial evidence set.</summary>
    /// <example><code>var records = observation.Evidence.Value;</code></example>
    public Result<ImmutableArray<AuditRecord>> Evidence { get; init; }

    /// <summary>Requires at least one matching original capture after confirmed persistence.</summary>
    /// <example><code>observation.AssertPresent(r => r.Actions.Any(a => a.Key == "customers.rename"));</code></example>
    public Result AssertPresent(Func<AuditRecord, bool> predicate) => this.Check(predicate, true);

    /// <summary>Requires no matching original capture; failed/pending evidence cannot prove absence.</summary>
    /// <example><code>observation.AssertAbsent(r => r.Key == "excluded.operation");</code></example>
    public Result AssertAbsent(Func<AuditRecord, bool> predicate) => this.Check(predicate, false);

    /// <summary>Checks all stored JSON string values and names without printing the forbidden text or record.</summary>
    /// <example><code>observation.AssertNoValue("test-only-secret");</code></example>
    public Result AssertNoValue(string forbidden)
    {
        if (string.IsNullOrEmpty(forbidden)) return Result.Failure(new AuditError("AuditAssertionInvalid"));
        return this.AssertAbsent(record =>
        {
            using var document = JsonDocument.Parse(AuditRecordCodec.Encode(record));
            return Contains(document.RootElement, forbidden);
        });
    }

    private Result Check(Func<AuditRecord, bool> predicate, bool expected)
    {
        if (this.Flush.IsFailure || this.Flush.Value.Pending != 0 || this.Flush.Value.Failed != 0 || this.Evidence.IsFailure)
            return Result.Failure(new AuditError("AuditEvidenceUnconfirmed"));
        try { return this.Evidence.Value.Any(predicate) == expected ? Result.Success() : Result.Failure(new AuditError("AuditExpectationFailed")); }
        catch (Exception) { return Result.Failure(new AuditError("AuditAssertionFailed")); }
    }
    private static bool Contains(JsonElement element, string forbidden) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString().Contains(forbidden, StringComparison.Ordinal),
        JsonValueKind.Object => element.EnumerateObject().Any(p => p.Name.Contains(forbidden, StringComparison.Ordinal) || Contains(p.Value, forbidden)),
        JsonValueKind.Array => element.EnumerateArray().Any(value => Contains(value, forbidden)),
        _ => false
    };
}

/// <summary>Preserves the actual business result alongside separately assessed test evidence.</summary>
/// <example><code>var result = observed.Value;</code></example>
public sealed record AuditObservedResult<T>
{
    /// <summary>The unchanged business delegate result.</summary>
    /// <example><code>var value = observed.Value;</code></example>
    public T Value { get; init; }

    /// <summary>Evidence collected only after the real invocation completes.</summary>
    /// <example><code>var evidence = observed.Observation;</code></example>
    public AuditObservation Observation { get; init; }
}
