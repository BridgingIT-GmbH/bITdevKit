// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>Registers trusted reusable codecs independently of application/storage features.</summary>
/// <example><code>services.AddValueCodecs(codecs => codecs.WithCodec&lt;ProductCard, SystemTextJsonValueCodec&lt;ProductCard&gt;&gt;());</code></example>
public static class ValueCodecServiceCollectionExtensions
{
    /// <summary>Configures trusted codec readers and registers their shared immutable registry.</summary>
    /// <param name="services">The host's service collection.</param>
    /// <param name="configure">The fluent codec registration callback.</param>
    /// <returns>The same service collection.</returns>
    /// <example><code>services.AddValueCodecs(codecs => codecs.WithCodec&lt;ProductCard, ProductCardCodec&gt;());</code></example>
    public static IServiceCollection AddValueCodecs(this IServiceCollection services, Action<ValueCodecsBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        services.TryAddSingleton<ValueCodecRegistry>();
        configure(new ValueCodecsBuilder(services));
        return services;
    }

    /// <summary>Adds an exact-format reader and the shared immutable reader registry.</summary>
    /// <typeparam name="T">The explicitly registered value contract.</typeparam>
    /// <param name="services">The host's service collection.</param>
    /// <param name="codec">The codec for a unique contract/format/version pair.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddValueCodec<T>(this IServiceCollection services, IValueCodec<T> codec)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(codec);
        return services.AddValueCodecs(codecs => codecs.WithCodec(codec));
    }
}