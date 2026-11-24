// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

using System.IO.Compression;
using System.Security.Cryptography;

/// <summary>Builds an owned logical read handle over an acquired provider snapshot with bounded envelope, transform, and transfer state.</summary>
/// <example><code>var opened = await CachePayloadReader.OpenAsync(acquired, options, binding, resources, tasks, time, transfer, cancellationToken);</code></example>
public static class CachePayloadReader
{
    // Called inside one provider admission for header acquisition. Subsequent consumer reads
    // acquire their own short native admissions and retain only their occupied transfer slot.
    /// <summary>Adopts provider snapshot and transfer ownership, validates read capabilities, and removes registered persisted transformations.</summary>
    /// <param name="acquired">The acquired provider snapshot; this method disposes it on failure or transfers it to the returned handle.</param>
    /// <param name="options">The explicit full, seekable, or range read requirements.</param>
    /// <param name="binding">The immutable collection policy and native capabilities.</param>
    /// <param name="resources">The finite process and collection resource hierarchy.</param>
    /// <param name="tasks">The tracker for already admitted transfer tails and cleanup.</param>
    /// <param name="time">The transfer deadline clock.</param>
    /// <param name="transfer">The already admitted transfer lease adopted by the returned handle or disposed on failure.</param>
    /// <param name="cancellationToken">Cancellation forwarded to initial envelope and range acquisition.</param>
    /// <param name="transforms">The optional code-approved reader registry for version-two transformation stacks.</param>
    /// <param name="validated">An optional callback invoked after full-envelope validation, never for partial ranges.</param>
    /// <param name="infrastructureObserver">Optional enabled provider-circuit observation for actual post-acquisition native reads.</param>
    /// <param name="observations">Optional predeclared collection observations for late native failures.</param>
    /// <returns>The owned logical handle, or a validation, capacity, capability, compatibility, or envelope failure.</returns>
    public static async ValueTask<Result<ICacheReadHandle>> OpenAsync(ICacheProviderReadHandle acquired,
        CacheReadOptions options, CacheCollectionBinding binding, CacheRuntimeResources resources, CacheRuntimeTasks tasks,
        TimeProvider time, CacheAdmissionLease transfer, CancellationToken cancellationToken, CachePayloadTransformRegistry transforms = null,
        Action validated = null, ICacheProviderInfrastructureObserver infrastructureObserver = null, CacheObservationSlot observations = null)
    {
        CacheEnvelopeReader framed = null;
        Stream logical = null;
        ResourceCharge memory = null;
        ResourceCharge transformMemory = null;
        List<Stream> decoders = null;
        var handedOff = false;
        try
        {
            var metadata = acquired.Metadata;
            var limits = binding.Options.Limits;
            options ??= new();
            var range = options.Offset.HasValue || options.Length.HasValue;
            var compression = metadata.Compression ?? "none";
            if (metadata.LogicalSizeBytes < 0 || metadata.LogicalSizeBytes > limits.MaxLogicalBytes ||
                metadata.EncodedSizeBytes < 0 || metadata.EncodedSizeBytes > limits.MaxEncodedBytes ||
                metadata.StoredSizeBytes < 0 || metadata.StoredSizeBytes > CacheSizePolicy.MaximumStoredBytes(binding.Options))
            {
                return Failure(CacheErrorCode.Capacity, "The acquired representation exceeds this collection's finite read policy.");
            }

            if ((range || options.RequireSeek) && (compression != "none" ||
                (range ? acquired is not ICacheProviderRangeReadHandle : !binding.Provider.Capabilities.SeekableRead || !acquired.Stream.CanSeek)))
            {
                return Failure(CacheErrorCode.Unsupported, "The acquired representation does not support ranges or seeking.");
            }

            var offset = options.Offset ?? 0;
            var length = options.Length ?? metadata.LogicalSizeBytes - offset;
            if (offset < 0 || offset > metadata.LogicalSizeBytes || length < 0 || (range && length == 0) || length > metadata.LogicalSizeBytes - offset ||
                (range && options.RequireSeek))
            {
                return Failure(CacheErrorCode.Validation, "The requested range is outside the acquired logical representation.");
            }

            var transformCharge = compression switch { "gzip" => CacheDefaults.MiB, "brotli" => CacheCompressionWorkspace.BrotliDecode, _ => 0 };
            var bufferSize = (int)Math.Max(1, Math.Min(binding.Options.Streaming.BufferSize, range ? length : metadata.LogicalSizeBytes));
            if (!ResourceCharge.TryReserve(bufferSize + transformCharge, out memory,
                resources.Transient, resources.ForCollection(binding.Name).Resident))
            {
                return Failure(CacheErrorCode.Capacity, "Transfer buffer admission was rejected.");
            }

            var source = new CacheProviderIoStream(acquired.Stream, acquired is CacheHotProviderReadHandle || StringComparer.OrdinalIgnoreCase.Equals(binding.Provider.Kind.Alias, "InMemory") ?
                null : resources.ForCollection(binding.Name).ProviderIO,
                binding.Provider.Options.AttemptTimeout, admit: false, infrastructureObserver: acquired is CacheHotProviderReadHandle ? null : infrastructureObserver, observations: observations);
            var opened = await CacheEnvelopeReader.OpenAsync(source, metadata, resources.Transient,
                resources.ForCollection(binding.Name).Resident, cancellationToken).ConfigureAwait(false);
            if (opened.IsFailure)
            {
                return Result<ICacheReadHandle>.Failure(opened);
            }

            framed = opened.Value;
            if (framed.Envelope.EnvelopeVersion == 2)
            {
                var resolved = new List<(CachePayloadTransformDescriptor Descriptor, ICachePayloadTransform Transform, byte[] Parameters)>();
                long state = 0;
                foreach (var descriptor in framed.Envelope.Transforms)
                {
                    if (transforms is null || !transforms.TryResolve(descriptor, out var transform, out var parameters))
                    {
                        return Failure(CacheErrorCode.Compatibility, "The persisted transformation reader is not registered.");
                    }

                    var charge = CachePayloadTransformRegistry.StateBytes(descriptor, transform);
                    if (charge > long.MaxValue - state) { return Failure(CacheErrorCode.Capacity, "The transformation state exceeds finite admission."); }

                    state += charge;
                    resolved.Add((descriptor, transform, parameters));
                }

                if (!ResourceCharge.TryReserve(state, out transformMemory, resources.Transient, resources.ForCollection(binding.Name).Resident))
                {
                    return Failure(CacheErrorCode.Capacity, "The persisted transformation state admission was rejected.");
                }

                decoders = [];
                logical = framed;
                for (var index = resolved.Count - 1; index >= 0; index--)
                {
                    var entry = resolved[index];
                    var decoder = CachePayloadTransformRegistry.Decode(entry.Descriptor, entry.Transform,
                        new ForwardOnlyReadStream(logical, true), entry.Parameters);
                    if (decoder is null || !decoder.CanRead) { throw new InvalidDataException("The registered transform returned no readable stream."); }

                    decoders.Add(decoder);
                    logical = decoder;
                }

                logical = new ForwardOnlyReadStream(logical, true);
            }
            else if (range)
            {
                var positioned = await framed.PositionRangeAsync(offset, ((ICacheProviderRangeReadHandle)acquired).PositionForRangeAsync,
                    cancellationToken).ConfigureAwait(false);
                if (positioned.IsFailure)
                {
                    return Result<ICacheReadHandle>.Failure(positioned);
                }

                logical = new RangeReadStream(framed, length);
            }
            else
            {
                logical = compression switch
                {
                    "gzip" => new GZipStream(framed, CompressionMode.Decompress, true),
                    "brotli" => new BrotliStream(framed, CompressionMode.Decompress, true),
                    _ => framed
                };
                if (!options.RequireSeek)
                {
                    logical = new ForwardOnlyReadStream(logical);
                }
            }

            source.EnableAdmission();
            var ownedFramed = framed;
            var stream = new CacheReadStream(logical, metadata, GC.AllocateUninitializedArray<byte>(bufferSize),
                memory, validated is null ? (full, token) => ownedFramed.VerifyAsync(!full, token) :
                    (full, token) => VerifyForHotAsync(ownedFramed, full, validated, token), async () =>
                {
                    try
                    {
                        await DisposeDecodersAsync(decoders).ConfigureAwait(false);
                    }
                    finally
                    {
                        try
                        {
                            ownedFramed.Dispose();
                            await acquired.DisposeAsync().ConfigureAwait(false);
                        }
                        finally
                        {
                            transformMemory?.Dispose();
                            transfer.Dispose();
                        }
                    }
                }, tasks, time, binding.Options.Streaming, range, range ? length : null);
            handedOff = true;
            return Result<ICacheReadHandle>.Success(new CacheReadHandle(stream, metadata, !range && compression == "none" && source.CanSeek ?
                (progress, token) => ValidateWholeAsync(source, metadata, binding, resources, progress, token) : null,
                range ? new CacheReadRange(offset, length) : null));
        }
        finally
        {
            if (!handedOff)
            {
                try
                {
                    if (logical is not null)
                    {
                        await logical.DisposeAsync().ConfigureAwait(false);
                    }

                    await DisposeDecodersAsync(decoders).ConfigureAwait(false);
                }
                finally
                {
                    try
                    {
                        framed?.Dispose();
                        await acquired.DisposeAsync().ConfigureAwait(false);
                    }
                    finally
                    {
                        memory?.Dispose();
                        transformMemory?.Dispose();
                        transfer.Dispose();
                    }
                }
            }
        }
    }

    private static async ValueTask<Result> VerifyForHotAsync(CacheEnvelopeReader reader, bool full, Action validated, CancellationToken cancellationToken)
    {
        var verified = await reader.VerifyAsync(!full, cancellationToken).ConfigureAwait(false);
        if (full && verified.IsSuccess) { validated(); }

        return verified;
    }

    private static async ValueTask DisposeDecodersAsync(List<Stream> decoders)
    {
        if (decoders is null) { return; }

        Exception failure = null;
        for (var index = decoders.Count - 1; index >= 0; index--)
        {
            try { await decoders[index].DisposeAsync().ConfigureAwait(false); }
            catch (Exception exception) { failure ??= exception; }
        }

        if (failure is not null) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw(); }
    }

    private static async ValueTask<Result> ValidateWholeAsync(Stream source, CacheEntryMetadata metadata, CacheCollectionBinding binding,
        CacheRuntimeResources resources, Action progress, CancellationToken cancellationToken)
    {
        var capacity = (int)Math.Max(1, Math.Min(binding.Options.Streaming.BufferSize, metadata.LogicalSizeBytes));
        if (!ResourceCharge.TryReserve(capacity, out var memory, resources.Transient, resources.ForCollection(binding.Name).Resident))
        {
            return Result.Failure(new CacheError(CacheErrorCode.Capacity, "The seek-compatible verification buffer was rejected."));
        }

        using (memory)
        {
            source.Seek(0, SeekOrigin.Begin);
            var opened = await CacheEnvelopeReader.OpenAsync(source, metadata, resources.Transient,
                resources.ForCollection(binding.Name).Resident, cancellationToken).ConfigureAwait(false);
            if (opened.IsFailure)
            {
                return Result.Failure().WithErrors(opened.Errors);
            }

            using var reader = opened.Value;
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = GC.AllocateUninitializedArray<byte>(capacity);
            long count = 0;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var read = await reader.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                if (read == 0)
                {
                    break;
                }

                if (read > metadata.LogicalSizeBytes - count)
                {
                    return Result.Failure(new CacheError(CacheErrorCode.Corruption, "The seek-compatible representation exceeds its logical size."));
                }

                count += read;
                hash.AppendData(buffer.AsSpan(0, read));
                progress();
            }

            return count == metadata.LogicalSizeBytes && Convert.ToHexStringLower(hash.GetHashAndReset()) == metadata.PayloadHash ? Result.Success() :
                Result.Failure(new CacheError(CacheErrorCode.Corruption, "The seek-compatible representation failed complete integrity validation."));
        }
    }

    private static Result<ICacheReadHandle> Failure(CacheErrorCode code, string message) =>
        Result<ICacheReadHandle>.Failure(new CacheError(code, message));
}