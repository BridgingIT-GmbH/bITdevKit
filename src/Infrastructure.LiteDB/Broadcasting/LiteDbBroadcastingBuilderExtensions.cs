// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Infrastructure.LiteDb;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

/// <summary>Selects native local-scope Broadcasting registry persistence.</summary>
public static class LiteDbBroadcastingBuilderExtensions
{
    /// <summary>Replaces the implicit registry while preserving the selected transport and Broadcasting runtime.</summary>
    /// <param name="context">The existing Broadcasting builder.</param>
    /// <param name="configure">Optional native database, namespace, and operation bounds.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>services.AddBroadcasting().WithLiteDbRegistry();</code></example>
    public static BroadcastingBuilderContext WithLiteDbRegistry(this BroadcastingBuilderContext context, Action<LiteDbFeatureOptions> configure = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        var options = context.Services.RegisterLiteDbFeature("broadcasting", ["nodes"], configure);
        context.UseRegistryProvider(typeof(LiteDbBroadcastRegistryStore));
        context.Services.Replace(ServiceDescriptor.Singleton(sp => new LiteDbBroadcastRegistryStore(new(sp.GetRequiredService<IServiceScopeFactory>(), options),
            context.Options, sp.GetService<TimeProvider>() ?? TimeProvider.System, sp.GetService<IMetricsService>(), sp.GetService<ILogger<LiteDbBroadcastRegistryStore>>())));
        context.Services.Replace(ServiceDescriptor.Singleton<IBroadcastRegistryStore>(sp => sp.GetRequiredService<LiteDbBroadcastRegistryStore>()));
        return context;
    }
}
