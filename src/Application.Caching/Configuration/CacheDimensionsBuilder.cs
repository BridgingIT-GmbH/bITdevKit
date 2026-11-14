// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Declares typed dependency schemas through a collection's grouped dimension callback.</summary>
/// <param name="collection">The collection builder receiving each declaration.</param>
/// <remarks>
/// Declarations append in order and share validation with the individual collection helpers. Nullable CLR types select
/// the underlying scalar kind; explicit null values still require the allowNull setting. Configuration can replace the
/// complete list, and catalog finalization checks its sixteen-dimension bound.
/// </remarks>
/// <exception cref="ArgumentNullException"><paramref name="collection"/> is null.</exception>
/// <example><code>collection.Dimensions(d => d.Set&lt;string&gt;("tags").Scalar&lt;Guid&gt;("owner"));</code></example>
public sealed class CacheDimensionsBuilder(CacheCollectionBuilder collection)
{
    private readonly CacheCollectionBuilder collection = collection ?? throw new ArgumentNullException(nameof(collection));

    /// <summary>Declares one scalar dependency with an explicit null policy.</summary>
    /// <typeparam name="T">String, Boolean, a signed integer alias, Guid, or DateTimeOffset, including nullable forms.</typeparam>
    /// <param name="name">The unique dimension name, compared using invariant case normalization.</param>
    /// <param name="allowNull">Whether the schema admits an explicitly typed null. Defaults to false.</param>
    /// <returns>The same dimension builder.</returns>
    /// <remarks>Signed integer aliases use the portable Int64 kind. Nullable CLR types do not enable null values automatically.</remarks>
    /// <exception cref="ArgumentException">The name or CLR type is invalid, or the normalized name is already declared.</exception>
    /// <example><code>dimensions.Scalar&lt;string&gt;("region").Scalar&lt;int?&gt;("priority", allowNull: true);</code></example>
    public CacheDimensionsBuilder Scalar<T>(string name, bool allowNull = false)
    {
        this.collection.WithScalarDimension<T>(name, allowNull);
        return this;
    }

    /// <summary>Declares a membership set whose values all use one portable scalar kind.</summary>
    /// <typeparam name="T">String, Boolean, a signed integer alias, Guid, or DateTimeOffset, including nullable forms.</typeparam>
    /// <param name="name">The unique dimension name, compared using invariant case normalization.</param>
    /// <param name="allowNull">Whether the set admits explicitly typed null members. Defaults to false.</param>
    /// <returns>The same dimension builder.</returns>
    /// <remarks>An empty set retains its declared kind. Nullable CLR types do not enable null members automatically.</remarks>
    /// <exception cref="ArgumentException">The name or CLR type is invalid, or the normalized name is already declared.</exception>
    /// <example><code>dimensions.Set&lt;string&gt;("tags").Set&lt;Guid&gt;("owners");</code></example>
    public CacheDimensionsBuilder Set<T>(string name, bool allowNull = false)
    {
        this.collection.WithSetDimension<T>(name, allowNull);
        return this;
    }

    /// <summary>Declares a half-open interval with non-null Integer or UTC Timestamp endpoints.</summary>
    /// <typeparam name="T">A signed integer alias or DateTimeOffset, including nullable forms with non-null endpoints.</typeparam>
    /// <param name="name">The unique dimension name, compared using invariant case normalization.</param>
    /// <returns>The same dimension builder.</returns>
    /// <remarks>Catalog finalization rejects intervals of other scalar kinds. Values must supply ordered, non-null endpoints.</remarks>
    /// <exception cref="ArgumentException">The name or CLR type is invalid, or the normalized name is already declared.</exception>
    /// <example><code>dimensions.Interval&lt;long&gt;("sequence").Interval&lt;DateTimeOffset&gt;("coverage");</code></example>
    public CacheDimensionsBuilder Interval<T>(string name)
    {
        this.collection.WithIntervalDimension<T>(name);
        return this;
    }
}