// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Add the dashboard endpoints to the service collection with the specified configuration.
    /// </summary>
    public static IServiceCollection AddDashboard(
        this IServiceCollection services,
        Action<DashboardEndpointsOptionsBuilder> configure)
    {
        var builder = new DashboardEndpointsOptionsBuilder();
        configure(builder); // Use the builder instead of directly configuring the options

        var options = builder.Build(); // Build the final options object
        if (!options.Enabled)
        {
            return services;
        }

        // Register services for the dashboard endpoints
        services.AddSingleton(options);
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, DashboardAuthorizationMiddlewareResultHandler>();
        services.AddSignalR();
        services.AddDashboardAuthentication(options);

        // Register the page generator
        //if (options.PageGenerator != null)
        //{
        //    services.AddSingleton(options.PageGenerator);
        //}
        //else
        //{
        //    services.AddSingleton<IPageGenerator, PageGenerator>();
        //}

        // Register CORS policies
        // services.AddCors(corsOptions =>
        // {
        //     corsOptions.AddPolicy(nameof(BridgingIT.DevKit.Presentation.Web.Dashboard), policy =>
        //     {
        //         policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        //     });
        // });

        services.AddDashboardPlugins(options);

        return services;
    }

    private static IServiceCollection AddDashboardAuthentication(this IServiceCollection services, DashboardEndpointsOptions options)
    {
        if (options.Authentication.Kind is not DashboardAuthenticationRegistrationKind.OpenIdConnect)
        {
            return services;
        }

        services.AddAuthentication()
            .AddCookie(options.Authentication.CookieScheme, cookieOptions =>
            {
                cookieOptions.Cookie.Name = ".Bdk.Dashboard";
                cookieOptions.Cookie.HttpOnly = true;
                cookieOptions.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                cookieOptions.Cookie.SameSite = SameSiteMode.Lax;
                cookieOptions.ExpireTimeSpan = TimeSpan.FromHours(12);
                cookieOptions.AccessDeniedPath = DashboardPath.Combine(options.GroupPath, options.EndpointPaths.AccessDenied);
                options.Authentication.ConfigureCookie?.Invoke(cookieOptions);
            })
            .AddPolicyScheme(options.Authentication.Scheme, "BDK Dashboard", policyOptions =>
            {
                policyOptions.ForwardAuthenticate = options.Authentication.CookieScheme;
                policyOptions.ForwardChallenge = options.Authentication.OpenIdConnectScheme;
                policyOptions.ForwardForbid = options.Authentication.CookieScheme;
                policyOptions.ForwardSignIn = options.Authentication.CookieScheme;
                policyOptions.ForwardSignOut = options.Authentication.CookieScheme;
            })
            .AddOpenIdConnect(options.Authentication.OpenIdConnectScheme, openIdConnectOptions =>
            {
                openIdConnectOptions.ClientId = DashboardAuthenticationDefaults.ClientId;
                openIdConnectOptions.ResponseType = "code";
                openIdConnectOptions.ResponseMode = "query";
                openIdConnectOptions.CallbackPath = DashboardPath.Combine(options.GroupPath, "/signin-oidc");
                openIdConnectOptions.SignedOutCallbackPath = DashboardPath.Combine(options.GroupPath, "/signout-callback-oidc");
                openIdConnectOptions.SignInScheme = options.Authentication.CookieScheme;
                openIdConnectOptions.SaveTokens = true;
                openIdConnectOptions.GetClaimsFromUserInfoEndpoint = true;

                openIdConnectOptions.Scope.Clear();
                openIdConnectOptions.Scope.Add("openid");
                openIdConnectOptions.Scope.Add("profile");
                openIdConnectOptions.Scope.Add("email");
                openIdConnectOptions.Scope.Add("roles");

                openIdConnectOptions.TokenValidationParameters = new TokenValidationParameters
                {
                    NameClaimType = ClaimTypes.Name,
                    RoleClaimType = ClaimTypes.Role
                };

                options.Authentication.ConfigureOpenIdConnect?.Invoke(openIdConnectOptions);
                ConfigureDashboardOpenIdConnectEvents(openIdConnectOptions);
            });

        return services;
    }

    private static void ConfigureDashboardOpenIdConnectEvents(OpenIdConnectOptions options)
    {
        options.Events ??= new OpenIdConnectEvents();

        var onTokenValidated = options.Events.OnTokenValidated;
        options.Events.OnTokenValidated = async context =>
        {
            if (onTokenValidated is not null)
            {
                await onTokenValidated(context);
            }

            PromoteRoles(context.Principal);
        };

        var onUserInformationReceived = options.Events.OnUserInformationReceived;
        options.Events.OnUserInformationReceived = async context =>
        {
            if (onUserInformationReceived is not null)
            {
                await onUserInformationReceived(context);
            }

            PromoteRoles(context.Principal);
        };
    }

    private static void PromoteRoles(ClaimsPrincipal principal)
    {
        if (principal?.Identity is not ClaimsIdentity identity)
        {
            return;
        }

        var roleClaims = identity.FindAll("roles").ToArray();
        foreach (var claim in roleClaims)
        {
            foreach (var role in ReadRoles(claim.Value))
            {
                if (!identity.HasClaim(ClaimTypes.Role, role))
                {
                    identity.AddClaim(new Claim(ClaimTypes.Role, role));
                }
            }
        }
    }

    private static IEnumerable<string> ReadRoles(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            yield break;
        }

        if (value.TrimStart().StartsWith("[", StringComparison.Ordinal))
        {
            foreach (var role in JsonSerializer.Deserialize<string[]>(value) ?? [])
            {
                if (!string.IsNullOrWhiteSpace(role))
                {
                    yield return role.Trim();
                }
            }

            yield break;
        }

        yield return value.Trim();
    }

    private static IServiceCollection AddDashboardPlugins(this IServiceCollection services, DashboardEndpointsOptions options)
    {
        var assemblies = GetDashboardPluginAssemblies(options)
            .Distinct()
            .ToArray();

        services.AddEndpoints(assemblies
            .SelectMany(assembly => assembly.SafeGetTypes<IDashboardEndpoints>())
            .Where(type => type.IsClass && !type.IsAbstract)
            .Distinct());

        var navigationDescriptors = assemblies
            .SelectMany(assembly => assembly.SafeGetTypes<IDashboardNavigationProvider>())
            .Where(type => type.IsClass && !type.IsAbstract)
            .Select(type => ServiceDescriptor.Singleton(typeof(IDashboardNavigationProvider), type))
            .ToArray();

        services.TryAddEnumerable(navigationDescriptors);

        var pageProviderDescriptors = assemblies
            .SelectMany(assembly => assembly.SafeGetTypes<IDashboardPageProvider>())
            .Where(type => type.IsClass && !type.IsAbstract)
            .Select(type => ServiceDescriptor.Singleton(typeof(IDashboardPageProvider), type))
            .ToArray();

        services.TryAddEnumerable(pageProviderDescriptors);

        return services;
    }

    private static IEnumerable<Assembly> GetDashboardPluginAssemblies(DashboardEndpointsOptions options)
    {
        yield return typeof(DashboardEndpoints).Assembly;

        foreach (var assembly in options.PluginAssemblies)
        {
            if (assembly is not null)
            {
                yield return assembly;
            }
        }

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.SafeGetTypes<IDashboardEndpoints>().Any() ||
                assembly.SafeGetTypes<IDashboardNavigationProvider>().Any() ||
                assembly.SafeGetTypes<IDashboardPageProvider>().Any())
            {
                yield return assembly;
            }
        }
    }
}
