// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.Orchestrations;
using BridgingIT.DevKit.Infrastructure.LiteDb;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>Selects LiteDB persistence for the existing orchestration runtime.</summary>
public static class LiteDbOrchestrationBuilderExtensions
{
    /// <summary>Registers all native orchestration stores without changing workflow execution or definitions.</summary>
    /// <param name="context">The existing orchestration builder.</param>
    /// <param name="configure">Optional named database, namespace, serializer, and row-limit settings.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>services.AddOrchestrations().WithLiteDb();</code></example>
    public static OrchestrationBuilderContext WithLiteDb(this OrchestrationBuilderContext context, Action<LiteDbFeatureOptions> configure = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        var options = context.Services.RegisterLiteDbFeature("orchestrations", ["instances", "history", "signals", "timers", "leases"], configure);
        context.Services.AddSingleton(sp => new LiteDbOrchestrationStorageProvider(new LiteDbFeatureDatabase(sp.GetRequiredService<IServiceScopeFactory>(), options),
            options.Serializer, sp.GetService<IOrchestrationClock>()));
        context.Services.Replace(ServiceDescriptor.Singleton<IOrchestrationStorageProvider>(sp => sp.GetRequiredService<LiteDbOrchestrationStorageProvider>()));
        context.Services.Replace(ServiceDescriptor.Singleton(sp => sp.GetRequiredService<IOrchestrationStorageProvider>().Instances));
        context.Services.Replace(ServiceDescriptor.Singleton(sp => sp.GetRequiredService<IOrchestrationStorageProvider>().Leases));
        context.Services.Replace(ServiceDescriptor.Singleton(sp => sp.GetRequiredService<IOrchestrationStorageProvider>().History));
        context.Services.Replace(ServiceDescriptor.Singleton(sp => sp.GetRequiredService<IOrchestrationStorageProvider>().Signals));
        context.Services.Replace(ServiceDescriptor.Singleton(sp => sp.GetRequiredService<IOrchestrationStorageProvider>().Timers));
        context.Services.Replace(ServiceDescriptor.Singleton(sp => sp.GetRequiredService<IOrchestrationStorageProvider>().Queries));
        context.Services.Replace(ServiceDescriptor.Singleton(sp => sp.GetRequiredService<IOrchestrationStorageProvider>().Administration));
        return context;
    }
}
