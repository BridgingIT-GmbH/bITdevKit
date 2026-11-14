// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Application.Modules.Core;

using System.Text.Json;
using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Common;

/// <summary>Reuses the serialized shared reference catalog through the application's declared cache collection.</summary>
/// <remarks>The catalog contains public reference data and uses <see cref="JsonSerializerOptions.Web"/> for camelCase JSON.</remarks>
/// <param name="cache">The application-owned cache entry point.</param>
/// <example><code>var json = await catalog.GetJsonAsync(cancellationToken); // Deliver JSON without serializing the string again.</code></example>
public sealed class EnumerationCatalog(IApplicationCache cache)
{
    /// <summary>Gets the startup-declared collection storing this shared JSON representation.</summary>
    /// <example><code>var entries = cache.GetCollection&lt;string&gt;(EnumerationCatalog.CollectionName);</code></example>
    public const string CollectionName = "reference-catalog";

    /// <summary>Gets the shared enumeration entry key.</summary>
    /// <example><code>await cache.GetCollection&lt;string&gt;(EnumerationCatalog.CollectionName).RemoveAsync(catalog.CacheKey);</code></example>
    public string CacheKey => "enumerations";

    /// <summary>Returns the JSON catalog with web serializer defaults, loading it once within the collection's configured ownership scope.</summary>
    /// <param name="cancellationToken">Cancels this caller's wait without changing another caller's ownership.</param>
    /// <returns>The complete JSON object text, or an explicit cache/source failure.</returns>
    /// <example><code>var result = await catalog.GetJsonAsync(cancellationToken);</code></example>
    public async ValueTask<Result<string>> GetJsonAsync(CancellationToken cancellationToken = default)
    {
        // Cache the completed representation so custom Enumeration types need no cache-specific reader.
        var result = await cache.GetCollection<string>(CollectionName).GetOrLoadAsync(new(this.CacheKey), (_, _) =>
            ValueTask.FromResult<Result<CacheLoadResult<string>>>(new CacheLoadResult<string>(
                JsonSerializer.Serialize(new EnumerationModel(), JsonSerializerOptions.Web))), cancellationToken);

        return result.Map(lookup => lookup.Value);
    }
}