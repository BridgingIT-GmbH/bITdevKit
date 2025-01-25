// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Presentation.Web;

public static class IdentityOptionsBuilderExtensions
{
    /// <summary>
    /// Enables or disables endpoints for evaluating entity permissions.
    /// </summary>
    /// <param name="source">The identity options builder.</param>
    /// <returns>The identity options builder for chaining.</returns>
    /// <example>
    /// Configuring evaluation endpoints:
    /// <code>
    /// services.AddIdentity(identity =>
    /// {
    ///     identity
    ///         // Configure entity permissions
    ///         .EnableEvaluationEndpoints()  // Enables /api/_system/identity/entities/permissions endpoints
    /// });
    /// </code>
    /// </example>
    public static IdentityOptionsBuilder EnableEvaluationEndpoints(
        this IdentityOptionsBuilder source)
    {
        source.Services.AddEndpoints<IdentityEntityPermissionEvaluationEndpoints>();

        return source;
    }

    /// <summary>
    /// Enables or disables endpoints for evaluating entity permissions.
    /// </summary>
    /// <param name="source">The identity options builder.</param>
    /// <param name="enabled">Whether to enable the endpoints (default: true).</param>
    /// <returns>The identity options builder for chaining.</returns>
    /// <example>
    /// Configuring evaluation endpoints:
    /// <code>
    /// services.AddIdentity(identity =>
    /// {
    ///     identity
    ///         // Configure entity permissions
    ///         .EnableEvaluationEndpoints()  // Enables /api/_system/identity/entities/permissions endpoints
    /// });
    /// </code>
    /// </example>
    public static IdentityOptionsBuilder EnableEvaluationEndpoints(
        this IdentityOptionsBuilder source,
        bool enabled)
    {
        source.Services.AddEndpoints<IdentityEntityPermissionEvaluationEndpoints>(enabled);

        return source;
    }

    /// <summary>
    /// Enables and configures endpoints for evaluating entity permissions.
    /// </summary>
    /// <param name="source">The identity options builder.</param>
    /// <param name="configure">Action to configure the evaluation endpoints options.</param>
    /// <returns>The identity options builder for chaining.</returns>
    /// <example>
    /// Configuring evaluation endpoints in Program.cs:
    /// <code>
    /// services.AddIdentity(identity =>
    /// {
    ///     identity.EnableEvaluationEndpoints(options =>
    ///     {
    ///         options.GroupPrefix = "/api/custom/permissions";
    ///         options.RequireAuthorization = true;
    ///         options.BypassCache = true;
    ///     });
    /// });
    /// </code>
    /// </example>
    public static IdentityOptionsBuilder EnableEvaluationEndpoints(
       this IdentityOptionsBuilder source,
       Action<IdentityEntityPermissionEvaluationEndpointsOptions> configure = null)
    {
        var options = new IdentityEntityPermissionEvaluationEndpointsOptions();
        configure?.Invoke(options);

        source.Services.AddSingleton(options);
        source.Services.AddEndpoints<IdentityEntityPermissionEvaluationEndpoints>(options.Enabled);

        return source;
    }

    /// <summary>
    /// Enables or disables endpoints for managing entity permissions.
    /// </summary>
    /// <param name="source">The identity options builder.</param>
    /// <returns>The identity options builder for chaining.</returns>
    /// <example>
    /// Configuring management endpoints:
    /// <code>
    /// services.AddIdentity(identity =>
    /// {
    ///     identity
    ///         // Configure entity permissions
    ///         .EnableManagementEndpoints()  // Enables /api/_system/identity/management/entities/permissions endpoints
    /// });
    /// </code>
    /// </example>
    public static IdentityOptionsBuilder EnableManagementEndpoints(
        this IdentityOptionsBuilder source)
    {
        source.Services.AddEndpoints<IdentityEntityPermissionManagementEndpoints>();

        return source;
    }

    /// <summary>
    /// Enables or disables endpoints for managing entity permissions.
    /// </summary>
    /// <param name="source">The identity options builder.</param>
    /// <param name="enabled">Whether to enable the endpoints (default: true).</param>
    /// <returns>The identity options builder for chaining.</returns>
    /// <example>
    /// Configuring management endpoints:
    /// <code>
    /// services.AddIdentity(identity =>
    /// {
    ///     identity
    ///         // Configure entity permissions
    ///         .EnableManagementEndpoints()  // Enables /api/_system/identity/management/entities/permissions endpoints
    /// });
    /// </code>
    /// </example>
    public static IdentityOptionsBuilder EnableManagementEndpoints(
        this IdentityOptionsBuilder source,
        bool enabled)
    {
        source.Services.AddEndpoints<IdentityEntityPermissionManagementEndpoints>(enabled);

        return source;
    }

    /// <summary>
    /// Enables and configures endpoints for managing entity permissions.
    /// </summary>
    /// <param name="source">The identity options builder.</param>
    /// <param name="configure">Action to configure the management endpoints options.</param>
    /// <returns>The identity options builder for chaining.</returns>
    /// <example>
    /// Configuring management endpoints in Program.cs:
    /// <code>
    /// services.AddIdentity(identity =>
    /// {
    ///     identity.EnableManagementEndpoints(options =>
    ///     {
    ///         options.RequiredRoles = ["Admin", "SystemManager"];
    ///         options.GroupPrefix = "/api/custom/permissions/manage";
    ///     });
    /// });
    /// </code>
    /// </example>
    public static IdentityOptionsBuilder EnableManagementEndpoints(
       this IdentityOptionsBuilder source,
       Action<IdentityEntityPermissionManagementEndpointsOptions> configure = null)
    {
        var options = new IdentityEntityPermissionManagementEndpointsOptions();
        configure?.Invoke(options);

        source.Services.AddSingleton(options);
        source.Services.AddEndpoints<IdentityEntityPermissionManagementEndpoints>(options.Enabled);

        return source;
    }
}