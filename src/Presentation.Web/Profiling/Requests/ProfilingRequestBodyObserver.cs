// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using System.Buffers;
using System.IO.Pipelines;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

/// <summary>Observes ordinary request consumption through Stream and PipeReader without initiating or buffering reads.</summary>
/// <example>Enabled explicitly with requests.ObserveRequestBodyBytes().</example>
public sealed class ProfilingRequestBodyObserver
{
    private Func<long, bool, bool> observe;
    private readonly ReadStream stream;
    private readonly ReadFeature pipe;

    /// <summary>Installs observers over the original stream and pipe before either can reference its wrapper.</summary>
    /// <example><code>var observer = new ProfilingRequestBodyObserver(context, ObserveBytes);</code></example>
    public ProfilingRequestBodyObserver(HttpContext context, Func<long, bool, bool> observe)
    {
        this.observe = observe;
        // Resolve the original reader before replacing Body, preventing double observation through lazy adapters.
        var reader = context.Features.Get<IRequestBodyPipeFeature>()?.Reader;
        this.stream = new(context.Request.Body, this.Count);
        this.pipe = reader is null ? null : new ReadFeature(new ReadPipe(reader, this.Count));
        context.Request.Body = this.stream;
        if (this.pipe is not null) { context.Features.Set<IRequestBodyPipeFeature>(this.pipe); }
    }

    /// <summary>Checks that ordinary body features have not been replaced by unsupported application adapters.</summary>
    /// <example><code>var covered = observer.IsInstalled(context);</code></example>
    public bool IsInstalled(HttpContext context) => ReferenceEquals(context.Request.Body, this.stream)
        && (this.pipe is null || ReferenceEquals(context.Features.Get<IRequestBodyPipeFeature>(), this.pipe));
    /// <summary>Releases diagnostic callbacks without completing a pipe or disposing the request transport.</summary>
    /// <example><code>observer.Detach();</code></example>
    public void Detach() { this.observe = null; this.stream.Detach(); (this.pipe?.Reader as ReadPipe)?.Detach(); }
    private void Count(long bytes, bool partial)
    {
        try { if (this.observe?.Invoke(bytes, partial) == false) { this.Detach(); } }
        catch (Exception) { this.Detach(); }
    }

    private sealed class ReadFeature(PipeReader reader) : IRequestBodyPipeFeature
    {
        /// <inheritdoc />
        public PipeReader Reader { get; } = reader;
    }

    private sealed class ReadPipe(PipeReader inner, Action<long, bool> count) : PipeReader
    {
        private Action<long, bool> observe = count;
        private ReadOnlySequence<byte> buffer;
        /// <summary>Releases the diagnostic callback while forwarding transport operations unchanged.</summary>
        public void Detach() { this.observe = null; this.buffer = default; }
        /// <inheritdoc />
        public override void AdvanceTo(SequencePosition consumed) => this.AdvanceTo(consumed, consumed);
        /// <inheritdoc />
        public override void AdvanceTo(SequencePosition consumed, SequencePosition examined)
        {
            if (this.observe is null)
            {
                inner.AdvanceTo(consumed, examined);
                return;
            }

            var bytes = this.buffer.Slice(0, consumed).Length;
            inner.AdvanceTo(consumed, examined);
            this.buffer = default;
            this.observe?.Invoke(bytes, false);
        }

        /// <inheritdoc />
        public override void CancelPendingRead() => inner.CancelPendingRead();
        /// <inheritdoc />
        public override void Complete(Exception exception = null) { inner.Complete(exception); this.buffer = default; }
        /// <inheritdoc />
        public override async ValueTask CompleteAsync(Exception exception = null)
        {
            await inner.CompleteAsync(exception).ConfigureAwait(false);
            this.buffer = default;
        }
        /// <inheritdoc />
        public override bool TryRead(out ReadResult result)
        {
            var available = inner.TryRead(out result);
            if (available && this.observe is not null) { this.buffer = result.Buffer; }

            return available;
        }

        /// <inheritdoc />
        public override async ValueTask<ReadResult> ReadAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await inner.ReadAsync(cancellationToken).ConfigureAwait(false);
                if (this.observe is not null) { this.buffer = result.Buffer; }

                return result;
            }
            catch { this.observe?.Invoke(0, true); throw; }
        }
    }

    private sealed class ReadStream(Stream inner, Action<long, bool> count) : Stream
    {
        private Action<long, bool> observe = count;
        /// <summary>Releases the diagnostic callback while forwarding transport operations unchanged.</summary>
        public void Detach() => this.observe = null;
        /// <inheritdoc />
        public override bool CanRead => inner.CanRead;
        /// <inheritdoc />
        public override bool CanSeek => inner.CanSeek;
        /// <inheritdoc />
        public override bool CanWrite => inner.CanWrite;
        /// <inheritdoc />
        public override long Length => inner.Length;
        /// <inheritdoc />
        public override bool CanTimeout => inner.CanTimeout;
        /// <inheritdoc />
        public override int ReadTimeout { get => inner.ReadTimeout; set => inner.ReadTimeout = value; }
        /// <inheritdoc />
        public override int WriteTimeout { get => inner.WriteTimeout; set => inner.WriteTimeout = value; }
        /// <inheritdoc />
        public override long Position { get => inner.Position; set { inner.Position = value; this.observe?.Invoke(0, true); } }
        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count) => this.Read(buffer.AsSpan(offset, count));
        /// <inheritdoc />
        public override int Read(Span<byte> buffer)
        {
            try { var length = inner.Read(buffer); this.observe?.Invoke(length, false); return length; }
            catch { this.observe?.Invoke(0, true); throw; }
        }

        /// <inheritdoc />
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => this.ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        /// <inheritdoc />
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            try { var length = await inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false); this.observe?.Invoke(length, false); return length; }
            catch { this.observe?.Invoke(0, true); throw; }
        }

        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin) { var position = inner.Seek(offset, origin); this.observe?.Invoke(0, true); return position; }
        /// <inheritdoc />
        public override void SetLength(long value) => inner.SetLength(value);
        /// <inheritdoc />
        public override void Flush() => inner.Flush();
        /// <inheritdoc />
        public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);
        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);
        /// <inheritdoc />
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => inner.WriteAsync(buffer, offset, count, cancellationToken);
        protected override void Dispose(bool disposing) { if (disposing) { inner.Dispose(); } base.Dispose(disposing); }
        /// <inheritdoc />
        public override ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}
