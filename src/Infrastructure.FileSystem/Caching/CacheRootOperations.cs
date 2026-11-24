// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.FileSystem.Caching;

using System.Buffers.Binary;
using System.Text.Json;
using BridgingIT.DevKit.Application.Caching;

/// <summary>Persists a tracked operation with its selection fingerprint, execution claim and any unresolved mutation checkpoint.</summary>
public sealed record CacheNativeOperationRecord(int Format, CacheProviderOperation Value, string SelectionHash, string ClaimNode,
    DateTimeOffset? ClaimUntil, string PendingCreditId = null)
{
    /// <summary>Rejects mismatched operation IDs, malformed revisions, invalid states or a running operation without a valid execution claim.</summary>
    public void Validate(string id)
    {
        if (this.Format != 1 || this.Value is null || this.Value.Id != id || this.Value.Request is null || this.Value.Request.OperationId != id ||
            this.Value.Request.Continuation is not null || !Guid.TryParseExact(id, "N", out _) || !Guid.TryParseExact(this.Value.Revision, "N", out _) ||
            !Enum.IsDefined(this.Value.State) || this.Value.Cutoff != this.Value.Request.Cutoff || this.Value.Collection != this.Value.Request.Collection ||
            this.SelectionHash?.Length != 64 || !this.SelectionHash.All(char.IsAsciiHexDigit) ||
            (this.Value.State == CacheOperationState.Running && (string.IsNullOrEmpty(this.ClaimNode) || !this.ClaimUntil.HasValue)) ||
            (this.PendingCreditId is not null && !Guid.TryParseExact(this.PendingCreditId, "N", out _)))
        {
            throw new CacheTransferException(CacheErrorCode.Corruption);
        }
    }
}

/// <summary>Owns a bounded decoded operation page and releases its retained memory charges on disposal.</summary>
/// <example><code>using var page = await ledger.GetOperationsAsync(authorizedScopes, 16, token);</code></example>
public sealed class CacheNativeOperationPage(IReadOnlyList<CacheNativeOperationRecord> values, IReadOnlyList<ResourceCharge> charges) : IDisposable
{
    /// <summary>Gets decoded operation records whose memory remains admitted until this page is disposed.</summary>
    public IReadOnlyList<CacheNativeOperationRecord> Values => values;

    /// <inheritdoc />
    public void Dispose() { foreach (var memory in charges) { memory.Dispose(); } }
}