// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using System.IO.Pipelines;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

/// <summary>Forwards response transport operations and observes successful outer-boundary body bytes once.</summary>
/// <example>Installed outside compression and cache middleware by RequestProfilingMiddleware.</example>
public sealed class ProfilingResponseBodyFeature : IHttpResponseBodyFeature
{
    private readonly IHttpResponseBodyFeature inner;
    private Func<long, bool, bool> observe;
    private readonly CountingStream stream;
    private CountingWriter writer;

    /// <summary>Wraps the existing body feature without buffering or assuming transport ownership.</summary>
    /// <example><code>var feature = new ProfilingResponseBodyFeature(original, ObserveBytes);</code></example>
    public ProfilingResponseBodyFeature(IHttpResponseBodyFeature inner, Func<long, bool, bool> observe)
    {
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        this.observe = observe;
        this.stream = new(inner.Stream, this.Count);
    }

    /// <inheritdoc />
    public Stream Stream => this.stream;
    /// <inheritdoc />
    public PipeWriter Writer => this.writer ??= new CountingWriter(this.inner.Writer, this.Count);
    /// <inheritdoc />
    public void DisableBuffering() => this.inner.DisableBuffering();
    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken = default) => this.inner.StartAsync(cancellationToken);
    /// <inheritdoc />
    public async Task CompleteAsync()
    {
        try
        {
            await this.inner.CompleteAsync().ConfigureAwait(false);
            this.writer?.ObserveCompletion();
        }
        catch { this.Count(0, true); throw; }
    }
    /// <inheritdoc />
    public async Task SendFileAsync(string path, long offset, long? count, CancellationToken cancellationToken = default)
    {
        var observed = count;
        try { observed ??= Math.Max(0, new FileInfo(path).Length - offset); }
        catch (Exception) { /* File metadata is optional; forwarding determines application behavior. */ }

        try
        {
            await this.inner.SendFileAsync(path, offset, count, cancellationToken).ConfigureAwait(false);
            this.Count(observed ?? 0, observed is null);
        }
        catch
        {
            this.Count(0, true);
            throw;
        }
    }

    /// <summary>Checks whether another middleware replaced the observed body feature.</summary>
    /// <example><code>var covered = observer.IsInstalled(httpContext);</code></example>
    public bool IsInstalled(HttpContext context) => ReferenceEquals(context.Features.Get<IHttpResponseBodyFeature>(), this) && this.writer?.HasPending != true;
    /// <summary>Releases diagnostic callbacks, leaving Stream, Writer and transport operations usable.</summary>
    /// <example><code>observer.Detach();</code></example>
    public void Detach() { this.observe = null; this.stream.Detach(); this.writer?.Detach(); }

    private void Count(long bytes, bool partial)
    {
        try
        {
            if (this.observe?.Invoke(bytes, partial) == false) { this.Detach(); }
        }
        catch (Exception) { this.Detach(); }
    }

    private sealed class CountingWriter(PipeWriter inner, Action<long, bool> count) : PipeWriter
    {
        private Action<long, bool> observe = count;
        private long pending;
        /// <summary>Gets whether staged bytes have no confirmed successful flush.</summary>
        public bool HasPending => this.pending != 0;
        /// <summary>Releases the diagnostic callback while forwarding transport operations unchanged.</summary>
        public void Detach() => this.observe = null;
        /// <summary>Observes pending bytes once after confirmed transport completion.</summary>
        public void ObserveCompletion()
        {
            var bytes = this.pending;
            this.pending = 0;
            this.observe?.Invoke(bytes, false);
        }
        /// <inheritdoc />
        public override bool CanGetUnflushedBytes => inner.CanGetUnflushedBytes;
        /// <inheritdoc />
        public override long UnflushedBytes => inner.UnflushedBytes;
        /// <inheritdoc />
        public override void Advance(int bytes) { inner.Advance(bytes); this.pending += bytes; }
        /// <inheritdoc />
        public override Memory<byte> GetMemory(int sizeHint = 0) => inner.GetMemory(sizeHint);
        /// <inheritdoc />
        public override Span<byte> GetSpan(int sizeHint = 0) => inner.GetSpan(sizeHint);
        /// <inheritdoc />
        public override void CancelPendingFlush() => inner.CancelPendingFlush();
        /// <inheritdoc />
        public override void Complete(Exception exception = null)
        {
            if (this.pending != 0 || exception is not null) { this.observe?.Invoke(0, true); }

            inner.Complete(exception);
        }

        /// <inheritdoc />
        public override async ValueTask<FlushResult> FlushAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await inner.FlushAsync(cancellationToken).ConfigureAwait(false);
                if (!result.IsCanceled) { var bytes = this.pending; this.pending = 0; this.observe?.Invoke(bytes, false); }
                else { this.observe?.Invoke(0, true); }

                return result;
            }
            catch { this.observe?.Invoke(0, true); throw; }
        }
    }

    private sealed class CountingStream(Stream inner, Action<long, bool> count) : Stream
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
        public override long Position { get => inner.Position; set => inner.Position = value; }
        /// <inheritdoc />
        public override bool CanTimeout => inner.CanTimeout;
        /// <inheritdoc />
        public override int ReadTimeout { get => inner.ReadTimeout; set => inner.ReadTimeout = value; }
        /// <inheritdoc />
        public override int WriteTimeout { get => inner.WriteTimeout; set => inner.WriteTimeout = value; }
        /// <inheritdoc />
        public override void Flush()
        {
            try { inner.Flush(); }
            catch { this.observe?.Invoke(0, true); throw; }
        }
        /// <inheritdoc />
        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            try { await inner.FlushAsync(cancellationToken).ConfigureAwait(false); }
            catch { this.observe?.Invoke(0, true); throw; }
        }
        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        /// <inheritdoc />
        public override int Read(Span<byte> buffer) => inner.Read(buffer);
        /// <inheritdoc />
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => inner.ReadAsync(buffer, offset, count, cancellationToken);
        /// <inheritdoc />
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => inner.ReadAsync(buffer, cancellationToken);
        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        /// <inheritdoc />
        public override void SetLength(long value) => inner.SetLength(value);
        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count) => this.Write(buffer.AsSpan(offset, count));
        /// <inheritdoc />
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            try { inner.Write(buffer); this.observe?.Invoke(buffer.Length, false); }
            catch { this.observe?.Invoke(0, true); throw; }
        }

        /// <inheritdoc />
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => this.WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        /// <inheritdoc />
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            try { await inner.WriteAsync(buffer, cancellationToken).ConfigureAwait(false); this.observe?.Invoke(buffer.Length, false); }
            catch { this.observe?.Invoke(0, true); throw; }
        }

        protected override void Dispose(bool disposing) { if (disposing) { inner.Dispose(); } base.Dispose(disposing); }
        /// <inheritdoc />
        public override ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}
