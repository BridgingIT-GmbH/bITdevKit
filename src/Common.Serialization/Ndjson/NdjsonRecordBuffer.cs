// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Buffers;
using System.Text.Json;

internal sealed class NdjsonRecordBuffer(int limit) : IBufferWriter<byte>, IDisposable
{
    private byte[] buffer;
    private int length;
    private bool disposed;
    // Utf8JsonWriter reserves up to three UTF-8 bytes per UTF-16 character,
    // even for ASCII, and asks for a 4096-byte growth window. Advance still
    // enforces the exact JSON limit before any record can be emitted.
    private readonly int maximumCapacity = (int)Math.Min(int.MaxValue, (3L * limit) + 4096);

    internal int Length => this.length;
    internal ReadOnlyMemory<byte> Memory
    {
        get
        {
            ObjectDisposedException.ThrowIf(this.disposed, this);
            return this.buffer.AsMemory(0, this.length);
        }
    }

    public void Advance(int count)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (count > limit - this.length)
        {
            throw new NdjsonStreamException("stream_record_too_large");
        }

        this.length += count;
    }

    public Memory<byte> GetMemory(int sizeHint = 0)
    {
        this.Ensure(Math.Max(1, sizeHint));
        return this.buffer.AsMemory(this.length);
    }

    public Span<byte> GetSpan(int sizeHint = 0) => this.GetMemory(sizeHint).Span;

    internal void AppendReceived(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length > (limit + 2) - this.length)
        {
            throw new NdjsonStreamException("stream_record_too_large");
        }

        // Received bytes need no Utf8JsonWriter reservation window. Small
        // frames can use smaller pool buckets while still clearing them fully.
        this.Ensure(bytes.Length, minimumCapacity: 256);
        bytes.CopyTo(this.buffer.AsSpan(this.length));
        this.length += bytes.Length;
    }

    private void Ensure(int additional, int minimumCapacity = 4096)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
        if (additional > this.maximumCapacity - this.length)
        {
            throw new NdjsonStreamException("stream_record_too_large");
        }

        var required = this.length + additional;
        if (this.buffer is not null && this.buffer.Length >= required)
        {
            return;
        }

        var next = ArrayPool<byte>.Shared.Rent(Math.Min(this.maximumCapacity, Math.Max(required, Math.Min(minimumCapacity, limit + 2))));
        if (this.buffer is not null)
        {
            this.buffer.AsSpan(0, this.length).CopyTo(next);
            // A converter may have written beyond Advance before throwing.
            ArrayPool<byte>.Shared.Return(this.buffer, clearArray: true);
        }

        this.buffer = next;
    }

    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        this.disposed = true;
        if (this.buffer is not null)
        {
            ArrayPool<byte>.Shared.Return(this.buffer, clearArray: true);
            this.buffer = null;
        }
    }
}

internal static class NdjsonRecordValidation
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    internal static void Validate(ReadOnlySpan<byte> bytes)
    {
        try
        {
            if (bytes.IsEmpty || bytes.StartsWith(new byte[] { 0xef, 0xbb, 0xbf }) || bytes.IndexOfAny((byte)'\r', (byte)'\n') >= 0)
            {
                throw new JsonException();
            }

            StrictUtf8.GetCharCount(bytes);
            var reader = new Utf8JsonReader(bytes, new JsonReaderOptions { CommentHandling = JsonCommentHandling.Disallow, AllowTrailingCommas = false });
            if (!reader.Read())
            {
                throw new JsonException();
            }

            reader.Skip();
            if (reader.Read())
            {
                throw new JsonException();
            }
        }
        catch (Exception error) when (error is JsonException or DecoderFallbackException)
        {
            throw new NdjsonStreamException("stream_invalid_record");
        }
    }
}
