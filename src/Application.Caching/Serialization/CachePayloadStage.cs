// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

using System.IO.Compression;

// Owns original and optional compressed same-store stages. Source/codec input is consumed once.
/// <summary>Owns original and optional transformed stages in the selected store without replaying the source or codec.</summary>
/// <remarks>Complete the original bytes and metadata before publication. Dispose every attempt, including failed or unselected candidate stages.</remarks>
/// <example><code>await using var stage = created.Value; await WriteOriginalAsync(stage.Stream); var completed = await stage.CompleteAsync(descriptor, validator, cancellationToken);</code></example>
public sealed class CachePayloadStage : IAsyncDisposable
{
    private readonly ICacheStoreProvider provider;
    private readonly CacheProviderWriteRequest request;
    private readonly CacheCollectionBinding binding;
    private readonly CacheRuntimeResources resources;
    private readonly CacheCollectionResources collectionResources;
    private readonly TimeProvider time;
    private readonly CacheEnvelopeWriter writer;
    private readonly CacheItem item;
    private readonly CacheEntryEnvelope envelope;
    private readonly ICacheProviderWriteSession original;
    private ICacheProviderWriteSession selected;
    private bool completed;
    private bool publishable;
    private readonly CacheProviderAccess access;
    private readonly CacheRuntimeTasks tasks;
    private readonly CachePayloadPipeline pipeline;
    private CacheEncodedPayload originalPayload;
    private int compressionAttempted;
    private CacheEntryEnvelope selectedEnvelope;
    private CacheEncodedPayload selectedPayload;
    private CachePayloadTransformRegistry transforms;
    private readonly List<ICacheProviderWriteSession> retired = [];
    private int transforming;
    private bool transformationsClosed;

    private CachePayloadStage(ICacheStoreProvider provider, CacheProviderWriteRequest request, CacheCollectionBinding binding,
        CacheRuntimeResources resources, TimeProvider time, CacheItem item, CacheEntryEnvelope envelope,
        ICacheProviderWriteSession original, CacheEnvelopeWriter writer, CacheProviderAccess access, CacheRuntimeTasks tasks, CachePayloadPipeline pipeline)
    {
        this.provider = provider;
        this.request = request;
        this.binding = binding;
        this.resources = resources;
        this.collectionResources = resources.ForCollection(binding.Name);
        this.time = time;
        this.item = item;
        this.envelope = envelope;
        this.selectedEnvelope = envelope;
        this.original = this.selected = original;
        this.writer = writer;
        this.access = access;
        this.tasks = tasks;
        this.pipeline = pipeline;
        this.Stream = new(writer, Math.Min(binding.Options.Limits.MaxEncodedBytes, binding.Options.Limits.MaxLogicalBytes));
    }

    /// <summary>Gets the bounded original codec or binary destination, before cache-managed transformations.</summary>
    public LimitedWriteStream Stream { get; }

    /// <summary>Gets finalized selected-stage metadata after successful original completion, including any selected transformation generation.</summary>
    public CacheEntryMetadata Metadata { get; private set; }

    /// <summary>Begins one selected-store staging session and reserves its original envelope writer.</summary>
    /// <param name="provider">The selected complete provider.</param>
    /// <param name="request">The publication identity, fence, and conditional-write request.</param>
    /// <param name="binding">The immutable collection contract and finite policy.</param>
    /// <param name="resources">The process and collection resource hierarchy.</param>
    /// <param name="time">The publication timestamp and expiration clock.</param>
    /// <param name="item">The immutable key and initial dependency snapshot.</param>
    /// <param name="formatId">The registered original codec or raw-format identifier.</param>
    /// <param name="formatVersion">The registered persisted format version.</param>
    /// <param name="cancellationToken">Cancellation forwarded to staging and header creation.</param>
    /// <param name="access">Optional existing provider admission and initialization orchestration.</param>
    /// <param name="tasks">The tracker used when admitted provider work outlives its caller.</param>
    /// <param name="pipeline">The optional trusted payload behavior chain.</param>
    /// <param name="transforms">The optional trusted custom transform registry.</param>
    /// <returns>The owned original stage, or a provider or envelope-admission failure.</returns>
    public static async ValueTask<Result<CachePayloadStage>> CreateAsync(ICacheStoreProvider provider,
        CacheProviderWriteRequest request, CacheCollectionBinding binding, CacheRuntimeResources resources, TimeProvider time,
        CacheItem item, string formatId, int formatVersion, CancellationToken cancellationToken,
        CacheProviderAccess access = null, CacheRuntimeTasks tasks = null, CachePayloadPipeline pipeline = null, CachePayloadTransformRegistry transforms = null)
    {
        var staged = access is null ? await provider.BeginWriteAsync(request, cancellationToken).ConfigureAwait(false) :
            await CacheWriteSessionAccess.BeginAsync(access, request, binding, resources.ForCollection(binding.Name), tasks,
                cancellationToken).ConfigureAwait(false);
        if (staged.IsFailure)
        {
            return Result<CachePayloadStage>.Failure(staged);
        }

        var session = staged.Value;
        try
        {
            var envelope = new CacheEntryEnvelope { IdentityDigest = request.Identity.Digest, EntryVersion = session.Version.Value,
                ContractId = binding.Contract.Alias, CodecId = formatId, CodecVersion = formatVersion };
            var created = await CacheEnvelopeWriter.CreateAsync(session.Stream, envelope, resources.Transient,
                resources.ForCollection(binding.Name).Resident, cancellationToken).ConfigureAwait(false);
            if (created.IsFailure)
            {
                await session.DisposeAsync().ConfigureAwait(false);
                return Result<CachePayloadStage>.Failure(created);
            }

            return Result<CachePayloadStage>.Success(new CachePayloadStage(provider, request, binding, resources, time, item, envelope, session, created.Value, access, tasks, pipeline)
                { transforms = transforms });
        }
        catch
        {
            await session.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>Finalizes original bytes, validates metadata and optional encoded input, then executes the selected payload behaviors once.</summary>
    /// <param name="descriptor">Collection-bounded dependency, lifetime, size, and item metadata.</param>
    /// <param name="validator">An optional registered validator that must consume the complete original representation.</param>
    /// <param name="cancellationToken">Cancellation forwarded through finalization, validation, and transformations.</param>
    /// <returns>Success when the selected stage becomes publishable, or a closed-input, metadata, validation, or transformation failure.</returns>
    public async ValueTask<Result> CompleteAsync(CacheEntryDescriptor descriptor,
        Func<Stream, CancellationToken, ValueTask<Result>> validator, CancellationToken cancellationToken)
    {
        if (this.completed || !this.Stream.CompletedSuccessfully)
        {
            return Result.Failure(new CacheError(CacheErrorCode.Contract, "The source closed or failed its cache-owned destination."));
        }

        this.completed = true;
        descriptor ??= new();
        if (descriptor.SuppressStorage)
        {
            return Result.Failure(new CacheError(CacheErrorCode.Unsupported, "Binary staging cannot suppress storage."));
        }

        var dimensions = CacheSizePolicy.MergeDimensions(this.item, descriptor, this.binding.Options);
        var declared = CacheSizePolicy.ValidateDescriptor(descriptor, this.binding.Options);
        var lifetime = CacheExpirationPolicy.Create(this.binding.Options, descriptor, false, this.time.GetUtcNow());
        if (dimensions.IsFailure || declared.IsFailure || lifetime.IsFailure)
        {
            return Result.Failure().WithErrors(dimensions.IsFailure ? dimensions.Errors : declared.IsFailure ? declared.Errors : lifetime.Errors);
        }

        var encoded = await this.writer.CompleteAsync(cancellationToken).ConfigureAwait(false);
        this.originalPayload = encoded;
        this.selectedPayload = encoded;
        this.Metadata = this.CreateMetadata(descriptor, dimensions.Value, lifetime.Value, encoded, this.original.Version);
        this.writer.Dispose();
        if (validator is not null)
        {
            var validation = await this.ValidateOriginalAsync(validator, cancellationToken).ConfigureAwait(false);
            if (validation.IsFailure)
            {
                return validation;
            }
        }

        var selectedPipeline = this.pipeline ?? (this.binding.Options.Compression.Enabled ? CachePayloadPipeline.CompressionOnly : null);
        if (selectedPipeline is not null)
        {
            Result transformed;
            try { transformed = await selectedPipeline.ExecuteAsync(new(this, this.binding), cancellationToken).ConfigureAwait(false); }
            finally { this.transformationsClosed = true; }

            if (transformed.IsFailure)
            {
                return transformed;
            }
        }

        this.publishable = true;
        return Result.Success();
    }

    /// <summary>Attempts the configured compression decision once and retains the original when no eligible beneficial candidate is selected.</summary>
    /// <param name="cancellationToken">Cancellation forwarded to same-store candidate reads and writes.</param>
    /// <returns>Success after the decision, or a contract failure when transformations are closed, concurrent, or already attempted.</returns>
    public async ValueTask<Result> ApplyCompressionAsync(CancellationToken cancellationToken)
    {
        if (this.transformationsClosed || Interlocked.Exchange(ref this.compressionAttempted, 1) != 0 || Interlocked.Exchange(ref this.transforming, 1) != 0)
        {
            return Result.Failure(new CacheError(CacheErrorCode.Contract, "A staged representation permits one cache-managed compression decision."));
        }

        try
        {
            if (this.binding.Options.Compression.Enabled && this.originalPayload.LogicalBytes >= this.binding.Options.Compression.ThresholdBytes)
            {
                await this.TryCompressAsync(this.selectedPayload, cancellationToken).ConfigureAwait(false);
            }
        }
        finally { Volatile.Write(ref this.transforming, 0); }

        return Result.Success();
    }

    /// <summary>Appends one code-approved transformation by reading the existing selected stage into a bounded same-store candidate.</summary>
    /// <param name="alias">The trusted transform registration alias.</param>
    /// <param name="parameters">Parameters copied into the envelope, limited to 256 bytes.</param>
    /// <param name="cancellationToken">Cancellation forwarded to candidate creation, reads, and writes.</param>
    /// <returns>Success after selecting the completed candidate, or a registration, stage, capacity, or provider failure.</returns>
    public async ValueTask<Result> ApplyTransformAsync(string alias, ReadOnlyMemory<byte> parameters, CancellationToken cancellationToken)
    {
        if (this.transformationsClosed || !this.completed || this.transforms is null || parameters.Length > 256 ||
            !this.transforms.TryGet(alias ?? "", out var transform))
        {
            return Result.Failure(new CacheError(CacheErrorCode.Contract, "Only a code-registered transform with bounded parameters can transform an active stage."));
        }

        var descriptors = this.Descriptors();
        if (descriptors.Count >= 8 || Interlocked.Exchange(ref this.transforming, 1) != 0)
        {
            return Result.Failure(new CacheError(CacheErrorCode.Contract, "Payload transformations are sequential and bounded to eight stages."));
        }

        try
        {
            if (transform.MaximumStateBytes > long.MaxValue - CacheEnvelopeWriter.BlockSize ||
                !ResourceCharge.TryReserve(transform.MaximumStateBytes + CacheEnvelopeWriter.BlockSize, out var memory,
                    this.resources.Transient, this.collectionResources.Resident))
            {
                return Result.Failure(new CacheError(CacheErrorCode.Capacity, "The transform's declared state admission was rejected."));
            }

            using (memory)
            {
                var copied = parameters.ToArray();
                var descriptor = new CachePayloadTransformDescriptor(CacheIdentityNormalizer.Name(transform.FormatId), transform.FormatVersion, Convert.ToBase64String(copied));
                var request = this.request with { OperationId = null };
                var staged = this.access is null ? await this.provider.BeginWriteAsync(request, cancellationToken).ConfigureAwait(false) :
                    await CacheWriteSessionAccess.BeginAsync(this.access, request, this.binding, this.collectionResources, this.tasks, cancellationToken).ConfigureAwait(false);
                if (staged.IsFailure)
                {
                    return Result.Failure().WithErrors(staged.Errors);
                }

                var candidate = staged.Value;
                try
                {
                    var header = this.selectedEnvelope with { EnvelopeVersion = 2, Compression = "stack", EntryVersion = candidate.Version.Value,
                        Transforms = descriptors.Append(descriptor).ToArray() };
                    var created = await CacheEnvelopeWriter.CreateAsync(candidate.Stream, header, this.resources.Transient,
                        this.collectionResources.Resident, cancellationToken).ConfigureAwait(false);
                    if (created.IsFailure)
                    {
                        return Result.Failure().WithErrors(created.Errors);
                    }

                    using var output = created.Value;
                    using var limited = new LimitedWriteStream(output, CacheSizePolicy.MaximumStoredBytes(this.binding.Options));
                    var opened = await this.selected.OpenStagedReadAsync(cancellationToken).ConfigureAwait(false);
                    if (opened.IsFailure)
                    {
                        return Result.Failure().WithErrors(opened.Errors);
                    }

                    await using var raw = opened.Value;
                    var framed = await CacheEnvelopeReader.OpenAsync(raw, this.Metadata, this.resources.Transient,
                        this.collectionResources.Resident, cancellationToken).ConfigureAwait(false);
                    if (framed.IsFailure)
                    {
                        return Result.Failure().WithErrors(framed.Errors);
                    }

                    using var source = framed.Value;
                    await using (var encoder = transform.CreateEncodingStream(limited, copied) ?? throw new InvalidDataException("A transform returned no encoding stream."))
                    {
                        var buffer = GC.AllocateUninitializedArray<byte>(CacheEnvelopeWriter.BlockSize);
                        int read;
                        while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) != 0)
                        {
                            await encoder.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                        }
                    }

                    if (limited.HasFailed)
                    {
                        return Result.Failure(new CacheError(CacheErrorCode.Capacity, "The transform exceeded its bounded staging destination."));
                    }

                    var payload = await output.CompleteAsync(cancellationToken, this.originalPayload.LogicalBytes, this.originalPayload.LogicalHash).ConfigureAwait(false);
                    this.Select(candidate, header, payload);
                    candidate = null;
                    return Result.Success();
                }
                finally
                {
                    if (candidate is not null) { await candidate.DisposeAsync().ConfigureAwait(false); }
                }
            }
        }
        finally { Volatile.Write(ref this.transforming, 0); }
    }

    private IReadOnlyList<CachePayloadTransformDescriptor> Descriptors() => this.selectedEnvelope.EnvelopeVersion == 2 ? this.selectedEnvelope.Transforms :
        this.selectedEnvelope.Compression == "none" ? [] : [new(this.selectedEnvelope.Compression, 1)];

    private void Select(ICacheProviderWriteSession candidate, CacheEntryEnvelope header, CacheEncodedPayload payload)
    {
        if (!ReferenceEquals(this.selected, this.original)) { this.retired.Add(this.selected); }

        this.selected = candidate;
        this.selectedEnvelope = header;
        this.selectedPayload = payload;
        this.Metadata = this.Metadata with { Version = candidate.Version, EnvelopeVersion = header.EnvelopeVersion, Compression = header.Compression,
            StoredSizeBytes = payload.StoredBytes, StoredHash = payload.StoredHash };
    }

    /// <summary>Publishes the completed selected stage using its existing provider session and original fence conditions.</summary>
    /// <param name="cancellationToken">Cancellation forwarded to conditional native publication.</param>
    /// <returns>The provider acknowledgement, or a contract failure when completion did not make the stage publishable.</returns>
    public ValueTask<Result<CacheWriteOutcome>> PublishAsync(CancellationToken cancellationToken) =>
        this.publishable ? this.selected.PublishAsync(this.Metadata, cancellationToken) :
            ValueTask.FromResult(Result<CacheWriteOutcome>.Failure(new CacheError(CacheErrorCode.Contract, "The staged payload is incomplete.")));

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        this.Stream.Dispose();
        this.writer.Dispose();
        if (!ReferenceEquals(this.selected, this.original))
        {
            await this.selected.DisposeAsync().ConfigureAwait(false);
        }

        await this.original.DisposeAsync().ConfigureAwait(false);
        foreach (var session in this.retired)
        {
            await session.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async ValueTask<Result> ValidateOriginalAsync(Func<Stream, CancellationToken, ValueTask<Result>> validator,
        CancellationToken cancellationToken)
    {
        var opened = await this.original.OpenStagedReadAsync(cancellationToken).ConfigureAwait(false);
        if (opened.IsFailure)
        {
            return Result.Failure().WithErrors(opened.Errors);
        }

        await using var raw = opened.Value;
        var framed = await CacheEnvelopeReader.OpenAsync(raw, this.Metadata, this.resources.Transient,
            this.collectionResources.Resident, cancellationToken).ConfigureAwait(false);
        if (framed.IsFailure)
        {
            return Result.Failure().WithErrors(framed.Errors);
        }

        using var logical = framed.Value;
        var validation = await validator(logical, cancellationToken).ConfigureAwait(false);
        if (validation.IsFailure)
        {
            return validation;
        }

        var probe = new byte[1];
        if (await logical.ReadAsync(probe, cancellationToken).ConfigureAwait(false) != 0)
        {
            return Result.Failure(new CacheError(CacheErrorCode.Contract, "The registered validator did not consume the complete input."));
        }

        return await logical.VerifyAsync(false, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask TryCompressAsync(CacheEncodedPayload originalPayload, CancellationToken cancellationToken)
    {
        var selectedBefore = this.selected;
        try
        {
            // The fixed conservative native transform charge is reserved before creating BCL state.
            var charge = CacheCompressionWorkspace.Encode(this.binding.Options.Compression.Algorithm.Value, this.binding.Options.Compression.Level.Value);
            if (!ResourceCharge.TryReserve(charge + CacheEnvelopeWriter.BlockSize, out var scratch,
                this.resources.Transient, this.collectionResources.Resident))
            {
                return;
            }

            using (scratch)
            {
                ICacheProviderWriteSession candidate = null;
                try
                {
                    var request = this.request with { OperationId = null };
                    var staged = this.access is null ? await this.provider.BeginWriteAsync(request, cancellationToken).ConfigureAwait(false) :
                        await CacheWriteSessionAccess.BeginAsync(this.access, request, this.binding, this.collectionResources, this.tasks,
                            cancellationToken).ConfigureAwait(false);
                    if (staged.IsFailure)
                    {
                        return;
                    }

                    candidate = staged.Value;
                    var algorithm = this.binding.Options.Compression.Algorithm == CacheCompressionAlgorithm.GZip ? "gzip" : "brotli";
                    var descriptors = this.Descriptors();
                    if (descriptors.Count >= 8) { return; }

                    var header = this.selectedEnvelope.EnvelopeVersion == 1 && this.selectedEnvelope.Compression == "none" ?
                        this.envelope with { EntryVersion = candidate.Version.Value, Compression = algorithm } :
                        this.selectedEnvelope with { EnvelopeVersion = 2, EntryVersion = candidate.Version.Value, Compression = "stack",
                            Transforms = descriptors.Append(new CachePayloadTransformDescriptor(algorithm, 1)).ToArray() };
                    var created = await CacheEnvelopeWriter.CreateAsync(candidate.Stream, header, this.resources.Transient,
                        this.collectionResources.Resident, cancellationToken).ConfigureAwait(false);
                    if (created.IsFailure)
                    {
                        return;
                    }

                    using var writer = created.Value;
                    var opened = await this.selected.OpenStagedReadAsync(cancellationToken).ConfigureAwait(false);
                    if (opened.IsFailure)
                    {
                        return;
                    }

                    await using var raw = opened.Value;
                    var framed = await CacheEnvelopeReader.OpenAsync(raw, this.Metadata, this.resources.Transient,
                        this.collectionResources.Resident, cancellationToken).ConfigureAwait(false);
                    if (framed.IsFailure)
                    {
                        throw new InvalidDataException("The original staging payload is not intact.");
                    }

                    using var source = framed.Value;
                    await using (Stream compressor = algorithm == "gzip" ? new GZipStream(writer, this.binding.Options.Compression.Level.Value, true) :
                        new BrotliStream(writer, this.binding.Options.Compression.Level.Value, true))
                    {
                        var buffer = GC.AllocateUninitializedArray<byte>(CacheEnvelopeWriter.BlockSize);
                        int read;
                        while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) != 0)
                        {
                            await compressor.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                        }
                    }

                    var compressed = await writer.CompleteAsync(cancellationToken, originalPayload.LogicalBytes, originalPayload.LogicalHash).ConfigureAwait(false);
                    if (compressed.StoredBytes <= originalPayload.StoredBytes * (1 - this.binding.Options.Compression.MinimumSavingRatio))
                    {
                        this.Select(candidate, header, compressed);
                        candidate = null;
                    }
                }
                catch (IOException) when (!cancellationToken.IsCancellationRequested)
                {
                    // A failed candidate never replaces the completed original. Verify it before fallback.
                    var intact = await this.ValidateSelectedAsync(cancellationToken).ConfigureAwait(false);
                    if (intact.IsFailure)
                    {
                        throw new InvalidDataException("The original staging payload cannot be retained safely.");
                    }
                }
                finally
                {
                    if (candidate is not null)
                    {
                        await candidate.DisposeAsync().ConfigureAwait(false);
                    }
                }
            }
        }
        finally
        {
            if (ReferenceEquals(selectedBefore, this.selected)) { this.access?.Observations?.Record(CacheObservationKind.CompressionFallback); }
        }
    }

    private async ValueTask<Result> ValidateSelectedAsync(CancellationToken cancellationToken)
    {
        var opened = await this.selected.OpenStagedReadAsync(cancellationToken).ConfigureAwait(false);
        if (opened.IsFailure) { return Result.Failure().WithErrors(opened.Errors); }

        await using var raw = opened.Value;
        var framed = await CacheEnvelopeReader.OpenAsync(raw, this.Metadata, this.resources.Transient, this.collectionResources.Resident, cancellationToken).ConfigureAwait(false);
        if (framed.IsFailure) { return Result.Failure().WithErrors(framed.Errors); }

        using var source = framed.Value;
        var buffer = new byte[CacheEnvelopeWriter.BlockSize];
        while (await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false) != 0) { }

        return await source.VerifyAsync(false, cancellationToken).ConfigureAwait(false);
    }

    private CacheEntryMetadata CreateMetadata(CacheEntryDescriptor descriptor, IReadOnlyList<CacheDimension> dimensions,
        CacheEntryLifetime lifetime, CacheEncodedPayload encoded, CacheEntryVersion version) => new()
        {
            Identity = this.request.Identity, DisplayKey = this.item.Key, ContractId = this.binding.Contract.Alias, Version = version,
            Dimensions = dimensions, PublishedAt = lifetime.PublishedAt, ExpiresAt = lifetime.ExpiresAt,
            AbsoluteExpiresAt = lifetime.AbsoluteExpiresAt, SlidingIdleTimeout = lifetime.SlidingIdleTimeout,
            LastAccessAt = lifetime.PublishedAt, LogicalSizeBytes = encoded.LogicalBytes, EncodedSizeBytes = encoded.LogicalBytes,
            StoredSizeBytes = encoded.StoredBytes, DecodedSizeBytes = descriptor.DecodedSizeBytes, ItemCount = descriptor.ItemCount,
            Priority = descriptor.Priority, CodecId = this.envelope.CodecId, CodecVersion = this.envelope.CodecVersion,
            Compression = encoded.Compression, ContentType = descriptor.ContentType,
            PayloadHash = encoded.LogicalHash, StoredHash = encoded.StoredHash
        };
}