// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Typed resource conveniences using the shared naming convention.</summary>
/// <example><code>scope.AddResource&lt;Customer&gt;(customerId);</code></example>
public static class AuditScopeExtensions
{
    /// <summary>Applies conventions, optional object details, then registered typed providers inside the admission callback.</summary>
    /// <remarks>Absent/disabled/suppressed auditing invokes none of the detail providers.</remarks>
    /// <example><code>await using var scope = audit.BeginScopeFor(command, services);</code></example>
    public static IAuditScope BeginScopeFor<T>(this IAuditContext audit, T value, IServiceProvider services = null, Action<AuditScopeOptionsBuilder> configure = null)
        => audit?.BeginScope(builder =>
        {
            builder.For<T>();
            if (value is IAuditDetails described) builder.Describe(described.DescribeAudit());
            var providers = services?.GetService(typeof(IEnumerable<IAuditDetailsProvider<T>>)) as IEnumerable<IAuditDetailsProvider<T>>;
            foreach (var provider in (providers ?? []).Take(32)) builder.Describe(provider.DescribeAudit(value));
            configure?.Invoke(builder);
        }) ?? NoOpAuditScope.Instance;

    /// <summary>Adds a typed resource without projecting an entity object.</summary>
    /// <example><code>scope.AddResource&lt;Customer&gt;("C-123");</code></example>
    public static void AddResource<T>(this IAuditScope scope, object id, string displayName = null)
    {
        if (scope?.IsRecording != true) return;
        scope.AddResource(new(AuditNamingConvention.Default.GetResourceType(typeof(T)), AuditNamingConvention.Default.ConvertId(id), displayName, typeof(T)));
    }
}
