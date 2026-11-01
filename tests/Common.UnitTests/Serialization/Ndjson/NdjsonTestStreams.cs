// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Testing.Ndjson;

internal static class NdjsonTestStreams
{
    internal sealed class FragmentedReadStream(byte[] bytes, int fragmentSize = 1, bool holdAtEof = false) : Stream
    {
        private int offset;
        internal int BytesRead => this.offset;
        internal bool Disposed { get; private set; }

        internal TaskCompletionSource EofGate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (this.offset == bytes.Length)
            {
                if (holdAtEof) { await this.EofGate.Task.WaitAsync(cancellationToken); }

                return 0;
            }

            var count = Math.Min(Math.Min(fragmentSize, buffer.Length), bytes.Length - this.offset);
            bytes.AsMemory(this.offset, count).CopyTo(buffer);
            this.offset += count;
            return count;
        }

        protected override void Dispose(bool disposing) { this.Disposed = true; base.Dispose(disposing); }

        public override void Flush() => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException("Synchronous read is forbidden.");
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    internal sealed class RecordingStream : Stream
    {
        private readonly MemoryStream output = new();
        internal int WriteCount { get; private set; }

        internal int FlushCount { get; private set; }

        internal int FailWriteAt { get; set; }

        internal int FailFlushAt { get; set; }

        internal int PartialWriteBytes { get; set; }

        internal Task WriteGate { get; set; }

        internal bool IgnoreCancellation { get; set; }

        internal bool Disposed { get; private set; }

        internal TaskCompletionSource SecondFlush { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource FirstFlush { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource WriteFinished { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource WriteStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal byte[] Bytes => this.output.ToArray();
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => this.output.Length;
        public override long Position { get => this.output.Position; set => throw new NotSupportedException(); }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            this.WriteCount++;
            this.WriteStarted.TrySetResult();
            if (this.WriteGate is not null)
            {
                if (this.IgnoreCancellation) { await this.WriteGate; }
                else { await this.WriteGate.WaitAsync(cancellationToken); }
            }

            if (this.WriteCount == this.FailWriteAt)
            {
                this.output.Write(buffer.Span[..Math.Min(buffer.Length, this.PartialWriteBytes)]);
                throw new IOException("Payload must never appear in diagnostics.");
            }

            this.output.Write(buffer.Span);
            this.WriteFinished.TrySetResult();
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            this.FlushCount++;
            if (this.FlushCount == this.FailFlushAt) { throw new IOException("Private flush details."); }

            this.FirstFlush.TrySetResult();
            if (this.FlushCount >= 2) { this.SecondFlush.TrySetResult(); }

            return Task.CompletedTask;
        }

        protected override void Dispose(bool disposing) { this.Disposed = true; base.Dispose(disposing); }

        public override void Flush() => throw new NotSupportedException("Synchronous flush is forbidden.");
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException("Synchronous write is forbidden.");
    }
}
