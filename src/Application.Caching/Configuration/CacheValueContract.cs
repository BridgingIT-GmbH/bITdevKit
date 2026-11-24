// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

using System.Collections.Frozen;

/// <summary>Describes a trusted startup value contract. Configuration can select its alias without loading CLR names.</summary>
/// <example><code>var type = catalog.Collections["CARDS"].Contract.ValueType;</code></example>
public abstract class CacheValueContract
{
    private protected CacheValueContract(string alias, Type valueType, bool isBinary)
    {
        this.Alias = CacheIdentityNormalizer.Name(alias);
        this.ValueType = valueType;
        this.IsBinary = isBinary;
    }

    /// <summary>Gets the stable registered alias.</summary>
    public string Alias { get; }

    /// <summary>Gets the code-approved CLR type; raw binary contracts use Stream.</summary>
    public Type ValueType { get; }

    /// <summary>Gets whether the contract is a declared raw binary format.</summary>
    public bool IsBinary { get; }

    /// <summary>Gets whether code approved decoded immutable sharing with conservative accounting.</summary>
    public abstract bool SupportsDecodedSharing { get; }

    /// <summary>Tests whether the trusted contract declares the selected codec alias.</summary>
    public abstract bool HasCodec(string alias);

    /// <summary>Creates a declared typed or binary collection handle through the selected engine factory and complete provider.</summary>
    public abstract object CreateHandle(ICacheCollectionFactory factory, CacheCollectionBinding binding, ICacheStoreProvider provider);
}

/// <summary>Describes trusted typed codec profiles and optional immutable-sharing approval.</summary>
/// <typeparam name="T">The only declared value type.</typeparam>
/// <example><code>var codec = contract.GetCodec("json", services);</code></example>
public sealed class CacheValueContract<T> : CacheValueContract
{
    private readonly FrozenDictionary<string, CacheCodecProfile<T>> profiles;

    /// <summary>Binds trusted codec profiles, conservative decoded graph estimation and explicit immutable-sharing approval.</summary>
    public CacheValueContract(string alias, IEnumerable<CacheCodecProfile<T>> profiles, Func<T, long?> estimate, bool immutable)
        : base(alias, typeof(T), false)
    {
        this.profiles = profiles.ToFrozenDictionary(p => CacheIdentityNormalizer.Name(p.Alias), StringComparer.OrdinalIgnoreCase);
        this.EstimateDecodedBytes = estimate;
        this.SupportsDecodedSharing = immutable && estimate is not null;
    }

    /// <summary>Gets code-approved conservative graph accounting, or null when unknown.</summary>
    public Func<T, long?> EstimateDecodedBytes { get; }

    /// <inheritdoc />
    public override bool SupportsDecodedSharing { get; }

    /// <summary>Resolves one code-registered codec profile. No runtime type-name activation occurs.</summary>
    public IValueCodec<T> GetCodec(string alias, IServiceProvider services) =>
        this.profiles[CacheIdentityNormalizer.Name(alias)].Codec(services);

    /// <summary>Resolves the selected explicit bounded contract validator, or null when unsupported.</summary>
    public IValueCodecValidator<T> GetValidator(string alias, IServiceProvider services) =>
        this.profiles[CacheIdentityNormalizer.Name(alias)].Validator?.Invoke(services);

    /// <summary>Resolves registered persisted readers and rejects ambiguous format/version mappings.</summary>
    public ValueCodecRegistry CreateReaders(IServiceProvider services) => new(this.profiles.Values
        .Select(p => (IValueCodecRegistration)new ValueCodecRegistration<T>(p.Codec(services))));

    /// <summary>Tests whether the trusted contract declares the selected codec alias.</summary>
    public override bool HasCodec(string alias) => this.profiles.ContainsKey(CacheIdentityNormalizer.Name(alias));

    /// <summary>Resolves the selected writer, optional validator and all persisted readers, rejecting invalid format/resource declarations.</summary>
    public CacheResolvedCodecs<T> Resolve(string alias, IServiceProvider services)
    {
        var codecs = this.profiles.ToDictionary(p => p.Key, p => p.Value.Codec(services), StringComparer.OrdinalIgnoreCase);
        foreach (var codec in codecs.Values)
        {
            ArgumentNullException.ThrowIfNull(codec);
            _ = CacheIdentityNormalizer.Name(codec.FormatId);
            if (codec.FormatVersion < 1 || codec.Capabilities is null || codec.Capabilities.MaximumScratchBytes is < 0)
            {
                throw new ArgumentException("A registered codec reader has an invalid format or resource declaration.");
            }
        }

        var selected = CacheIdentityNormalizer.Name(alias);
        var validator = this.profiles[selected].Validator?.Invoke(services);
        return new(codecs[selected], validator, new(codecs.Values.Select(c => new ValueCodecRegistration<T>(c))));
    }

    /// <summary>Creates a declared typed or binary collection handle through the selected engine factory and complete provider.</summary>
    public override object CreateHandle(ICacheCollectionFactory factory, CacheCollectionBinding binding, ICacheStoreProvider provider) =>
        factory.Create(binding, this, provider);
}

/// <summary>Describes a declared raw binary format without object serialization.</summary>
/// <example><code>var format = contract.FormatId;</code></example>
public sealed class CacheBinaryValueContract : CacheValueContract
{
    /// <summary>Declares a raw binary format and optional single-pass validator, rejecting invalid format identifiers or versions.</summary>
    public CacheBinaryValueContract(string alias, string formatId, int formatVersion,
        Func<Stream, CancellationToken, ValueTask<Result>> validator) : base(alias, typeof(Stream), true)
    {
        this.FormatId = CacheIdentityNormalizer.Name(formatId);
        this.FormatVersion = formatVersion > 0 ? formatVersion : throw new ArgumentOutOfRangeException(nameof(formatVersion));
        this.Validator = validator;
    }

    /// <summary>Gets the stable raw format identifier.</summary>
    public string FormatId { get; }

    /// <summary>Gets the persisted raw format version.</summary>
    public int FormatVersion { get; }

    /// <summary>Gets optional single-pass bounded raw validation. Null explicitly selects integrity/size-only validation.</summary>
    public Func<Stream, CancellationToken, ValueTask<Result>> Validator { get; }

    /// <inheritdoc />
    public override bool SupportsDecodedSharing => false;

    /// <inheritdoc />
    public override bool HasCodec(string alias) => StringComparer.OrdinalIgnoreCase.Equals(alias, "binary");

    /// <inheritdoc />
    public override object CreateHandle(ICacheCollectionFactory factory, CacheCollectionBinding binding, ICacheStoreProvider provider) =>
        factory.CreateBinary(binding, this, provider);
}

/// <summary>Registers one trusted typed codec factory and its optional bounded nonmaterializing validator.</summary>
/// <typeparam name="T">The declared value or operation result type.</typeparam>
/// <example><code>var writer = profile.Codec(services);</code></example>
public sealed record CacheCodecProfile<T>(string Alias, Func<IServiceProvider, IValueCodec<T>> Codec,
    Func<IServiceProvider, IValueCodecValidator<T>> Validator);

/// <summary>Contains a selected writer, optional encoded-write validator and persisted-reader registry resolved for one handle.</summary>
/// <typeparam name="T">The declared value or operation result type.</typeparam>
/// <example><code>var format = resolved.Writer.FormatId;</code></example>
public sealed record CacheResolvedCodecs<T>(IValueCodec<T> Writer, IValueCodecValidator<T> Validator, ValueCodecRegistry Readers);