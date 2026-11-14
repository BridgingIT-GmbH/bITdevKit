// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Selects an explicitly configured GZip/Brotli candidate using completed same-store staging and actual storage savings.</summary>
/// <remarks>The source/codec is never repeated. Recoverable candidate failure may retain only a verified intact original.</remarks>
/// <example><code>collection.With(x => x.Compression.Enabled, true);</code></example>
public sealed class CompressionCachePayloadBehavior : ICachePayloadBehavior
{
    /// <inheritdoc />
    public async ValueTask<Result> ExecuteAsync(CachePayloadBehaviorContext context, CachePayloadContinuation next,
        CancellationToken cancellationToken = default)
    {
        var transformed = await context.CompressAsync(cancellationToken).ConfigureAwait(false);
        return transformed.IsFailure ? transformed : await next(context, cancellationToken).ConfigureAwait(false);
    }
}