// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Auditing;

using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Http;

/// <summary>Selects HTTP business boundaries after the shared request-path blacklist.</summary>
/// <example><code>auditing.WithHttpRequests(options => options.IncludeAuditIdHeader = true);</code></example>
public sealed class AuditHttpOptions
{
    /// <summary>Replacement whole-path blacklist. Null or empty disables only path blacklisting.</summary>
    /// <example><code>options.PathBlackListPatterns = ["/health*"];</code></example>
    public string[] PathBlackListPatterns { get; set; } = RequestPathDefaults.Blacklist.ToArray();

    /// <summary>Explicit meaningful business read selector. Routine reads are omitted by default.</summary>
    /// <example><code>options.SelectBusinessRead = context => context.Request.Path == "/customers/statement";</code></example>
    public Func<HttpContext, bool> SelectBusinessRead { get; set; }

    /// <summary>Identifies read-only requests, including host-marked POST reads.</summary>
    /// <example><code>endpoint.WithMetadata(new AuditHttpReadMetadata());</code></example>
    public Func<HttpContext, bool> IsRead { get; set; } = context => HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method) || HttpMethods.IsOptions(context.Request.Method) || context.GetEndpoint()?.Metadata.GetMetadata<AuditHttpReadMetadata>() is not null;

    /// <summary>Whether selected responses expose their opaque X-Audit-Id before response start.</summary>
    /// <example><code>options.IncludeAuditIdHeader = true;</code></example>
    public bool IncludeAuditIdHeader { get; set; }
}

/// <summary>Marks a host-owned route as a read even when it uses POST.</summary>
/// <example><code>app.MapPost("/customers/search", Search).WithMetadata(new AuditHttpReadMetadata());</code></example>
public sealed class AuditHttpReadMetadata;
