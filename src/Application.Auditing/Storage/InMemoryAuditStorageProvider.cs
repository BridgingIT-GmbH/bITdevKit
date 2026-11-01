// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using BridgingIT.DevKit.Common;

/// <summary>Bounded process-local original document storage. It is neither crash-durable nor shared across nodes.</summary>
/// <example><code>var provider = new InMemoryAuditStorageProvider(options);</code></example>
public sealed partial class InMemoryAuditStorageProvider(AuditingOptions options, AuditFieldCatalog catalog = null, AuditRetentionOptions retention = null, TimeProvider time = null, AuditIntegrityReadPolicy integrity = null) : IAuditingStorageProvider
{
    private readonly object gate = new();
    private readonly SemaphoreSlim appendGate = new(1, 1);
    private readonly AuditIntegrityReadPolicy readPolicy = integrity ?? new();
    private readonly Dictionary<Guid, MemoryEntry> records = [];
    private readonly Guid receiptEpoch = Guid.NewGuid();
    private long sequence;
    private long retainedBytes;
    private readonly TimeProvider clock = time ?? TimeProvider.System;
    private long evicted;

    /// <summary>Whole retained graphs lost to bounded memory capacity, separate from configured retention.</summary>
    /// <example><code>var lost = provider.EvictedRecords;</code></example>
    public long EvictedRecords { get { lock (this.gate) return this.evicted; } }

    /// <summary>Returns process-local append acknowledgments; memory presence is not crash durability.</summary>
    /// <example><code>var delivery = provider.DeliveryStatus;</code></example>
    public AuditFlushResult DeliveryStatus { get { lock (this.gate) return new() { ReceiptEpoch = this.receiptEpoch, ThroughSequence = this.sequence, Persisted = this.sequence }; } }

    /// <summary>Validates identity and immediately admits a copied frozen document.</summary>
    /// <example><code>var receipt = await provider.AppendAsync(record);</code></example>
    public async Task<Result<AuditAppendReceipt>> AppendAsync(AuditRecord record, CancellationToken cancellationToken = default)
    {
        var frozen = AuditRecordCodec.Freeze(record);
        if (frozen.IsFailure) return Result<AuditAppendReceipt>.Failure(frozen);
        await this.appendGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (this.readPolicy.IsConfigured)
            {
                var incoming = await this.readPolicy.VerifyOriginalAsync(frozen.Value, cancellationToken).ConfigureAwait(false);
                if (incoming.IsFailure) return Result<AuditAppendReceipt>.Failure(incoming);
                if (incoming.Value != "Verified") return Result<AuditAppendReceipt>.Failure(new AuditError("AuditIntegrityMissingProtection"));
            }

            AuditRecord existing;
            lock (this.gate) existing = this.records.TryGetValue(record.AuditId, out var row) ? row.Record : null;
            if (existing is not null && (existing.IntegrityProtection is not null || this.readPolicy.IsConfigured))
            {
                var verified = await this.readPolicy.VerifyOriginalAsync(existing, cancellationToken).ConfigureAwait(false);
                if (verified.IsFailure) return Result<AuditAppendReceipt>.Failure(verified);
                if (verified.Value is not ("Verified" or "NotProtected")) return Result<AuditAppendReceipt>.Failure(new AuditError("AuditIntegrityInvalid"));
            }

            return await this.AppendCoreAsync(frozen.Value, cancellationToken).ConfigureAwait(false);
        }
        finally { this.appendGate.Release(); }
    }

    private Task<Result<AuditAppendReceipt>> AppendCoreAsync(AuditRecord record, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var frozen = AuditRecordCodec.Freeze(record);
        if (frozen.IsFailure) return Task.FromResult(Result<AuditAppendReceipt>.Failure(frozen));
        var bytes = AuditRecordCodec.Encode(frozen.Value);
        if (bytes.Length > options.Limits.MaxRecordBytes || bytes.Length > options.MemoryByteLimit)
            return Task.FromResult(Result<AuditAppendReceipt>.Failure(new AuditError("AuditRecordTooLarge")));
        var copy = AuditRecordCodec.Decode(bytes);
        if (copy.IsFailure) return Task.FromResult(Result<AuditAppendReceipt>.Failure(copy));
        lock (this.gate)
        {
            if (this.records.TryGetValue(record.AuditId, out var existing))
            {
                return Task.FromResult(existing.Record.ContentHash == copy.Value.ContentHash
                    ? Result<AuditAppendReceipt>.Success(existing.Receipt with { State = "AlreadyPresent" })
                    : Result<AuditAppendReceipt>.Failure(new AuditError("AuditIdentityConflict")));
            }

            var now = this.clock.GetUtcNow();
            var removeBefore = retention?.RemoveAfter is TimeSpan remove ? Math.Max(this.rejectCompletedBefore, (now - remove).UtcTicks) : this.rejectCompletedBefore;
            var archiveBefore = retention?.ArchiveAfter is TimeSpan archive ? Math.Max(this.archiveCompletedBefore, (now - archive).UtcTicks) : this.archiveCompletedBefore;
            if (record.CompletedUtc.UtcTicks < removeBefore)
                return Task.FromResult(Result<AuditAppendReceipt>.Failure(new AuditError("AuditEvidenceExpired")));
            while (this.records.Count >= options.MemoryRecordLimit || this.retainedBytes + bytes.Length > options.MemoryByteLimit)
            {
                var oldest = this.records.Values.OrderByDescending(e => e.Archived).ThenBy(e => e.Position).First();
                this.RemoveRow(oldest);
                this.evicted++;
            }

            var receipt = new AuditAppendReceipt { AuditId = record.AuditId, ReceiptEpoch = this.receiptEpoch, AcceptanceSequence = ++this.sequence };
            this.records.Add(record.AuditId, new MemoryEntry(copy.Value, receipt, bytes.Length, ++this.publishedPosition) { Archived = record.CompletedUtc.UtcTicks < archiveBefore });
            this.retainedBytes += bytes.Length;
            return Task.FromResult(Result<AuditAppendReceipt>.Success(receipt));
        }
    }

    /// <summary>Reads immutable original capture without live identity/resource lookups.</summary>
    /// <example><code>var original = await provider.FindAsync(auditId);</code></example>
    public Task<Result<AuditRecord>> FindAsync(Guid auditId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (this.gate) return Task.FromResult(this.records.TryGetValue(auditId, out var entry)
            ? Result<AuditRecord>.Success(entry.Record) : Result<AuditRecord>.Failure(new AuditError("AuditNotFound")));
    }

    /// <summary>Immediately confirms this memory instance's admissions, without claiming crash durability.</summary>
    /// <example><code>var result = await provider.FlushAsync();</code></example>
    public Task<Result<AuditFlushResult>> FlushAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (this.gate) return Task.FromResult(Result<AuditFlushResult>.Success(new() { ReceiptEpoch = this.receiptEpoch, ThroughSequence = this.sequence, Persisted = this.sequence }));
    }

    private sealed record MemoryEntry(AuditRecord Record, AuditAppendReceipt Receipt, int Bytes, long Position)
    {
        /// <summary>Whether the retained row is archived.</summary>
        /// <example><code>var value = component.Archived;</code></example>
        public bool Archived { get; set; }
    }
}
