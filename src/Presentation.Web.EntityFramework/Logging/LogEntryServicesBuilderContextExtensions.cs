// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Infrastructure.EntityFramework;
using BridgingIT.DevKit.Presentation.Web;

/// <summary>
/// Provides builder context extensions for exposing the log entry endpoints.
/// </summary>
public static class LogEntryServicesBuilderContextExtensions
{
    /// <summary>
    /// Registers the <see cref="LogEntryEndpoints" /> for querying, streaming, purging, and exporting log entries.
    /// </summary>
    /// <param name="context">The log entry services builder context.</param>
    /// <param name="enabled">When <c>false</c>, the endpoints are not registered.</param>
    /// <returns>The same <paramref name="context" /> for chaining.</returns>
    /// <remarks>Nothing is registered when the log entry services themselves are disabled.</remarks>
    /// <example>
    /// <code>
    /// services.AddLogEntryServices&lt;CoreDbContext&gt;()
    ///     .AddEndpoints(builder.Environment.IsDevelopment());
    /// </code>
    /// </example>
    public static LogEntryServicesBuilderContext AddEndpoints(
        this LogEntryServicesBuilderContext context,
        bool enabled = true)
    {
        EnsureArg.IsNotNull(context, nameof(context));

        context.Services.AddEndpoints<LogEntryEndpoints>(enabled && context.Options.Enabled);

        return context;
    }
}
