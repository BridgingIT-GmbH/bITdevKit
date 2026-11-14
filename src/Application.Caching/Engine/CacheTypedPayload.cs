// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Resolves trusted typed codecs and implements complete decoding, encoded validation and conditional staging over a collection runtime.</summary>
/// <typeparam name="T">The declared value or operation result type.</typeparam>
/// <example><code>var value = await payload.GetAsync(&quot;sku:42&quot;, token);</code></example>
public sealed class CacheTypedPayload<T>
{
    private static readonly CacheReadOptions ForwardRead = new();
    private static readonly CacheReadOptions SeekRead = new(RequireSeek: true);
    private readonly CacheCollectionRuntime runtime;
    private readonly CacheResolvedCodecs<T> codecs;
    private readonly CacheValueContract<T> contract;
    private readonly Func<CacheEntryMetadata, bool> compatible;
    private readonly Func<CacheEntryMetadata, Result<CacheReadOptions>> decodeAccess;

    /// <summary>Resolves selected trusted codec profiles and their access requirements for one declared typed collection.</summary>
    public CacheTypedPayload(CacheCollectionRuntime runtime, CacheValueContract<T> contract, IServiceProvider services)
    {
        this.runtime = runtime;
        this.contract = contract;
        this.codecs = contract.Resolve(runtime.Binding.Options.Codec, services);
        this.compatible = this.Compatible;
        this.decodeAccess = this.ResolveDecodeAccess;
        var writer = this.codecs.Writer;
        if (writer is null || string.IsNullOrWhiteSpace(writer.FormatId) || writer.FormatVersion < 1 || writer.Capabilities is null)
        {
            throw new ArgumentException("A registered value codec returned an invalid format/capability declaration.");
        }

        _ = CacheIdentityNormalizer.Name(writer.FormatId);
        if (writer.Capabilities.MaximumScratchBytes is < 0)
        {
            throw new ArgumentException("A registered codec scratch bound cannot be negative.");
        }

        var validator = this.codecs.Validator;
        if (validator is not null && (validator.Format.Id != writer.FormatId || validator.Format.Version != writer.FormatVersion ||
            validator.Capabilities is null || validator.Capabilities.MaterializesValue || !validator.Capabilities.CanReadForwardOnly ||
            validator.Capabilities.RequiresSeekableInput || validator.Capabilities.MaximumScratchBytes is null or < 0))
        {
            throw new ArgumentException("An encoded-write validator must declare the exact writer format and finite forward-only nonmaterializing validation.");
        }
    }

    /// <summary>Gets whether this selected codec has an explicitly registered nonmaterializing encoded-write validator.</summary>
    public bool SupportsEncodedWrites => this.codecs.Validator is not null;

    /// <summary>Gets the selected writer&apos;s stable persisted format identifier.</summary>
    public string FormatId => this.codecs.Writer.FormatId;

    /// <summary>Gets the selected writer&apos;s persisted byte-format version.</summary>
    public int FormatVersion => this.codecs.Writer.FormatVersion;

    /// <summary>Tests whether metadata is an absence marker or has a persisted format/version readable by this declared contract.</summary>
    public bool Compatible(CacheEntryMetadata metadata) => metadata.IsNotFound ||
        (metadata.EnvelopeVersion is 1 or 2 && this.codecs.Readers.TryGet<T>(new(metadata.CodecId, metadata.CodecVersion), out _));

    /// <summary>Acquires and fully validates a compatible value, using a strictly checked decoded hot representation when available.</summary>
    public async ValueTask<Result<CacheLookup<T>>> GetAsync(string key, CancellationToken cancellationToken, bool observeAccess = true)
    {
        if (this.runtime.Hot is not null && this.runtime.Hot.Options.Representation != CacheHotEntryRepresentation.Binary)
        {
            var retained = await this.runtime.TryGetDecodedAsync<T>(key, this.compatible, cancellationToken, observeAccess).ConfigureAwait(false);
            if (retained.HasValue) { return retained.Value; }
        }

        var opened = await this.runtime.OpenAsync(key, null, cancellationToken, observeAccess: observeAccess, compatible: this.compatible,
            resolveAccess: this.decodeAccess).ConfigureAwait(false);
        if (opened.IsFailure)
        {
            return Result<CacheLookup<T>>.Failure(opened);
        }

        if (!opened.Value.Found)
        {
            return Result<CacheLookup<T>>.Success(new(false, default, opened.Value.LookupOutcome, opened.Value.StorageOutcome,
                isNotFound: opened.Value.IsNotFound, version: opened.Value.Version));
        }

        // The actual decoder owns this handle and its graph/scratch charge until it really ends.
        Task<Result<CacheLookup<T>>> actual;
        var hot = this.runtime.Hot;
        if (observeAccess && hot is not null && hot.Options.Representation != CacheHotEntryRepresentation.Binary &&
            hot.Admission.ShouldCapture(opened.Value.Value.Metadata) && hot.Promotions.TryRun(opened.Value.Value.Metadata.Identity,
                opened.Value.Value.Metadata.Version, this.CreateDecodeFactory(opened.Value.Value), out var promoted, out var owns))
        {
            if (!owns) { await opened.Value.Value.DisposeAsync().ConfigureAwait(false); }

            actual = promoted;
        }
        else
        {
            actual = this.DecodeAsync(opened.Value.Value, cancellationToken, observeAccess);
        }

        return await this.runtime.WaitOwnedAsync(actual, cancellationToken, this.runtime.Binding.Options.Streaming.Lifetime,
            CacheErrorCode.CoordinationTimeout).ConfigureAwait(false);
    }

    private Func<Task<Result<CacheLookup<T>>>> CreateDecodeFactory(ICacheReadHandle handle) => () => this.DecodeAsync(handle, this.runtime.Tasks.Stopping);

    /// <summary>Acquires a compatible protected encoded snapshot without materializing the typed graph.</summary>
    public ValueTask<Result<CacheLookup<ICacheReadHandle>>> OpenEncodedAsync(string key, CacheReadOptions options,
        CancellationToken cancellationToken, bool observeAccess = true)
    {
        if (options is null || !(options.RequireSeek || options.Offset.HasValue || options.Length.HasValue))
        {
            return this.runtime.OpenAsync(key, options, cancellationToken, observeAccess: observeAccess, compatible: this.compatible);
        }

        return this.runtime.OpenAsync(key, options, cancellationToken, observeAccess: observeAccess, compatible: this.compatible, resolveAccess: metadata =>
        {
            this.codecs.Readers.TryGet<T>(new(metadata.CodecId, metadata.CodecVersion), out var codec);
            return (options.RequireSeek && !codec.Capabilities.SupportsRawByteSeeking) ||
                ((options.Offset.HasValue || options.Length.HasValue) && !codec.Capabilities.SupportsRawByteRanges) ?
                Result<CacheReadOptions>.Failure(new CacheError(CacheErrorCode.Unsupported, "The registered codec has not approved raw byte seeking/ranges.")) :
                Result<CacheReadOptions>.Success(options);
        });
    }

    private Result<CacheReadOptions> ResolveDecodeAccess(CacheEntryMetadata metadata)
    {
        this.codecs.Readers.TryGet<T>(new(metadata.CodecId, metadata.CodecVersion), out var codec);
        return Result<CacheReadOptions>.Success(codec.Capabilities.RequiresSeekableInput ? SeekRead : ForwardRead);
    }

    /// <summary>Encodes under scratch admission and publishes only after size, integrity, authority and conditional checks succeed.</summary>
    public ValueTask<Result<CacheWriteOutcome>> SetAsync(CacheItem item, T value, CacheEntryDescriptor descriptor,
        CacheWriteCondition condition, CancellationToken cancellationToken, CachePublicationFence? fence = null,
        CacheOwnerToken? owner = null, bool isNotFound = false, DateTimeOffset? deadline = null)
    {
        if (!this.codecs.Writer.Capabilities.CanWriteForwardOnly)
        {
            return ValueTask.FromResult(Result<CacheWriteOutcome>.Failure(new CacheError(CacheErrorCode.Unsupported,
                "The selected codec cannot encode to this forward-only staging destination.")));
        }

        if (!isNotFound && descriptor?.DecodedSizeBytes is null && this.contract.EstimateDecodedBytes is not null)
        {
            descriptor = (descriptor ?? new()) with { DecodedSizeBytes = this.contract.EstimateDecodedBytes(value) };
        }

        return this.runtime.WriteAsync(item, descriptor, condition, this.FormatId, this.FormatVersion, async (stream, token) =>
        {
            var scratchSize = this.codecs.Writer.Capabilities.MaximumScratchBytes ?? this.runtime.Binding.Options.Limits.MaxDecodedBytes;
            if (!ResourceCharge.TryReserve(scratchSize, out var scratch, this.runtime.Resources.Transient,
                this.runtime.CollectionResources.Resident))
            {
                return Result.Failure(new CacheError(CacheErrorCode.Capacity, "Codec scratch admission was rejected."));
            }

            using (scratch)
            {
                return await this.codecs.Writer.EncodeAsync(value, stream, token).ConfigureAwait(false);
            }
        }, null, cancellationToken, fence, owner, isNotFound, deadline);
    }

    /// <summary>Validates a compatible caller-supplied encoded stream without full graph materialization before conditional publication.</summary>
    public ValueTask<Result<CacheWriteOutcome>> SetEncodedAsync(CacheItem item, Stream input, CacheEntryDescriptor descriptor,
        CacheWriteCondition condition, CancellationToken cancellationToken)
    {
        if (this.codecs.Validator is null)
        {
            return ValueTask.FromResult(Result<CacheWriteOutcome>.Failure(new CacheError(CacheErrorCode.Unsupported,
                "The selected codec has no registered nonmaterializing encoded-write validator.")));
        }

        return this.runtime.WriteAsync(item, descriptor, condition, this.FormatId, this.FormatVersion,
            (stream, token) => this.runtime.CopyInputAsync(input, stream, token), async (stream, token) =>
            {
                if (!ResourceCharge.TryReserve(this.codecs.Validator.Capabilities.MaximumScratchBytes.Value, out var scratch,
                    this.runtime.Resources.Transient, this.runtime.CollectionResources.Resident))
                {
                    return Result.Failure(new CacheError(CacheErrorCode.Capacity, "Validator scratch admission was rejected."));
                }

                using (scratch)
                {
                    return await this.codecs.Validator.ValidateAsync(stream, token).ConfigureAwait(false);
                }
            }, cancellationToken);
    }

    private async Task<Result<CacheLookup<T>>> DecodeAsync(ICacheReadHandle handle, CancellationToken cancellationToken, bool observeAccess = true)
    {
        await using (handle)
        {
            this.codecs.Readers.TryGet<T>(new(handle.Metadata.CodecId, handle.Metadata.CodecVersion), out var codec);
            if (codec.Capabilities.RequiresSeekableInput && !handle.Stream.CanSeek)
            {
                return Result<CacheLookup<T>>.Failure(new CacheError(CacheErrorCode.Unsupported, "The registered reader requires a seekable acquired representation."));
            }

            var admitted = await this.runtime.Resources.DecodePromotions.AcquireAsync(this.runtime.Binding.Options.Loading.WaitTimeout,
                cancellationToken).ConfigureAwait(false);
            if (admitted.IsFailure)
            {
                return Result<CacheLookup<T>>.Failure(admitted);
            }

            using var decodeAdmission = admitted.Value;
            var limits = this.runtime.Binding.Options.Limits;
            var graph = codec.Capabilities.MaterializesValue ? limits.MaxDecodedBytes : 0;
            var scratchSize = codec.Capabilities.MaximumScratchBytes ?? this.runtime.Binding.Options.Streaming.BufferSize;
            if (!ResourceCharge.TryReserve(checked(graph + scratchSize), out var memory, this.runtime.Resources.Transient,
                this.runtime.CollectionResources.Resident))
            {
                return Result<CacheLookup<T>>.Failure(new CacheError(CacheErrorCode.Capacity, "Typed graph/codec scratch admission was rejected."));
            }

            using (memory)
            {
                try
                {
                    var decoded = await codec.DecodeAsync(handle.Stream, cancellationToken).ConfigureAwait(false);
                    if (decoded.IsFailure)
                    {
                        this.runtime.Observations?.Record(CacheObservationKind.DecodeFailure);
                        return Result<CacheLookup<T>>.Failure(new CacheError(CacheErrorCode.Corruption, "The registered codec rejected the acquired payload."));
                    }

                    var probe = new byte[1];
                    if (await handle.Stream.ReadAsync(probe, cancellationToken).ConfigureAwait(false) != 0)
                    {
                        this.runtime.Observations?.Record(CacheObservationKind.DecodeFailure);
                        return Result<CacheLookup<T>>.Failure(new CacheError(CacheErrorCode.Corruption, "The registered codec did not consume the complete payload."));
                    }

                    var completion = await handle.WaitForCompletionAsync(cancellationToken).ConfigureAwait(false);
                    if (completion.IsSuccess && completion.Value == CacheReadCompletion.RangeCompleted && handle is CacheReadHandle owned)
                    {
                        var verified = await owned.ValidateWholeAsync(cancellationToken).ConfigureAwait(false);
                        if (verified.IsFailure)
                        {
                            return Result<CacheLookup<T>>.Failure(verified);
                        }

                        if (observeAccess) { this.runtime.Hot?.Admission.Observe(handle.Metadata); }
                    }
                    else if (completion.IsFailure || completion.Value != CacheReadCompletion.Validated)
                    {
                        return Result<CacheLookup<T>>.Failure(new CacheError(CacheErrorCode.Corruption, "The typed read did not establish complete payload integrity."));
                    }

                    long? estimate;
                    try { estimate = this.contract.EstimateDecodedBytes?.Invoke(decoded.Value); }
                    catch (Exception) { estimate = null; }

                    if (estimate is < 0 || estimate > limits.MaxDecodedBytes)
                    {
                        return Result<CacheLookup<T>>.Failure(new CacheError(CacheErrorCode.Capacity, "The decoded graph exceeds its declared collection limit."));
                    }

                    if (observeAccess) { this.runtime.Hot?.TryRetainDecoded(handle.Metadata, this.contract, decoded.Value); }

                    return Result<CacheLookup<T>>.Success(new(true, decoded.Value, CacheLookupOutcome.Hit, CacheStorageOutcome.None,
                        version: handle.Metadata.Version));
                }
                catch (Exception exception) when (exception is IOException or InvalidDataException)
                {
                    this.runtime.Observations?.Record(CacheObservationKind.DecodeFailure);
                    return Result<CacheLookup<T>>.Failure(new CacheError(exception is CacheTransferException transfer ? transfer.Code :
                        exception is StreamSizeLimitExceededException ? CacheErrorCode.Capacity :
                        CacheErrorCode.Corruption, "The typed payload read did not complete."));
                }
            }
        }
    }
}