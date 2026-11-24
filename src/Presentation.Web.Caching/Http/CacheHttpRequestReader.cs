// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Caching;

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text;
using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Http;

/// <summary>Reads request bodies only after authorization/exposure guards and applies finite public DTO/stream limits.</summary>
/// <param name="configuration">The trusted endpoint body and descriptor limits.</param>
/// <param name="profiles">The code-registered public boundary model mappings.</param>
/// <param name="registry">The collection access capabilities selected at startup.</param>
/// <param name="resources">The process and collection resource admission accounts.</param>
/// <param name="time">The clock used for collection body-transfer deadlines.</param>
/// <example><code>var body = await reader.ReadInvalidationAsync(context.Request, cancellationToken);</code></example>
public sealed class CacheHttpRequestReader(CacheWebConfiguration configuration, CacheHttpWriteProfiles profiles,
    CacheCollectionAccessRegistry registry, CacheRuntimeResources resources, TimeProvider time)
{
    private static readonly JsonSerializerOptions MetadataJson = new(JsonSerializerDefaults.Web)
    {
        MaxDepth = 32, Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>Reads an optional compact JSON X-Cache-Descriptor header, bounded to sixteen KiB, for TTL and declared dependencies.</summary>
    /// <example><code>var descriptor = reader.ReadDescriptor(request);</code></example>
    public Result<CacheEntryDescriptor> ReadDescriptor(HttpRequest request)
    {
        var raw = request.Headers["X-Cache-Descriptor"].ToString();
        if (raw.Length == 0) { return Result<CacheEntryDescriptor>.Success(new()); }

        if (Encoding.UTF8.GetByteCount(raw) > Math.Min(16384, configuration.Options.MaximumMetadataBodyBytes))
        {
            return Result<CacheEntryDescriptor>.Failure(new CacheError(CacheErrorCode.Validation, "The public cache descriptor exceeds its header bound."));
        }

        try
        {
            var descriptor = JsonSerializer.Deserialize<CacheEntryDescriptor>(raw, MetadataJson) ?? new();
            return Result<CacheEntryDescriptor>.Success(descriptor with { DecodedSizeBytes = null });
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or FormatException or OverflowException)
        {
            return Result<CacheEntryDescriptor>.Failure(new CacheError(CacheErrorCode.Validation, "The public cache descriptor is malformed."));
        }
    }

    /// <summary>Validates registered write mappings against immutable collection types and resource ceilings without reading bodies.</summary>
    public void ValidateProfiles(CacheCatalog catalog)
    {
        foreach (var pair in profiles.Profiles)
        {
            var bindings = catalog.Collections.Values.Where(binding => StringComparer.OrdinalIgnoreCase.Equals(binding.Contract.Alias, pair.Key)).ToArray();
            if (bindings.Length == 0 || bindings.Any(binding => binding.Contract.IsBinary || binding.Contract.ValueType != pair.Value.ValueType ||
                pair.Value.MaximumBodyBytes < 1 || pair.Value.MaximumBodyBytes > binding.Options.Limits.MaxEncodedBytes ||
                pair.Value.MaximumDecodedBytes < 1 || pair.Value.MaximumDecodedBytes > binding.Options.Limits.MaxDecodedBytes))
            {
                throw new ArgumentException("A public cache HTTP write mapping does not match its declared contract or finite limits.");
            }
        }

        profiles.Freeze();
    }

    /// <summary>Reads one bounded structured invalidation body after all explicit collection targets passed exposure checks.</summary>
    public async ValueTask<Result<CacheHttpInvalidationRequest>> ReadInvalidationAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (request.ContentLength == 0) { return Result<CacheHttpInvalidationRequest>.Success(new()); }

        try
        {
            using var bounded = new CacheHttpBoundedReadStream(request.Body, configuration.Options.MaximumMetadataBodyBytes);
            var body = await JsonSerializer.DeserializeAsync<CacheHttpInvalidationRequest>(bounded, MetadataJson, cancellationToken).ConfigureAwait(false);
            return Result<CacheHttpInvalidationRequest>.Success(body ?? new());
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidDataException or FormatException or OverflowException)
        {
            return Result<CacheHttpInvalidationRequest>.Failure(new CacheError(CacheErrorCode.Validation, "The bounded invalidation request is malformed or too large."));
        }
    }

    /// <summary>Prepares a registered typed model or leaves a bounded stream unconsumed for the common write handler.</summary>
    /// <remarks>Typed input observes collection idle/lifetime limits. A timed-out read retains decode admission and reservations until its actual I/O completes.</remarks>
    /// <example><code>var prepared = await reader.ReadWriteAsync(context.Request, collection, context.RequestAborted); // Dispose a successful prepared value after publication.</code></example>
    public async ValueTask<Result<CacheHttpPreparedWrite>> ReadWriteAsync(HttpRequest request, CacheCollectionBinding binding, CancellationToken cancellationToken)
    {
        var collection = registry.Get(binding.Name);
        if (profiles.Profiles.TryGetValue(binding.Contract.Alias, out var profile))
        {
            if (request.ContentType?.Split(';')[0].Trim().Equals(profile.ContentType, StringComparison.OrdinalIgnoreCase) != true)
            {
                return Result<CacheHttpPreparedWrite>.Failure(new CacheError(CacheErrorCode.Validation, "The request media type does not match the registered public model."));
            }

            if (request.ContentLength > profile.MaximumBodyBytes ||
                !ResourceCharge.TryReserve(profile.MaximumDecodedBytes + profile.MaximumBodyBytes + 16 * CacheDefaults.KiB, out var memory,
                    resources.Transient, resources.ForCollection(binding.Name).Resident))
            {
                return Result<CacheHttpPreparedWrite>.Failure(new CacheError(CacheErrorCode.Capacity, "The public model decode exceeds its finite admission."));
            }

            var handedOff = false;
            CacheTransferBudget transfer = null;
            try
            {
                var admitted = await resources.DecodePromotions.AcquireAsync(binding.Options.Loading.WaitTimeout, cancellationToken).ConfigureAwait(false);
                if (admitted.IsFailure) { return Result<CacheHttpPreparedWrite>.Failure(admitted); }

                using var admission = admitted.Value;
                using (transfer = new(time, binding.Options.Streaming, static () => { }))
                using (var bounded = new CacheHttpBoundedReadStream(request.Body, profile.MaximumBodyBytes, transfer))
                using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, transfer.Token))
                {
                    transfer.Start();
                    var value = await profile.ReadAsync(bounded, linked.Token).ConfigureAwait(false);
                    if (transfer.HasExpired) { return TransferExpired(); }

                    if (value.IsFailure) { return Result<CacheHttpPreparedWrite>.Failure(value); }

                    handedOff = true;
                    return Result<CacheHttpPreparedWrite>.Success(new(null, value.Value, true, memory));
                }
            }
            catch (OperationCanceledException) when (transfer?.HasExpired == true)
            {
                return TransferExpired();
            }
            catch (InvalidDataException)
            {
                return Result<CacheHttpPreparedWrite>.Failure(new CacheError(CacheErrorCode.Capacity, "The public model exceeds its serialized body limit."));
            }
            finally
            {
                // Cancellation cannot release decode/native ownership before an actual body read ends.
                if (!handedOff) { memory.Dispose(); }
            }
        }

        if (!collection.SupportsStreamWrites)
        {
            return Result<CacheHttpPreparedWrite>.Failure(new CacheError(CacheErrorCode.Unsupported, "Typed HTTP writes require an explicit public model or an exact-format streaming validator."));
        }

        if (request.ContentLength > binding.Options.Limits.MaxEncodedBytes)
        {
            return Result<CacheHttpPreparedWrite>.Failure(new CacheError(CacheErrorCode.Capacity, "The streamed request exceeds the collection limit."));
        }

        return Result<CacheHttpPreparedWrite>.Success(new(request.Body, null, false));
    }

    private static Result<CacheHttpPreparedWrite> TransferExpired() => Result<CacheHttpPreparedWrite>.Failure(
        new CacheError(CacheErrorCode.CoordinationTimeout, "The public model body exceeded its collection transfer deadline."));
}

/// <summary>Limits serialized body consumption while preserving ownership of the underlying HTTP request stream.</summary>
/// <param name="inner">The caller-owned request stream.</param>
/// <param name="maximumBytes">The maximum serialized bytes before controlled rejection.</param>
/// <param name="transfer">An optional caller-owned deadline budget renewed only after successful reads.</param>
/// <example><code>using var bounded = new CacheHttpBoundedReadStream(request.Body, maximumBytes);</code></example>
public sealed class CacheHttpBoundedReadStream(Stream inner, long maximumBytes, CacheTransferBudget transfer = null) : Stream
{
    private readonly Stream stream = inner ?? throw new ArgumentNullException(nameof(inner));
    private readonly long limit = maximumBytes >= 0 ? maximumBytes : throw new ArgumentOutOfRangeException(nameof(maximumBytes));
    private long read;
    private bool exceeded;

    /// <inheritdoc />
    public override bool CanRead => this.stream.CanRead;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Position { get => this.read; set => throw new NotSupportedException(); }

    /// <inheritdoc />
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        this.CheckDeadline();
        var count = await this.stream.ReadAsync(buffer[..this.ReadLimit(buffer.Length)], cancellationToken).ConfigureAwait(false);
        this.CheckDeadline();
        this.Advance(count);
        if (count > 0) { transfer?.Progress(); }

        return count;
    }

    /// <inheritdoc />
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        this.ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count)
    {
        this.CheckDeadline();
        var readCount = this.stream.Read(buffer, offset, this.ReadLimit(count));
        this.CheckDeadline();
        this.Advance(readCount);
        if (readCount > 0) { transfer?.Progress(); }

        return readCount;
    }

    /// <inheritdoc />
    public override void Flush() { }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    private void CheckDeadline()
    {
        if (transfer?.HasExpired == true) { throw new OperationCanceledException(transfer.Token); }
    }

    private int ReadLimit(int requested)
    {
        if (this.exceeded) { throw new InvalidDataException("The HTTP body exceeds its finite byte limit."); }

        var remaining = this.limit - this.read;
        return remaining >= requested ? requested : (int)remaining + 1;
    }

    private void Advance(int count)
    {
        if (count > this.limit - this.read)
        {
            this.exceeded = true;
            throw new InvalidDataException("The HTTP body exceeds its finite byte limit.");
        }

        this.read += count;
    }
}