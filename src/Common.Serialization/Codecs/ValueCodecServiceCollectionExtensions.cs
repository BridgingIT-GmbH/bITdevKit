// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>Registers trusted reusable codecs independently of application/storage features.</summary>
/// <example><code>services.AddValueCodec(new SystemTextJsonValueCodec&lt;ProductCard&gt;());</code></example>
public static class ValueCodecServiceCollectionExtensions
{
    /// <summary>Adds an exact-format reader and the shared immutable reader registry.</summary>
    /// <typeparam name="T">The explicitly registered value contract.</typeparam>
    /// <param name="services">The host's service collection.</param>
    /// <param name="codec">The codec for a unique contract/format/version pair.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddValueCodec<T>(this IServiceCollection services, IValueCodec<T> codec)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(codec);
        services.AddSingleton<IValueCodecRegistration>(new ValueCodecRegistration<T>(codec));
        services.TryAddSingleton<ValueCodecRegistry>();
        return services;
    }
}