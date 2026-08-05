// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Resolves an explicitly configured process address before other address sources.</summary>
/// <param name="options">The shared HTTP transport configuration.</param>
/// <example><code>var address = await resolver.ResolveAsync(cancellationToken);</code></example>
public sealed class ConfiguredBroadcastNodeAddressResolver(BroadcastingHttpOptions options)
    : IBroadcastNodeAddressResolver
{
    /// <inheritdoc />
    public ValueTask<Uri> ResolveAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(AppendRoute(options.AdvertisedAddress, options.ReceiverRoute));
    }

    /// <summary>Validates a concrete HTTP address and appends the receiver route exactly once.</summary>
    public static Uri AppendRoute(Uri address, string route)
    {
        if (address is null)
        {
            return null;
        }

        if (
            address.Scheme is not ("http" or "https")
            || address.Host is "0.0.0.0" or "::" or "[::]" or "*" or "+"
            || !string.IsNullOrEmpty(address.UserInfo)
        )
        {
            throw new InvalidOperationException(
                "A broadcast address must identify one HTTP or HTTPS process."
            );
        }

        var normalizedRoute = $"/{route.Trim('/')}";
        if (
            address
                .AbsolutePath.TrimEnd('/')
                .EndsWith(normalizedRoute, StringComparison.OrdinalIgnoreCase)
        )
        {
            if (address.AbsoluteUri.Length > 2048)
            {
                throw new InvalidOperationException(
                    "A broadcast receiver address cannot exceed 2048 characters."
                );
            }

            return address;
        }

        var receiverAddress = new UriBuilder(address)
        {
            Path = $"{address.AbsolutePath.TrimEnd('/')}{normalizedRoute}",
        }.Uri;
        if (receiverAddress.AbsoluteUri.Length > 2048)
        {
            throw new InvalidOperationException(
                "A broadcast receiver address cannot exceed 2048 characters."
            );
        }

        return receiverAddress;
    }
}

/// <summary>Resolves the first concrete HTTP address exposed by the running Kestrel server.</summary>
/// <param name="serviceProvider">The host service provider.</param>
/// <param name="options">The shared HTTP transport configuration.</param>
/// <example><code>var address = await resolver.ResolveAsync(cancellationToken);</code></example>
public sealed class KestrelBroadcastNodeAddressResolver(
    IServiceProvider serviceProvider,
    BroadcastingHttpOptions options
) : IBroadcastNodeAddressResolver
{
    /// <inheritdoc />
    public ValueTask<Uri> ResolveAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var server = serviceProvider.GetService<IServer>();
        var addresses = server?.Features.Get<IServerAddressesFeature>()?.Addresses;
        var address = addresses
            ?.Select(value => Uri.TryCreate(value, UriKind.Absolute, out var uri) ? uri : null)
            .FirstOrDefault(uri =>
                uri is not null
                && uri.Scheme is "http" or "https"
                && uri.Host is not ("0.0.0.0" or "::" or "[::]" or "*" or "+")
            );
        return ValueTask.FromResult(
            ConfiguredBroadcastNodeAddressResolver.AppendRoute(address, options.ReceiverRoute)
        );
    }
}

/// <summary>Describes an ordered custom node-address resolver registration.</summary>
/// <param name="ResolverType">The registered resolver implementation type.</param>
/// <param name="Order">The ascending resolver order.</param>
/// <example><code>var registration = new BroadcastNodeAddressResolverRegistration(typeof(MyResolver), 10);</code></example>
public sealed record BroadcastNodeAddressResolverRegistration(Type ResolverType, int Order);

/// <summary>Resolves addresses using explicit, ordered custom, and Kestrel sources.</summary>
/// <param name="configuredResolver">The explicit-address resolver.</param>
/// <param name="kestrelResolver">The Kestrel fallback resolver.</param>
/// <param name="registrations">The ordered custom resolver registrations.</param>
/// <param name="serviceProvider">The service provider used to resolve custom implementations.</param>
/// <param name="options">The shared HTTP transport configuration.</param>
/// <example><code>var address = await chain.ResolveAsync(cancellationToken);</code></example>
public sealed class BroadcastNodeAddressResolverChain(
    ConfiguredBroadcastNodeAddressResolver configuredResolver,
    KestrelBroadcastNodeAddressResolver kestrelResolver,
    IEnumerable<BroadcastNodeAddressResolverRegistration> registrations,
    IServiceProvider serviceProvider,
    BroadcastingHttpOptions options
) : IBroadcastNodeAddressResolver
{
    /// <inheritdoc />
    public async ValueTask<Uri> ResolveAsync(CancellationToken cancellationToken = default)
    {
        var address = await configuredResolver
            .ResolveAsync(cancellationToken)
            .ConfigureAwait(false);
        if (address is not null)
        {
            return address;
        }

        foreach (
            var registration in registrations
                .OrderBy(item => item.Order)
                .ThenBy(item => item.ResolverType.FullName, StringComparer.Ordinal)
        )
        {
            var resolver = (IBroadcastNodeAddressResolver)
                serviceProvider.GetRequiredService(registration.ResolverType);
            address = await resolver.ResolveAsync(cancellationToken).ConfigureAwait(false);
            if (address is not null)
            {
                return ConfiguredBroadcastNodeAddressResolver.AppendRoute(
                    address,
                    options.ReceiverRoute
                );
            }
        }

        return await kestrelResolver.ResolveAsync(cancellationToken).ConfigureAwait(false);
    }
}
