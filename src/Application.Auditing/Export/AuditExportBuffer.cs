// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Auditing;

using System.Buffers;
using System.Text;

/// <summary>A single owned UTF-8 workspace with an exact output cap, bounded JSON reservation slack, full clearing and in-place CSV escaping.</summary>
/// <example><code>using var buffer = new AuditExportBuffer(8192); buffer.WriteUtf8("SourceStoreId,AuditId\r\n");</code></example>
public sealed class AuditExportBuffer(int maximumBytes) : IBufferWriter<byte>, IDisposable
{
    private byte[] buffer;
    private int length;
    private bool disposed;

    /// <summary>The exact committed formatted prefix.</summary>
    /// <example><code>hash.AppendData(buffer.Bytes.Span);</code></example>
    public ReadOnlyMemory<byte> Bytes => this.disposed ? throw new ObjectDisposedException(nameof(AuditExportBuffer)) : this.buffer is null ? ReadOnlyMemory<byte>.Empty : this.buffer.AsMemory(0, this.length);

    /// <summary>The remaining formatted-byte budget.</summary>
    /// <example><code>var remaining = buffer.Remaining;</code></example>
    public int Remaining => maximumBytes - this.length;

    /// <inheritdoc />
    public Memory<byte> GetMemory(int sizeHint = 0) { this.Ensure(Math.Max(sizeHint, 1)); return this.buffer.AsMemory(this.length, Math.Min(this.buffer.Length - this.length, this.Remaining)); }

    /// <inheritdoc />
    public Span<byte> GetSpan(int sizeHint = 0) => this.GetMemory(sizeHint).Span;

    /// <inheritdoc />
    public void Advance(int count)
    {
        if (count < 0 || count > this.Remaining || count > (this.buffer?.Length ?? 0) - this.length) throw new AuditExportException("AuditExportRecordTooLarge");
        this.length += count;
    }

    /// <summary>Writes known framing or text directly into the owned workspace.</summary>
    /// <example><code>buffer.WriteUtf8("\\n");</code></example>
    public void WriteUtf8(string value)
    {
        if (string.IsNullOrEmpty(value)) return;
        var size = Encoding.UTF8.GetByteCount(value);
        Encoding.UTF8.GetBytes(value.AsSpan(), this.GetSpan(size));
        this.Advance(size);
    }

    /// <summary>Writes a framing byte without another buffer.</summary>
    /// <example><code>buffer.WriteByte((byte)',');</code></example>
    public void WriteByte(byte value) { this.GetSpan(1)[0] = value; this.Advance(1); }

    /// <summary>Provides JSON reservation space; actual quote-expanded bytes are checked on Advance.</summary>
    /// <example><code>using var writer = new Utf8JsonWriter(buffer.CsvJsonCellWriter());</code></example>
    public IBufferWriter<byte> CsvJsonCellWriter() => new CsvCellWriter(this);

    /// <summary>Clears the entire rented array and returns it once.</summary>
    /// <example><code>buffer.Dispose();</code></example>
    public void Dispose()
    {
        if (this.disposed) return;
        this.disposed = true;
        if (this.buffer is not null) ArrayPool<byte>.Shared.Return(this.buffer, clearArray: true);
        this.buffer = null;
    }

    private void Ensure(int count)
    {
        if (this.disposed) throw new ObjectDisposedException(nameof(AuditExportBuffer));
        if (maximumBytes <= 0 || count > this.Remaining) throw new AuditExportException("AuditExportRecordTooLarge");
        this.EnsureReservation(count);
    }

    private void EnsureReservation(int count, int pendingBytes = 0)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
        var capacity = (int)Math.Min(int.MaxValue, 3L * maximumBytes + 4096);
        if (count < 1 || count > capacity - this.length) throw new AuditExportException("AuditExportRecordTooLarge");
        var required = this.length + count;
        if (this.buffer?.Length >= required) return;
        var replacement = ArrayPool<byte>.Shared.Rent(Math.Min(capacity, Math.Max(required, Math.Max(256, (this.buffer?.Length ?? 0) * 2))));
        if (this.buffer is not null)
        {
            this.buffer.AsSpan(0, this.length + pendingBytes).CopyTo(replacement);
            ArrayPool<byte>.Shared.Return(this.buffer, clearArray: true);
        }

        this.buffer = replacement;
    }

    private sealed class CsvCellWriter(AuditExportBuffer owner) : IBufferWriter<byte>
    {
        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            owner.EnsureReservation(Math.Max(1, sizeHint));
            return owner.buffer.AsMemory(owner.length);
        }

        public Span<byte> GetSpan(int sizeHint = 0) => this.GetMemory(sizeHint).Span;

        public void Advance(int count)
        {
            var span = owner.buffer.AsSpan(owner.length);
            var quotes = 0;
            for (var index = 0; index < count; index++) if (span[index] == (byte)'"') quotes++;
            var output = checked(count + quotes);
            if (output > owner.Remaining) throw new AuditExportException("AuditExportRecordTooLarge");
            owner.EnsureReservation(Math.Max(1, output), count);
            span = owner.buffer.AsSpan(owner.length);
            for (int source = count - 1, target = output - 1; source >= 0; source--)
            {
                var value = span[source];
                span[target--] = value;
                if (value == (byte)'"') span[target--] = value;
            }

            owner.Advance(output);
        }
    }
}
