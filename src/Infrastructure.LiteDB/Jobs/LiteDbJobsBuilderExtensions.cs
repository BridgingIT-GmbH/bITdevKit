// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.Jobs;
using BridgingIT.DevKit.Infrastructure.LiteDb;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>Selects LiteDB persistence for the existing Jobs scheduler.</summary>
public static class LiteDbJobsBuilderExtensions
{
    /// <summary>Replaces the Jobs store provider while retaining code-owned definitions and scheduling.</summary>
    /// <param name="context">The existing Jobs builder.</param>
    /// <param name="configure">Optional named database, prefix, serializer, and row-bound settings.</param>
    /// <returns>The same builder for further configuration.</returns>
    /// <example><code>services.AddJobScheduler().WithLiteDb();</code></example>
    public static JobBuilderContext WithLiteDb(this JobBuilderContext context, Action<LiteDbFeatureOptions> configure = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        var options = context.Services.RegisterLiteDbFeature("jobs", ["job_runtime_states", "trigger_runtime_states", "occurrences", "occurrence_keys", "executions",
            "dependencies_by_dependent", "dependencies_by_prerequisite", "batches", "batch_membership", "leases", "history_by_occurrence", "history_by_batch", "accepted_events", "accepted_event_keys"], configure);
        context.Services.AddSingleton(sp => new LiteDbJobStoreProvider(new LiteDbFeatureDatabase(sp.GetRequiredService<IServiceScopeFactory>(), options),
            sp.GetService<TimeProvider>(), options.Serializer));
        context.Services.Replace(ServiceDescriptor.Singleton<IJobStoreProvider>(sp => sp.GetRequiredService<LiteDbJobStoreProvider>()));
        return context;
    }
}
