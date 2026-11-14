// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Exposes a shared streaming transform through the cache registration contract without changing its bytes or ownership.</summary>
/// <param name="transform">The trusted, shared streaming implementation.</param>
/// <example><code>caching.WithPayloadTransform("custom", services => new CachePayloadTransformAdapter(CreateSharedTransform(services)));</code></example>
public sealed class CachePayloadTransformAdapter(IStreamingPayloadTransform transform) : ICachePayloadTransform
{
    private readonly IStreamingPayloadTransform transform = transform ?? throw new ArgumentNullException(nameof(transform));

    /// <inheritdoc />
    public string FormatId => this.transform.FormatId;

    /// <inheritdoc />
    public int FormatVersion => this.transform.FormatVersion;

    /// <inheritdoc />
    public long MaximumStateBytes => this.transform.MaximumStateBytes;

    /// <inheritdoc />
    public Stream CreateEncodingStream(Stream destination, ReadOnlyMemory<byte> parameters) => this.transform.CreateEncodingStream(destination, parameters);

    /// <inheritdoc />
    public Stream CreateDecodingStream(Stream source, ReadOnlyMemory<byte> parameters) => this.transform.CreateDecodingStream(source, parameters);
}