// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

/// <summary>
///     Provides a base implementation for modular Minimal API endpoint sets.
/// </summary>
/// <remarks>
///     Derived classes implement <see cref="Map" /> to declare their routes. <see cref="MapGroup" /> creates a configured
///     route group from <see cref="EndpointsOptionsBase" /> and applies shared endpoint metadata such as OpenAPI tags,
///     authorization, CORS, rate limiting, route name prefix metadata, and custom metadata. All configuration is applied
///     before the derived endpoint class maps individual routes into the returned group.
///
///     Example:
///     <code>
///     public sealed class OrdersEndpoints(OrdersEndpointsOptions options) : EndpointsBase
///     {
///         public override void Map(IEndpointRouteBuilder app)
///         {
///             var group = MapGroup(app, options);
///
///             group.MapGet("", () =&gt; Results.Ok())
///                 .WithName(options, "List");
///         }
///     }
///     </code>
/// </remarks>
public abstract class EndpointsBase : IEndpoints
{
    /// <summary>
    ///     Gets or sets whether this endpoint set should be mapped.
    /// </summary>
    /// <remarks>
    ///     The endpoint mapping extension skips endpoint instances whose value is <c>false</c>. The default value is
    ///     <c>true</c>.
    /// </remarks>
    public bool Enabled { get; set; } = true;

    /// <summary>
    ///     Gets or sets whether this endpoint set has already been mapped.
    /// </summary>
    /// <remarks>
    ///     The endpoint mapping extension sets this value after calling <see cref="Map" /> to avoid duplicate mapping when
    ///     endpoint registration is invoked repeatedly for the same instance.
    /// </remarks>
    public bool IsRegistered { get; set; }

    /// <summary>
    ///     Maps this endpoint set into the specified route builder.
    /// </summary>
    /// <param name="app">The endpoint route builder that receives the route mappings.</param>
    /// <remarks>
    ///     Derived classes typically call <see cref="MapGroup" /> with their endpoint options and then map individual
    ///     endpoints into the returned <see cref="RouteGroupBuilder" />.
    /// </remarks>
    public abstract void Map(IEndpointRouteBuilder app);

    /// <summary>
    ///     Creates a route group and applies the shared endpoint group configuration from the supplied options.
    /// </summary>
    /// <param name="app">The endpoint route builder that owns the new group.</param>
    /// <param name="options">The endpoint group options to apply.</param>
    /// <returns>A configured route group builder that callers can use to map individual endpoints.</returns>
    /// <remarks>
    ///     The method validates <paramref name="app" /> and <paramref name="options" /> before mapping. It creates the group
    ///     with <see cref="EndpointsOptionsBase.GroupPath" />, optionally normalizes the path, applies tags, OpenAPI group
    ///     metadata, description visibility, authorization or anonymous metadata, CORS metadata, rate limiting metadata,
    ///     route name prefix metadata, and custom metadata. Authorization role entries and authentication schemes that are
    ///     blank are ignored. Anonymous access takes precedence over authorization metadata.
    ///
    ///     Example:
    ///     <code>
    ///     var group = MapGroup(app, options);
    ///     group.MapGet("items", GetItems)
    ///         .WithName(BuildRouteName(options, "GetItems"));
    ///     </code>
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="app" /> or <paramref name="options" /> is <c>null</c>.
    /// </exception>
    public RouteGroupBuilder MapGroup(IEndpointRouteBuilder app, EndpointsOptionsBase options)
    {
        EnsureArg.IsNotNull(app, nameof(app));
        EnsureArg.IsNotNull(options, nameof(options));

        var group = app.MapGroup(GetGroupPath(options))
            .WithTags(GetGroupTags(options));

        ConfigureOpenApi(group, options);
        ConfigureDescription(group, options);
        ConfigureAuthorization(group, options);
        ConfigureCors(group, options);
        ConfigureRateLimiting(group, options);
        ConfigureRouteNamePrefix(group, options);
        ConfigureMetadata(group, options);

        return group;
    }

    /// <summary>
    ///     Builds an endpoint route name using the optional prefix configured on the endpoint options.
    /// </summary>
    /// <param name="options">The endpoint options that may contain a route name prefix.</param>
    /// <param name="name">The endpoint-specific route name.</param>
    /// <returns>
    ///     The unmodified <paramref name="name" /> when no prefix is configured; the prefix when
    ///     <paramref name="name" /> is empty; otherwise the prefix and name joined by a single period.
    /// </returns>
    /// <remarks>
    ///     ASP.NET Core does not automatically prepend endpoint names from route group metadata. Derived endpoint classes
    ///     should call this helper before passing names to <c>WithName</c>. The helper trims trailing periods from the
    ///     prefix and leading periods from the endpoint-specific name.
    ///
    ///     Example:
    ///     <code>
    ///     options.RouteNamePrefix = "Orders";
    ///     var name = BuildRouteName(options, "List"); // Orders.List
    ///     </code>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options" /> is <c>null</c>.</exception>
    public static string BuildRouteName(EndpointsOptionsBase options, string name)
    {
        EnsureArg.IsNotNull(options, nameof(options));

        if (options.RouteNamePrefix.IsNullOrEmpty())
        {
            return name;
        }

        if (name.IsNullOrEmpty())
        {
            return options.RouteNamePrefix;
        }

        return $"{options.RouteNamePrefix.TrimEnd('.')}.{name.TrimStart('.')}";
    }

    private static string GetGroupPath(EndpointsOptionsBase options)
    {
        return options.NormalizeGroupPath
            ? NormalizeGroupPath(options.GroupPath)
            : options.GroupPath;
    }

    private static string NormalizeGroupPath(string path)
    {
        if (path.IsNullOrEmpty())
        {
            return "/";
        }

        var segments = path.Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return segments.Length == 0
            ? "/"
            : $"/{string.Join('/', segments)}";
    }

    private static string[] GetGroupTags(EndpointsOptionsBase options)
    {
        return [options.GroupTag, .. options.GroupTags.SafeNull()];
    }

    private static void ConfigureOpenApi(RouteGroupBuilder group, EndpointsOptionsBase options)
    {
        if (!options.GroupName.IsNullOrEmpty())
        {
            group.WithGroupName(options.GroupName);
        }

        if (!options.Summary.IsNullOrEmpty())
        {
            group.WithSummary(options.Summary);
        }

        if (!options.Description.IsNullOrEmpty())
        {
            group.WithDescription(options.Description);
        }

        if (options.Deprecated)
        {
            group.WithMetadata(new ObsoleteAttribute());
        }
    }

    private static void ConfigureDescription(RouteGroupBuilder group, EndpointsOptionsBase options)
    {
        if (options.ExcludeFromDescription)
        {
            group.ExcludeFromDescription();
        }
    }

    private static void ConfigureAuthorization(RouteGroupBuilder group, EndpointsOptionsBase options)
    {
        if (options.AllowAnonymous)
        {
            group.AllowAnonymous();

            return;
        }

        if (!options.RequireAuthorization)
        {
            return;
        }

        var roles = GetRequiredRoles(options);
        var schemes = GetRequiredAuthenticationSchemes(options);
        if (roles.SafeAny())
        {
            var attribute = new AuthorizeAttribute
            {
                Roles = string.Join(",", roles)
            };

            ConfigureAuthenticationSchemes(attribute, schemes);
            group.RequireAuthorization(attribute);

            return;
        }

        if (!options.RequirePolicy.IsNullOrEmpty())
        {
            var attribute = new AuthorizeAttribute(options.RequirePolicy);

            ConfigureAuthenticationSchemes(attribute, schemes);
            group.RequireAuthorization(attribute);

            return;
        }

        if (schemes.SafeAny())
        {
            group.RequireAuthorization(
                new AuthorizeAttribute
                {
                    AuthenticationSchemes = string.Join(",", schemes)
                });

            return;
        }

        group.RequireAuthorization();
    }

    private static string[] GetRequiredRoles(EndpointsOptionsBase options)
    {
        return options.RequireRoles
            .SafeNull()
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .ToArray();
    }

    private static string[] GetRequiredAuthenticationSchemes(EndpointsOptionsBase options)
    {
        return options.RequireAuthenticationSchemes
            .SafeNull()
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToArray();
    }

    private static void ConfigureAuthenticationSchemes(AuthorizeAttribute attribute, string[] schemes)
    {
        if (schemes.SafeAny())
        {
            attribute.AuthenticationSchemes = string.Join(",", schemes);
        }
    }

    private static void ConfigureCors(RouteGroupBuilder group, EndpointsOptionsBase options)
    {
        if (options.DisableCors)
        {
            group.WithMetadata(new DisableCorsAttribute());

            return;
        }

        if (!options.RequireCorsPolicy.IsNullOrEmpty())
        {
            group.RequireCors(options.RequireCorsPolicy);
        }
    }

    private static void ConfigureRateLimiting(RouteGroupBuilder group, EndpointsOptionsBase options)
    {
        if (options.DisableRateLimiting)
        {
            group.DisableRateLimiting();

            return;
        }

        if (!options.RequireRateLimitingPolicy.IsNullOrEmpty())
        {
            group.RequireRateLimiting(options.RequireRateLimitingPolicy);
        }
    }

    private static void ConfigureRouteNamePrefix(RouteGroupBuilder group, EndpointsOptionsBase options)
    {
        if (!options.RouteNamePrefix.IsNullOrEmpty())
        {
            group.WithMetadata(new EndpointRouteNamePrefixMetadata(options.RouteNamePrefix));
        }
    }

    private static void ConfigureMetadata(RouteGroupBuilder group, EndpointsOptionsBase options)
    {
        foreach (var metadata in options.Metadata.SafeNull().Where(metadata => metadata is not null))
        {
            group.WithMetadata(metadata);
        }
    }
}
