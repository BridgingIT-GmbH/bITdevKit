// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Benchmarks.Caching;

using BenchmarkDotNet.Attributes;
using BridgingIT.DevKit.Application.Caching;
using System.Security.Cryptography;
using System.Diagnostics;

/// <summary>Measures protected raw snapshot transfer through final integrity completion instead of timing stream acquisition.</summary>
/// <example><code>dotnet run -c Release --project benchmarks/Application.Benchmarks -- --filter *CacheStreamingBenchmarks*</code></example>
[MemoryDiagnoser]
public class CacheStreamingBenchmarks
{
    private CacheBenchmarkEnvironment environment;
    private byte[] expectedDigest;

    /// <summary>Gets or sets a real provider profile.</summary>
    [ParamsSource(nameof(ProviderProfiles))]
    public string Provider { get; set; }

    /// <summary>Gets explicit provider profiles visible in every BenchmarkDotNet result.</summary>
    public IReadOnlyList<string> ProviderProfiles => CacheBenchmarkEnvironment.SelectProviders(["InMemory", "Sqlite", "FileSystem"]);

    /// <summary>Gets or sets the exact uncompressed raw transfer bytes.</summary>
    [ParamsSource(nameof(PayloadProfiles))]
    public int PayloadBytes { get; set; }

    /// <summary>Gets or sets the raw/compressed and compressible/pseudorandom source profile visible in the report.</summary>
    [ParamsSource(nameof(RepresentationProfiles))]
    public string Representation { get; set; }

    /// <summary>Gets raw zeros by default, or BDK_CACHE_BENCH_STREAM_REPRESENTATIONS selecting PlainZeros, PlainRandom, GZipZeros or GZipRandom.</summary>
    public IReadOnlyList<string> RepresentationProfiles
    {
        get
        {
            var selected = (Environment.GetEnvironmentVariable("BDK_CACHE_BENCH_STREAM_REPRESENTATIONS") ?? "PlainZeros")
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (selected.Length == 0 || selected.Any(value => value is not ("PlainZeros" or "PlainRandom" or "GZipZeros" or "GZipRandom")))
            {
                throw new ArgumentException("Unknown binary benchmark representation.");
            }

            return selected.Distinct(StringComparer.Ordinal).ToArray();
        }
    }

    /// <summary>Gets 64-KiB/1-MiB defaults, or one explicit BDK_CACHE_BENCH_STREAM_BYTES profile up to one GiB.</summary>
    public IReadOnlyList<int> PayloadProfiles
    {
        get
        {
            var selected = Environment.GetEnvironmentVariable("BDK_CACHE_BENCH_STREAM_BYTES");
            if (selected is null) { return [65536, 1048576]; }

            var size = int.Parse(selected, System.Globalization.CultureInfo.InvariantCulture);
            if (size is < 1 or > 1073741824) { throw new ArgumentOutOfRangeException(nameof(selected)); }

            return [size];
        }
    }

    /// <summary>Creates a confirmed binary snapshot outside measurement.</summary>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        this.environment = await CacheBenchmarkEnvironment.CreateAsync(this.Provider, 1, 256, maximumBinaryBytes: this.PayloadBytes,
            compression: this.Representation.StartsWith("GZip", StringComparison.Ordinal));
        using var source = new PatternInput(this.PayloadBytes, this.Representation.EndsWith("Zeros", StringComparison.Ordinal));
        var stored = await this.environment.Binary.SetAsync(new("transfer"), source);
        CacheBenchmarkEnvironment.Ensure(stored);
        if (stored.Value.StorageOutcome != CacheStorageOutcome.Stored || stored.Value.Metadata?.LogicalSizeBytes != this.PayloadBytes)
        {
            throw new InvalidDataException("The complete source stream was not confirmed stored.");
        }

        this.expectedDigest = source.Digest();
        this.environment.MarkMeasurementsReady();
    }

    /// <summary>Transfers all bytes, awaits validated completion and disposes the actual reader pin.</summary>
    [Benchmark]
    public async Task<long> CopyValidatedAsync()
    {
        var started = Stopwatch.GetTimestamp();
        var result = await this.environment.Binary.OpenReadAsync("transfer");
        this.environment.Measurements.RecordDuration("StreamAcquire", started);
        CacheBenchmarkEnvironment.Ensure(result);
        if (!result.Value.Found) { throw new InvalidDataException("The transfer snapshot was missing."); }

        await using (var handle = result.Value.Value)
        {
            var buffer = new byte[64 * 1024];
            using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var first = await handle.Stream.ReadAsync(buffer.AsMemory(0, 1));
            if (first != 1) { throw new InvalidDataException("The transfer did not deliver its first byte."); }

            this.environment.Measurements.RecordDuration("StreamFirstByte", started);
            digest.AppendData(buffer.AsSpan(0, first));
            long copied = first;
            this.environment.Measurements.ObserveResourceUsage();
            int read;
            while ((read = await handle.Stream.ReadAsync(buffer)) != 0)
            {
                digest.AppendData(buffer.AsSpan(0, read));
                copied += read;
                this.environment.Measurements.ObserveResourceUsage();
            }

            var completed = await handle.WaitForCompletionAsync();
            CacheBenchmarkEnvironment.Ensure(completed);
            if (completed.Value != CacheReadCompletion.Validated || copied != this.PayloadBytes ||
                !CryptographicOperations.FixedTimeEquals(digest.GetHashAndReset(), this.expectedDigest))
            {
                throw new InvalidDataException("Complete transfer length, bytes or integrity were not confirmed.");
            }
        }

        this.environment.Measurements.RecordDuration("StreamComplete", started);
        return this.PayloadBytes;
    }

    /// <summary>Closes native handles and the owned host.</summary>
    [GlobalCleanup]
    public async Task CleanupAsync() => await this.environment.DisposeAsync();

    private sealed class PatternInput(long length, bool zeros) : Stream
    {
        private long position;
        private readonly Random random = new(173);
        private readonly IncrementalHash digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        /// <summary>Returns the digest only after the source was consumed completely.</summary>
        public byte[] Digest() => this.position == length ? this.digest.GetHashAndReset() : throw new InvalidDataException("The source was not consumed completely.");

        /// <inheritdoc />
        public override bool CanRead => true;

        /// <inheritdoc />
        public override bool CanWrite => false;

        /// <inheritdoc />
        public override bool CanSeek => false;

        /// <inheritdoc />
        public override long Length => length;

        /// <inheritdoc />
        public override long Position { get => this.position; set => throw new NotSupportedException(); }

        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count)
        {
            var taken = (int)Math.Min(count, length - this.position);
            this.Fill(buffer.AsSpan(offset, taken));
            this.position += taken;
            return taken;
        }

        /// <inheritdoc />
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var taken = (int)Math.Min(buffer.Length, length - this.position);
            this.Fill(buffer.Span[..taken]);
            this.position += taken;
            return ValueTask.FromResult(taken);
        }

        /// <inheritdoc />
        public override void Flush() => throw new NotSupportedException();

        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        /// <inheritdoc />
        public override void SetLength(long value) => throw new NotSupportedException();

        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        private void Fill(Span<byte> bytes)
        {
            if (zeros) { bytes.Clear(); }
            else { this.random.NextBytes(bytes); }

            this.digest.AppendData(bytes);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { this.digest.Dispose(); }

            base.Dispose(disposing);
        }
    }
}