// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Writes a repeatable binary source to the bounded engine-owned destination.</summary>
/// <remarks>The destination remains open. No uncached binary delivery or business mutation is permitted.</remarks>
/// <example><code>var result = await loader.LoadAsync(context, destination, cancellationToken);</code></example>
public interface ICacheBinaryRefreshLoader
{
    /// <summary>Writes a complete source payload and returns its metadata, leaving publication to the engine.</summary>
    ValueTask<Result<CacheBinaryLoadResult>> LoadAsync(CacheLoadContext context, Stream destination, CancellationToken cancellationToken = default);
}