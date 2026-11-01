// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.Auditing;
using BridgingIT.DevKit.Presentation.Web.Auditing;
using Microsoft.AspNetCore.Builder;

/// <summary>Registers and installs explicitly selected HTTP audit capture.</summary>
/// <example><code>services.AddAuditing().WithHttpRequests(); app.UseAuditing();</code></example>
public static class AuditingHttpExtensions
{
    /// <summary>Copies HTTP options without enabling any other source behavior.</summary>
    /// <example><code>services.AddAuditing().WithHttpRequests(options => options.PathBlackListPatterns = []);</code></example>
    public static AuditingBuilder WithHttpRequests(this AuditingBuilder builder, Action<AuditHttpOptions> configure = null)
    {
        if (builder.Services.Any(descriptor => descriptor.ServiceType == typeof(AuditHttpOptions)))
        {
            if (configure is not null) throw new InvalidOperationException("HTTP auditing is already configured.");
            return builder;
        }

        var options = new AuditHttpOptions();
        configure?.Invoke(options);
        if (options.IsRead is null || options.PathBlackListPatterns?.Length > 256 || options.PathBlackListPatterns?.Any(pattern => pattern?.Length > 2_048) == true) throw new ArgumentException("HTTP audit options exceed supported bounds.");
        builder.Services.AddSingleton(new AuditHttpOptions { PathBlackListPatterns = options.PathBlackListPatterns?.ToArray(), IsRead = options.IsRead, SelectBusinessRead = options.SelectBusinessRead, IncludeAuditIdHeader = options.IncludeAuditIdHeader });
        return builder;
    }

    /// <summary>Installs capture after routing/authentication and before exception handling/authorization.</summary>
    /// <example><code>app.UseAuditing(); app.UseExceptionHandler(); app.UseAuthorization();</code></example>
    public static IApplicationBuilder UseAuditing(this IApplicationBuilder app)
    {
        if (app.ApplicationServices.GetService<AuditHttpOptions>() is null) throw new InvalidOperationException("Select WithHttpRequests before installing UseAuditing.");
        return app.UseMiddleware<AuditHttpMiddleware>();
    }
}
