// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation;
using BridgingIT.DevKit.Presentation.Web;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>Adds the standard Broadcasting HTTP transport and receiver.</summary>
/// <example><code>services.AddBroadcasting().WithHttpTransport();</code></example>
public static partial class ServiceCollectionExtensions
{
    /// <summary>Registers Broadcasting inspection and probe console commands.</summary>
    /// <param name="context">The shared Broadcasting builder.</param>
    /// <param name="enabled">Whether the console commands should be registered.</param>
    /// <returns>The shared Broadcasting builder.</returns>
    /// <example>
    /// <code>
    /// services.AddBroadcasting()
    ///     .AddConsoleCommands();
    /// </code>
    /// </example>
    public static BroadcastingBuilderContext AddConsoleCommands(
        this BroadcastingBuilderContext context,
        bool enabled = true
    )
    {
        ArgumentNullException.ThrowIfNull(context);

        if (enabled)
        {
            context.Services.TryAddEnumerable(
                ServiceDescriptor.Transient<IConsoleCommand, BroadcastingListConsoleCommand>()
            );
            context.Services.TryAddEnumerable(
                ServiceDescriptor.Transient<IConsoleCommand, BroadcastingProbeConsoleCommand>()
            );
        }

        return context;
    }

    /// <summary>Selects and configures the shared HTTP transport.</summary>
    /// <example>
    /// <code>
    /// services.AddBroadcasting()
    ///     .WithHttpTransport(options => options.SharedSecret(configuration["Broadcasting:SharedSecret"]));
    /// </code>
    /// </example>
    public static BroadcastingBuilderContext WithHttpTransport(
        this BroadcastingBuilderContext context,
        Action<BroadcastingHttpOptionsBuilder> configure = null
    )
    {
        ArgumentNullException.ThrowIfNull(context);

        var options =
            context
                .Services.FirstOrDefault(x => x.ServiceType == typeof(BroadcastingHttpOptions))
                ?.ImplementationInstance as BroadcastingHttpOptions
            ?? new BroadcastingHttpOptions();
        configure?.Invoke(new BroadcastingHttpOptionsBuilder(options));
        options.AuthenticationType ??= typeof(AllowAllBroadcastHttpAuthentication);

        context.UseTransport(typeof(HttpBroadcastTransport));
        context.Services.TryAddSingleton(options);
        context.Services.AddHttpClient(HttpBroadcastTransport.ClientName);
        context.Services.TryAddSingleton<ConfiguredBroadcastNodeAddressResolver>();
        context.Services.TryAddSingleton<KestrelBroadcastNodeAddressResolver>();
        context.Services.TryAddSingleton<
            IBroadcastNodeAddressResolver,
            BroadcastNodeAddressResolverChain
        >();

        if (options.AuthenticationType == typeof(SharedSecretBroadcastHttpAuthentication))
        {
            context.Services.RemoveAll<IBroadcastHttpAuthentication>();
            context.Services.TryAddSingleton<
                IBroadcastHttpAuthentication,
                SharedSecretBroadcastHttpAuthentication
            >();
        }
        else
        {
            context.Services.TryAddSingleton<
                IBroadcastHttpAuthentication,
                AllowAllBroadcastHttpAuthentication
            >();
        }

        context.Services.TryAddSingleton<BroadcastingEndpointsOptions>();
        context.Services.AddEndpoints<BroadcastingEndpoints>();
        return context;
    }

    /// <summary>Adds an ordered custom resolver between explicit configuration and Kestrel discovery.</summary>
    /// <typeparam name="TResolver">The custom node-address resolver implementation.</typeparam>
    /// <param name="context">The shared Broadcasting builder.</param>
    /// <param name="order">The ascending custom-resolver order.</param>
    /// <example><code>services.AddBroadcasting().AddNodeAddressResolver&lt;ContainerAddressResolver&gt;(10);</code></example>
    public static BroadcastingBuilderContext AddNodeAddressResolver<TResolver>(
        this BroadcastingBuilderContext context,
        int order = 0
    )
        where TResolver : class, IBroadcastNodeAddressResolver
    {
        ArgumentNullException.ThrowIfNull(context);

        var existing = context
            .Services.Where(descriptor =>
                descriptor.ServiceType == typeof(BroadcastNodeAddressResolverRegistration)
            )
            .Select(descriptor =>
                descriptor.ImplementationInstance as BroadcastNodeAddressResolverRegistration
            )
            .FirstOrDefault(registration => registration?.ResolverType == typeof(TResolver));
        if (existing is not null && existing.Order != order)
        {
            throw new InvalidOperationException(
                $"Broadcast address resolver '{typeof(TResolver).FullName}' is already registered with order {existing.Order}."
            );
        }

        if (existing is null)
        {
            context.Services.AddSingleton(
                new BroadcastNodeAddressResolverRegistration(typeof(TResolver), order)
            );
        }

        context.Services.TryAddSingleton<TResolver>();
        return context;
    }

    /// <summary>Selects a custom dedicated HTTP authentication implementation.</summary>
    /// <typeparam name="TAuthentication">The custom transport authentication implementation.</typeparam>
    public static BroadcastingBuilderContext WithHttpAuthentication<TAuthentication>(
        this BroadcastingBuilderContext context
    )
        where TAuthentication : class, IBroadcastHttpAuthentication
    {
        ArgumentNullException.ThrowIfNull(context);
        var options =
            context
                .Services.FirstOrDefault(x => x.ServiceType == typeof(BroadcastingHttpOptions))
                ?.ImplementationInstance as BroadcastingHttpOptions
            ?? new BroadcastingHttpOptions();

        if (
            options.AuthenticationType is not null
            && options.AuthenticationType != typeof(AllowAllBroadcastHttpAuthentication)
            && options.AuthenticationType != typeof(TAuthentication)
        )
        {
            throw new InvalidOperationException(
                "A different explicit broadcast HTTP authentication is already selected."
            );
        }

        options.AuthenticationType = typeof(TAuthentication);
        context.Services.TryAddSingleton(options);
        context.Services.RemoveAll<IBroadcastHttpAuthentication>();
        context.Services.TryAddSingleton<IBroadcastHttpAuthentication, TAuthentication>();
        return context;
    }
}