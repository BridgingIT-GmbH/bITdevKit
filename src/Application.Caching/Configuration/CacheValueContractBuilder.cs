// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

using System.Text.Json;

/// <summary>Registers trusted typed representation profiles and optional conservative object accounting.</summary>
/// <typeparam name="T">The registered value contract.</typeparam>
/// <remarks>
/// Configure this builder during startup from one thread. Resolved codec and validator instances are reused by their
/// collection and must support concurrent calls without retaining per-call state or request-scoped dependencies.
/// Each profile must declare a unique persisted format identifier and version within this contract.
/// </remarks>
/// <example><code>contract.WithImmutableSharing(value => 128L + value.Code.Length * 2L);</code></example>
public sealed class CacheValueContractBuilder<T>
{
    private readonly Dictionary<string, CacheCodecProfile<T>> profiles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["JSON"] = new("JSON", _ => new SystemTextJsonValueCodec<T>(), null)
    };
    private Func<T, long?> estimate;
    private bool immutable;

    /// <summary>Registers an explicit codec reader/writer profile and optional exact-format bounded validator.</summary>
    /// <param name="alias">The unique profile alias that collections can select.</param>
    /// <param name="codec">A trusted factory for a codec that supports concurrent collection calls.</param>
    /// <param name="validator">An optional concurrent validator for the codec's exact format identifier and version.</param>
    /// <returns>This builder for further contract configuration.</returns>
    /// <remarks>
    /// Factories can run more than once and must not capture request-scoped dependencies. Keep legacy reader profiles
    /// while persisted entries use their formats. Registering a second alias for the same persisted format and version
    /// fails reader resolution. Use <see cref="WithJsonCodec"/> to replace the existing JSON profile.
    /// A validator must validate the complete input in one bounded pass without materializing the value graph.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="codec"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="alias"/> is invalid or already registered.</exception>
    /// <example><code>contract.WithCodec("compact", services => services.GetRequiredService&lt;CompactCardCodec&gt;());</code></example>
    public CacheValueContractBuilder<T> WithCodec(string alias, Func<IServiceProvider, IValueCodec<T>> codec,
        Func<IServiceProvider, IValueCodecValidator<T>> validator = null)
    {
        ArgumentNullException.ThrowIfNull(codec);
        if (!this.profiles.TryAdd(CacheIdentityNormalizer.Name(alias), new(alias, codec, validator)))
        {
            throw new ArgumentException("A codec profile alias was registered twice.", nameof(alias));
        }

        return this;
    }

    /// <summary>Explicitly replaces the default JSON profile with a code-approved configured JSON codec.</summary>
    /// <param name="codec">A trusted factory for the replacement codec, whose instances must support concurrent calls.</param>
    /// <param name="validator">An optional concurrent, bounded validator for the replacement codec's exact format and version.</param>
    /// <returns>This builder for further contract configuration.</returns>
    /// <remarks>
    /// Replaces both the codec factory and validator registered under JSON. Omitting the validator removes any earlier
    /// JSON validator. Factories must not capture request-scoped dependencies. The replacement's persisted format and
    /// version must remain unique among profiles, and its reader must understand existing entries using that format.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="codec"/> is null.</exception>
    /// <example><code>contract.WithJsonCodec(_ => new SystemTextJsonValueCodec&lt;Card&gt;(JsonSerializerOptions.Web));</code></example>
    public CacheValueContractBuilder<T> WithJsonCodec(Func<IServiceProvider, IValueCodec<T>> codec,
        Func<IServiceProvider, IValueCodecValidator<T>> validator = null)
    {
        ArgumentNullException.ThrowIfNull(codec);
        this.profiles["JSON"] = new("JSON", codec, validator);
        return this;
    }

    /// <summary>Replaces the JSON profile with the standard JSON codec using a registration-time options snapshot.</summary>
    /// <param name="options">Caller-owned JSON conventions copied immediately without freezing the caller's instance.</param>
    /// <returns>This builder for further contract configuration.</returns>
    /// <remarks>
    /// Later changes to the options or its converter list do not change this registration. Converter and resolver objects
    /// are shared references and must remain safe for concurrent use. This replaces any earlier JSON codec and validator;
    /// use <see cref="WithJsonCodec"/> when an encoded-write validator is required. The persisted format remains JSON
    /// version 1, so choose conventions compatible with retained entries or change the collection's contract version.
    /// Other codec profiles and decoded-sharing settings remain unchanged.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    /// <example><code>contract.WithJsonOptions(JsonSerializerOptions.Web);</code></example>
    public CacheValueContractBuilder<T> WithJsonOptions(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var snapshot = new JsonSerializerOptions(options);
        return this.WithJsonCodec(_ => new SystemTextJsonValueCodec<T>(snapshot));
    }

    /// <summary>Registers a conservative graph estimate without approving immutable shared retention.</summary>
    /// <param name="estimate">A concurrent estimator returning retained graph bytes, or null when the size is unknown.</param>
    /// <returns>This builder for further contract configuration.</returns>
    /// <remarks>
    /// The estimator can be reused concurrently and must not capture request-scoped dependencies. Include referenced
    /// objects in the estimate. Unknown, negative, or oversized estimates decline decoded retention. This method preserves
    /// any earlier immutable-sharing approval but does not grant it on its own.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="estimate"/> is null.</exception>
    /// <example><code>contract.WithDecodedSize(value => value is null ? 0L : 128L + value.Name.Length * 2L);</code></example>
    public CacheValueContractBuilder<T> WithDecodedSize(Func<T, long?> estimate)
    {
        this.estimate = estimate ?? throw new ArgumentNullException(nameof(estimate));
        return this;
    }

    /// <summary>Approves immutable sharing and conservative retained graph accounting in trusted application code.</summary>
    /// <param name="estimate">A concurrent estimator returning retained graph bytes, or null when the size is unknown.</param>
    /// <returns>This builder for further contract configuration.</returns>
    /// <remarks>
    /// Every reachable object must remain immutable for the complete retention and consumer lifetime. Collections can
    /// return the same instance to concurrent callers. The estimator must account for the entire retained graph and must
    /// not capture request-scoped dependencies. Collection policy still controls whether decoded retention is enabled.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="estimate"/> is null.</exception>
    /// <example><code>contract.WithImmutableSharing(value => value is null ? 0L : 128L + value.Code.Length * 2L);</code></example>
    public CacheValueContractBuilder<T> WithImmutableSharing(Func<T, long?> estimate)
    {
        this.immutable = true;
        return this.WithDecodedSize(estimate);
    }

    /// <summary>Creates the finalized trusted typed contract from registered profiles and immutable-sharing approval.</summary>
    /// <param name="alias">The stable contract alias selected by collections.</param>
    /// <returns>A contract with a snapshot of the registered profiles and current accounting settings.</returns>
    /// <remarks>
    /// Factories and estimators remain trusted shared delegates. Building does not resolve codecs or validate duplicate
    /// persisted format/version pairs; reader resolution performs that validation when the collection is created.
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="alias"/> is invalid.</exception>
    /// <example><code>var valueContract = contract.Build("cards");</code></example>
    public CacheValueContract<T> Build(string alias) => new(alias, this.profiles.Values, this.estimate, this.immutable);
}