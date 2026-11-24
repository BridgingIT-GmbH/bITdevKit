// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>Registers trusted readers for declared value contracts and byte formats.</summary>
/// <example><code>services.AddValueCodecs(codecs => codecs.WithCodec&lt;ProductCard, ProductCardCodec&gt;());</code></example>
public sealed class ValueCodecsBuilder
{
    private readonly IServiceCollection services;

    internal ValueCodecsBuilder(IServiceCollection services)
    {
        this.services = services;
    }

    /// <summary>Registers a singleton codec whose constructor dependencies are resolved through dependency injection.</summary>
    /// <typeparam name="T">The declared value contract.</typeparam>
    /// <typeparam name="TCodec">The codec implementation for one exact format and version.</typeparam>
    /// <param name="isDefault">Whether this codec also becomes the default writer resolved as <see cref="IValueCodec{T}"/>.</param>
    /// <returns>The current builder.</returns>
    /// <remarks>An existing registration for <typeparamref name="TCodec"/> is preserved. A later explicit default replaces the writer without removing readers. Codec instances must support concurrent calls.</remarks>
    /// <example><code>codecs.WithCodec&lt;ProductCard, ProductCardCodecV1&gt;().WithCodec&lt;ProductCard, ProductCardCodecV2&gt;(isDefault: true);</code></example>
    public ValueCodecsBuilder WithCodec<T, TCodec>(bool isDefault = false)
        where TCodec : class, IValueCodec<T>
    {
        this.services.TryAddSingleton<TCodec>();
        this.services.AddSingleton<IValueCodecRegistration>(services =>
            new ValueCodecRegistration<T>(services.GetRequiredService<TCodec>()));
        if (isDefault)
        {
            this.services.Replace(ServiceDescriptor.Singleton<IValueCodec<T>>(services => services.GetRequiredService<TCodec>()));
        }

        return this;
    }

    /// <summary>Registers a configured codec instance for one exact format and version.</summary>
    /// <typeparam name="T">The declared value contract.</typeparam>
    /// <param name="codec">The shared codec instance, which must support concurrent calls.</param>
    /// <param name="isDefault">Whether this codec also becomes the default writer resolved as <see cref="IValueCodec{T}"/>.</param>
    /// <returns>The current builder.</returns>
    /// <remarks>A later explicit default replaces the writer without removing readers.</remarks>
    /// <example><code>codecs.WithCodec(new SystemTextJsonValueCodec&lt;ProductCard&gt;(), isDefault: true);</code></example>
    public ValueCodecsBuilder WithCodec<T>(IValueCodec<T> codec, bool isDefault = false)
    {
        ArgumentNullException.ThrowIfNull(codec);
        this.services.AddSingleton<IValueCodecRegistration>(new ValueCodecRegistration<T>(codec));
        if (isDefault)
        {
            this.services.Replace(ServiceDescriptor.Singleton(codec));
        }

        return this;
    }
}