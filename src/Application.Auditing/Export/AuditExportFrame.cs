// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Auditing;

using System.Collections.Immutable;
using BridgingIT.DevKit.Common;

/// <summary>Owns one exact formatted source expansion or control frame. Output never reserializes its measured bytes.</summary>
/// <example><code>using var frame = AuditExportFrame.FromJson(encoded); await output.WriteAsync(frame.Bytes, cancellationToken);</code></example>
public sealed class AuditExportFrame : IDisposable
{
    private readonly AuditExportBuffer buffer;
    private readonly NdjsonEncodedRecord json;
    private bool transferred;
    private AuditExportFrame(AuditExportBuffer buffer, NdjsonEncodedRecord json, int itemCount, bool data, IReadOnlyDictionary<string, long> transformations)
    {
        this.buffer = buffer;
        this.json = json;
        this.ItemCount = itemCount;
        this.IsData = data;
        this.Transformations = transformations?.ToImmutableDictionary(StringComparer.Ordinal) ?? ImmutableDictionary<string, long>.Empty;
        if (itemCount < 0 || this.Transformations.Count > 64 || this.Transformations.Any(item => item.Key.Length > 128 || item.Value < 0)) throw new ArgumentException("Invalid bounded formatter facts.");
    }

    /// <summary>Original formatted bytes, valid until disposal or ownership transfer.</summary>
    /// <example><code>hash.AppendData(frame.Bytes.Span);</code></example>
    public ReadOnlyMemory<byte> Bytes => this.transferred ? throw new ObjectDisposedException(nameof(AuditExportFrame)) : this.json?.FramedBytes ?? this.buffer?.Bytes ?? ReadOnlyMemory<byte>.Empty;

    /// <summary>Number of represented output items, independent of source-record count.</summary>
    /// <example><code>var events = frame.ItemCount;</code></example>
    public int ItemCount { get; }

    /// <summary>Whether bytes belong to the profile's data digest.</summary>
    /// <example><code>if (frame.IsData) hash.AppendData(frame.Bytes.Span);</code></example>
    public bool IsData { get; }

    /// <summary>Bounded aggregate formatter transformations.</summary>
    /// <example><code>var escaped = frame.Transformations.GetValueOrDefault("FormulaNeutralized");</code></example>
    public ImmutableDictionary<string, long> Transformations { get; }

    /// <summary>Transfers an already encoded NDJSON frame to this owner without copying.</summary>
    /// <example><code>return AuditExportFrame.FromJson(encoded, data: true);</code></example>
    public static AuditExportFrame FromJson(NdjsonEncodedRecord frame, int itemCount = 1, bool data = true) => new(null, frame ?? throw new ArgumentNullException(nameof(frame)), itemCount, data, null);

    /// <summary>Transfers a bounded formatter workspace to this owner without copying.</summary>
    /// <example><code>return AuditExportFrame.FromBuffer(buffer, itemCount: 1);</code></example>
    public static AuditExportFrame FromBuffer(AuditExportBuffer buffer, int itemCount, bool data = true, IReadOnlyDictionary<string, long> transformations = null) => new(buffer ?? throw new ArgumentNullException(nameof(buffer)), null, itemCount, data, transformations);

    /// <summary>Transfers exact JSON ownership to the shared raw HTTP writer once.</summary>
    /// <example><code>yield return frame.TakeJson();</code></example>
    public NdjsonEncodedRecord TakeJson()
    {
        if (this.transferred || this.json is null) throw new InvalidOperationException("An owned JSON frame is required.");
        this.transferred = true;
        return this.json;
    }

    /// <summary>Releases owned bytes. A successfully transferred shared JSON frame belongs to its recipient.</summary>
    /// <example><code>frame.Dispose();</code></example>
    public void Dispose()
    {
        if (this.transferred) return;
        this.transferred = true;
        this.json?.Dispose();
        this.buffer?.Dispose();
    }
}
