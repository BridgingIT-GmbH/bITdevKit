// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Infrastructure.LiteDb;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>Selects native event persistence independently of monitored file content providers.</summary>
public static class LiteDbFileMonitoringBuilderExtensions
{
    /// <summary>Replaces the file event store while preserving monitoring locations, scanning, and processors.</summary>
    /// <param name="context">The existing file monitoring builder context.</param>
    /// <param name="configure">Optional database, collection prefix, and materialization bounds.</param>
    /// <returns>The same builder context.</returns>
    /// <example><code>services.AddFileMonitoring(_ => { }).WithLiteDbStore();</code></example>
    public static FileMonitoringBuilderContext WithLiteDbStore(this FileMonitoringBuilderContext context, Action<LiteDbFeatureOptions> configure = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        var options = context.Services.RegisterLiteDbFeature("file_monitoring", ["events", "results", "presence", "state"], configure);
        if (options.MaxMaterializedRows < 3) { throw new ArgumentException("File monitoring requires room for an event, its presence, and sequence state."); }

        context.Services.AddSingleton(sp => new LiteDbFileEventStore(new LiteDbFeatureDatabase(sp.GetRequiredService<IServiceScopeFactory>(), options)));
        context.Services.RemoveAll<IFileEventStore>();
        context.Services.AddSingleton<IFileEventStore>(sp => sp.GetRequiredService<LiteDbFileEventStore>());
        return context;
    }
}
