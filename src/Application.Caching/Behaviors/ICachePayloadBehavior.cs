// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Exposes a completed same-store staged representation before the engine's atomic publication.</summary>
/// <remarks>Behaviors cannot publish, take ownership of caller input, or introduce another payload store.</remarks>
/// <example><code>var actualStoredBytes = context.Metadata.StoredSizeBytes;</code></example>
public sealed class CachePayloadBehaviorContext
{
    private readonly CachePayloadStage stage;

    /// <summary>Binds a staged payload and its immutable collection policy for one payload behavior invocation.</summary>
    public CachePayloadBehaviorContext(CachePayloadStage stage, CacheCollectionBinding binding)
    {
        this.stage = stage;
        this.Collection = binding.Name;
        this.Provider = binding.Provider.Name;
        this.Compression = binding.Options.Compression;
    }

    /// <summary>Gets the bounded declared collection alias.</summary>
    public string Collection { get; }

    /// <summary>Gets the bounded selected complete-provider alias.</summary>
    public string Provider { get; }

    /// <summary>Gets the actual currently selected staged format/sizes. Publication has not yet occurred.</summary>
    public CacheEntryMetadata Metadata => this.stage.Metadata;

    /// <summary>Gets immutable effective write compression settings. Existing read decoders are independent of this flag.</summary>
    public CacheCompressionOptions Compression { get; }

    /// <summary>Streams the selected staged body through one approved transform into a new version in the same provider.
    /// The logical value is encoded only once; transform parameters are copied, persisted, and bounded to 256 bytes.</summary>
    public ValueTask<Result> ApplyTransformAsync(string alias, ReadOnlyMemory<byte> parameters = default, CancellationToken cancellationToken = default) =>
        this.stage.ApplyTransformAsync(alias, parameters, cancellationToken);

    /// <summary>Gets or sets the runtime-owned continuation guard shared by this payload behavior invocation.</summary>
    public object Invocation { get; set; }

    /// <summary>Applies the registered bounded compression policy to this staged payload without publishing it.</summary>
    public ValueTask<Result> CompressAsync(CancellationToken cancellationToken) => this.stage.ApplyCompressionAsync(cancellationToken);
}

/// <summary>Continues one bounded transformation chain, at most once.</summary>
/// <param name="context">The supplied staging context.</param>
/// <param name="cancellationToken">The original bounded work/transfer token.</param>
/// <returns>Successful complete transformation or an explicit failure that prevents publication.</returns>
public delegate ValueTask<Result> CachePayloadContinuation(CachePayloadBehaviorContext context, CancellationToken cancellationToken);

/// <summary>Acts after one original encode/input pass and before engine-owned publication.</summary>
/// <example><code>return await next(context, cancellationToken);</code></example>
public interface ICachePayloadBehavior
{
    /// <summary>Checks contract compatibility once while building this collection's chain.</summary>
    /// <param name="valueType">The collection's statically registered value type.</param>
    /// <param name="isBinary">Whether the collection declares raw binary payloads.</param>
    /// <returns>True when this behavior can participate in the declared contract.</returns>
    bool Supports(Type valueType, bool isBinary) => true;

    /// <summary>Runs under selected-provider staging and mandatory size/integrity/fence limits.</summary>
    /// <param name="context">The original engine-supplied context for this invocation.</param>
    /// <param name="next">The remaining mandatory chain, which may be invoked at most once with the original context.</param>
    /// <param name="cancellationToken">The original bounded invocation cancellation token.</param>
    /// <returns>The completed payload transformation result.</returns>
    /// <example><code>return await next(context, cancellationToken);</code></example>
    ValueTask<Result> ExecuteAsync(CachePayloadBehaviorContext context, CachePayloadContinuation next, CancellationToken cancellationToken = default);
}