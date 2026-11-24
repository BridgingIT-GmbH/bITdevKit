// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Caching;

using System.Text.Json;
using System.Collections.Frozen;
using System.Collections.ObjectModel;
using BridgingIT.DevKit.Common;

/// <summary>Defines a code-approved public boundary model for typed HTTP publication.</summary>
/// <example><code>cache.WithHttpModel&lt;ProductCard, ProductInput&gt;("card", input => new ProductCard(input.Code), input => 128 + input.Code.Length * 2L);</code></example>
public interface ICacheHttpWriteProfile
{
    /// <summary>Gets the trusted declared application value type.</summary>
    Type ValueType { get; }

    /// <summary>Gets the registered public request media type.</summary>
    string ContentType { get; }

    /// <summary>Gets the finite serialized request bound.</summary>
    int MaximumBodyBytes { get; }

    /// <summary>Gets the conservative boundary-model admission ceiling.</summary>
    long MaximumDecodedBytes { get; }

    /// <summary>Decodes and maps one bounded public model without selecting a source or codec from request data.</summary>
    ValueTask<Result<object>> ReadAsync(Stream input, CancellationToken cancellationToken = default);
}

/// <summary>Maps a JSON boundary DTO into one code-declared typed collection value.</summary>
/// <typeparam name="TValue">The registered application value.</typeparam>
/// <typeparam name="TModel">The public boundary model.</typeparam>
/// <param name="map">Trusted boundary mapping.</param>
/// <param name="estimate">Conservative boundary-model size estimate.</param>
/// <param name="maximumBodyBytes">Finite serialized request bound.</param>
/// <param name="maximumDecodedBytes">Finite boundary graph admission.</param>
/// <example><code>var profile = new CacheJsonHttpWriteProfile&lt;ProductCard, ProductInput&gt;(Map, Estimate);</code></example>
public sealed class CacheJsonHttpWriteProfile<TValue, TModel>(Func<TModel, TValue> map, Func<TModel, long?> estimate,
    int maximumBodyBytes = 65536, long maximumDecodedBytes = 1048576) : ICacheHttpWriteProfile
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { MaxDepth = 32 };

    /// <inheritdoc />
    public Type ValueType => typeof(TValue);

    /// <inheritdoc />
    public string ContentType => "application/json";

    /// <inheritdoc />
    public int MaximumBodyBytes => maximumBodyBytes;

    /// <inheritdoc />
    public long MaximumDecodedBytes => maximumDecodedBytes;

    /// <inheritdoc />
    public async ValueTask<Result<object>> ReadAsync(Stream input, CancellationToken cancellationToken = default)
    {
        try
        {
            var model = await JsonSerializer.DeserializeAsync<TModel>(input, Json, cancellationToken).ConfigureAwait(false);
            var bytes = estimate(model);
            if (bytes is not >= 0 || bytes > maximumDecodedBytes)
            {
                return Result<object>.Failure(new BridgingIT.DevKit.Application.Caching.CacheError(
                    BridgingIT.DevKit.Application.Caching.CacheErrorCode.Capacity, "The public boundary model exceeds its conservative graph limit."));
            }

            return Result<object>.Success(map(model));
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or NotSupportedException)
        {
            return Result<object>.Failure(new BridgingIT.DevKit.Application.Caching.CacheError(
                BridgingIT.DevKit.Application.Caching.CacheErrorCode.Validation, "The request is not a valid registered public boundary model."));
        }
    }
}

/// <summary>Holds code-approved public write mappings; request data cannot add contracts or factories.</summary>
/// <example><code>var profile = registrations.Profiles["card"];</code></example>
public sealed class CacheHttpWriteProfiles
{
    private readonly Lock gate = new();
    private readonly Dictionary<string, ICacheHttpWriteProfile> profiles = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyDictionary<string, ICacheHttpWriteProfile> frozen;

    /// <summary>Gets trusted registered profiles.</summary>
    public IReadOnlyDictionary<string, ICacheHttpWriteProfile> Profiles
    {
        get { lock (this.gate) { return this.frozen ?? new ReadOnlyDictionary<string, ICacheHttpWriteProfile>(this.profiles); } }
    }

    /// <summary>Registers one explicit public boundary model per value contract.</summary>
    public void Add(string contract, ICacheHttpWriteProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        lock (this.gate)
        {
            if (this.frozen is not null) { throw new InvalidOperationException("Public cache HTTP mappings are finalized for this host lifetime."); }

            if (!this.profiles.TryAdd(BridgingIT.DevKit.Application.Caching.CacheIdentityNormalizer.Name(contract), profile))
            {
                throw new ArgumentException("A public cache HTTP write mapping was registered twice.");
            }
        }
    }

    /// <summary>Finalizes trusted profile lookup once after startup type and resource-limit validation.</summary>
    public void Freeze()
    {
        lock (this.gate) { this.frozen ??= this.profiles.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase); }
    }
}