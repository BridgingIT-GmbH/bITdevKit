// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Security.Cryptography;

/// <summary>Appends bytes written to a stream directly to a caller-owned incremental hash.</summary>
/// <remarks>
/// The caller owns and finalizes the hash. Flushing or disposing the stream leaves the hash and
/// its accumulated bytes available. Writes complete inline and concurrent writes are unsupported.
/// </remarks>
/// <example>
/// <code>
/// using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
/// using (var output = new IncrementalHashWriteStream(hash))
/// {
///     output.Write("inspection report"u8);
/// }
///
/// var fingerprint = Convert.ToHexStringLower(hash.GetHashAndReset());
/// </code>
/// </example>
public sealed class IncrementalHashWriteStream : Stream
{
    private readonly IncrementalHash hash;
    private readonly CancellationToken cancellationToken;
    private bool disposed;

    /// <summary>Creates a write-only stream that appends to the supplied hash.</summary>
    /// <param name="hash">The hash to update. The caller retains ownership.</param>
    /// <param name="cancellationToken">The token checked before each write appends bytes.</param>
    /// <exception cref="ArgumentNullException"><paramref name="hash"/> is null.</exception>
    public IncrementalHashWriteStream(IncrementalHash hash, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(hash);

        this.hash = hash;
        this.cancellationToken = cancellationToken;
    }

    /// <inheritdoc />
    public override bool CanRead => false;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => !this.disposed;

    /// <inheritdoc />
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <inheritdoc />
    public override void Flush() { }

    /// <inheritdoc />
    public override Task FlushAsync(CancellationToken cancellationToken) =>
        cancellationToken.IsCancellationRequested ? Task.FromCanceled(cancellationToken) : Task.CompletedTask;

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count)
    {
        ValidateBufferArguments(buffer, offset, count);
        this.Write(buffer.AsSpan(offset, count));
    }

    /// <inheritdoc />
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        this.cancellationToken.ThrowIfCancellationRequested();
        this.Append(buffer);
    }

    /// <inheritdoc />
    public override void WriteByte(byte value) => this.Write(new ReadOnlySpan<byte>(in value));

    /// <inheritdoc />
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        ValidateBufferArguments(buffer, offset, count);
        return this.WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    /// <inheritdoc />
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromCanceled(cancellationToken);
        }

        if (this.cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromCanceled(this.cancellationToken);
        }

        this.Append(buffer.Span);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        this.disposed = true;
        base.Dispose(disposing);
    }

    private void Append(ReadOnlySpan<byte> buffer)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
        this.hash.AppendData(buffer);
    }
}
