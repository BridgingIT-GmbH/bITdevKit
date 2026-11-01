// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides the shared builder context for ChangeHistory registration.
/// </summary>
/// <example>
/// <code>
/// services.AddChangeHistory(options =&gt; options.Track&lt;Customer&gt;())
///     .WithReadAuthorizer&lt;AppDbContext, AppChangeHistoryReadAuthorizer&gt;();
/// </code>
/// </example>
public sealed class ChangeHistoryBuilderContext
{
    /// <summary>Creates a source-owned ChangeHistory configuration context.</summary>
    /// <example><code>var context = new ChangeHistoryBuilderContext(services, options);</code></example>
    public ChangeHistoryBuilderContext(IServiceCollection services, ChangeHistoryOptions options)
    {
        this.Services = services;
        this.Options = options;
        AuditBoundaryRegistrationCatalog.GetOrAdd(services, "ChangeHistory", "Bridge");
    }

    /// <summary>
    /// Gets the service collection used by the registration.
    /// </summary>
    public IServiceCollection Services { get; }

    /// <summary>
    /// Gets the configured ChangeHistory options.
    /// </summary>
    public ChangeHistoryOptions Options { get; }
}
