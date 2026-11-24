// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

using System.IO.Compression;

/// <summary>Resolves only trusted code-registered streaming transformations and the built-in persisted compression formats.</summary>
/// <example><code>if (registry.TryResolve(descriptor, out var transform, out var parameters)) { UseApprovedReader(transform, parameters); }</code></example>
public sealed class CachePayloadTransformRegistry
{
    private static readonly GZipPayloadTransform GZip = new();
    private readonly Dictionary<string, ICachePayloadTransform> aliases = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<(string Id, int Version), ICachePayloadTransform> readers = [];

    /// <summary>Instantiates approved transform factories and rejects duplicate formats or invalid finite state declarations.</summary>
    /// <param name="catalog">The immutable catalog containing trusted transform registrations.</param>
    /// <param name="services">The host services passed only to approved code factories.</param>
    /// <exception cref="ArgumentException">A factory returns null, a format duplicates another registration or built-in format, or its version or state charge is invalid.</exception>
    public CachePayloadTransformRegistry(CacheCatalog catalog, IServiceProvider services)
    {
        foreach (var (alias, factory) in catalog.Registry.PayloadTransforms)
        {
            var transform = factory(services) ?? throw new ArgumentException("A payload transform factory returned null.");
            var id = CacheIdentityNormalizer.Name(transform.FormatId);
            if (id is "GZIP" or "BROTLI" || transform.FormatVersion <= 0 || transform.MaximumStateBytes < 0 ||
                !this.readers.TryAdd((id, transform.FormatVersion), transform))
            {
                throw new ArgumentException("A transform requires one unique registered format/version and a finite nonnegative state charge.");
            }

            this.aliases.Add(alias, transform);
        }
    }

    /// <summary>Finds a trusted custom transform by its code registration alias.</summary>
    /// <param name="alias">The configured registration alias.</param>
    /// <param name="transform">The registered instance when found.</param>
    /// <returns>True when the custom registration exists.</returns>
    public bool TryGet(string alias, out ICachePayloadTransform transform) => this.aliases.TryGetValue(alias, out transform);

    /// <summary>Validates a persisted format and bounded Base64 parameters without activating request-selected types.</summary>
    /// <param name="descriptor">The persisted format, positive version, and at most 256 decoded parameter bytes.</param>
    /// <param name="transform">The custom instance, or null for a recognized built-in compression reader.</param>
    /// <param name="parameters">The decoded bounded parameters when resolution succeeds.</param>
    /// <returns>True only for a recognized custom format or built-in version-one gzip or Brotli with empty parameters.</returns>
    public bool TryResolve(CachePayloadTransformDescriptor descriptor, out ICachePayloadTransform transform, out byte[] parameters)
    {
        transform = null;
        parameters = null;
        if (descriptor is null || descriptor.FormatId is null || descriptor.FormatVersion <= 0 || descriptor.Parameters is null || descriptor.Parameters.Length > 344)
        {
            return false;
        }

        try
        {
            var id = CacheIdentityNormalizer.Name(descriptor.FormatId);
            parameters = Convert.FromBase64String(descriptor.Parameters);
            if (parameters.Length > 256)
            {
                return false;
            }

            return (id is "GZIP" or "BROTLI" && descriptor.FormatVersion == 1 && parameters.Length == 0) || this.readers.TryGetValue((id, descriptor.FormatVersion), out transform);
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException) { return false; }
    }

    /// <summary>Gets conservative decode workspace bytes for an already resolved custom or built-in transform.</summary>
    /// <param name="descriptor">The successfully resolved persisted descriptor.</param>
    /// <param name="transform">The custom reader, or null for built-in compression.</param>
    /// <returns>The declared custom state charge or built-in decode reservation.</returns>
    public static long StateBytes(CachePayloadTransformDescriptor descriptor, ICachePayloadTransform transform) =>
        transform?.MaximumStateBytes ?? (descriptor.FormatId.Equals("gzip", StringComparison.OrdinalIgnoreCase) ? CacheDefaults.MiB : CacheCompressionWorkspace.BrotliDecode);

    /// <summary>Creates the selected approved decoding stream after format resolution and workspace admission.</summary>
    /// <param name="descriptor">The successfully resolved persisted descriptor.</param>
    /// <param name="transform">The approved custom reader, or null for built-in compression.</param>
    /// <param name="source">The forward-readable transformed input.</param>
    /// <param name="parameters">The previously validated decoded format parameters.</param>
    /// <returns>A decoding stream; built-in wrappers leave the supplied source open.</returns>
    public static Stream Decode(CachePayloadTransformDescriptor descriptor, ICachePayloadTransform transform, Stream source, byte[] parameters) =>
        transform is not null ? transform.CreateDecodingStream(source, parameters) : descriptor.FormatId.Equals("gzip", StringComparison.OrdinalIgnoreCase) ?
            GZip.CreateDecodingStream(source, parameters) : new BrotliStream(source, CompressionMode.Decompress, true);
}