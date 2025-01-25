// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using System;
using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Authentication;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds and configures fake authentication for testing scenarios.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="users">The predefined users.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <example>
    /// Basic setup with predefined users:
    /// <code>
    /// builder.Services.AddFakeAuthentication(Fakes.Users);
    /// </code>
    /// </example>
    public static IServiceCollection AddFakeAuthentication(
        this IServiceCollection services,
        IEnumerable<FakeUser> users, bool enabled = true)
    {
        if (!enabled)
        {
            return services;
        }

        return services.AddFakeAuthentication(o => o.WithUsers(users));
    }

    /// <summary>
    /// Adds and configures fake authentication for testing scenarios.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Action to configure the fake authentication options.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <example>
    /// Basic setup with predefined users:
    /// <code>
    /// builder.Services.AddFakeAuthentication(options => options
    ///     .WithUsers(Fakes.Users));
    /// </code>
    ///
    /// Advanced setup with custom users and claims:
    /// <code>
    /// builder.Services.AddFakeAuthentication(o => o
    ///     .WithUsers(Fakes.Users)
    ///     .AddUser(
    ///         "extra@example.com",
    ///         "Extra User",
    ///         new[] { "User" })
    ///     .AddClaim("tenant", "test")
    ///     .WithAdditionalClaims(
    ///         ("culture", "en-US"),
    ///         ("theme", "dark")));
    /// </code>
    /// </example>
    public static IServiceCollection AddFakeAuthentication(
        this IServiceCollection services,
        Action<FakeAuthenticationOptionsBuilder> configure,
        bool enabled = true)
    {
        if (!enabled)
        {
            return services;
        }

        var builder = new FakeAuthenticationOptionsBuilder();
        configure?.Invoke(builder);
        var options = builder.Build();

        if (!options.Enabled)
        {
            return services;
        }

        // Register options
        services.AddSingleton(options);

        // Configure authentication
        services.AddAuthentication(o =>
        {
            o.DefaultAuthenticateScheme = FakeAuthenticationHandler.SchemeName;
            o.DefaultScheme = FakeAuthenticationHandler.SchemeName;
        })
        .AddScheme<AuthenticationSchemeOptions, FakeAuthenticationHandler>(
            FakeAuthenticationHandler.SchemeName,
            null);

        return services;
    }
}