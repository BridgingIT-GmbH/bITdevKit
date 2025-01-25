// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using BridgingIT.DevKit.Presentation.Web.IdentityProvider;
using Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add the fake identity provider middlewar to the service collection.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="configure"></param>
    /// <returns></returns>
    public static IServiceCollection AddFakeIdentityProvider(
        this IServiceCollection services,
        Action<FakeIdentityProviderEndpointsOptionsBuilder> configure)
    {
        var builder = new FakeIdentityProviderEndpointsOptionsBuilder();
        configure(builder); // Use the builder instead of directly configuring the options

        var options = builder.Build(); // Build the final options object

        if (!options.Enabled)
        {
            return services;
        }

        // Add middlware that redirects the request to the identity provider configuration
        // https://localhost:5001/.well-known/openid-configuratio ---> https://localhost:5001/api/_system/identity/connect/.well-known/openid-configuration

        // Register services for the identity provider endpoints
        services.AddSingleton(options);

        // Register the appropriate token service based on the provider
        services.AddSingleton<ITokenService>(sp =>
        {
            return options.TokenProvider switch
            {
                TokenProvider.EntraIdV2 => new EntraIdTokenService(options),
                TokenProvider.Adfs => new AdfsTokenService(options),
                TokenProvider.Keycloak => new KeyCloakTokenService(options),
                _ => new DefaultTokenService(options)
            };
        });

        services.AddSingleton<IUserInfoService>(sp =>
        {
            var options = sp.GetRequiredService<FakeIdentityProviderEndpointsOptions>();
            var tokenService = sp.GetRequiredService<ITokenService>();

            return options.TokenProvider switch
            {
                TokenProvider.EntraIdV2 => new EntraIdUserInfoService(tokenService, options),
                TokenProvider.Adfs => new AdfsUserInfoService(tokenService, options),
                TokenProvider.Keycloak => new KeyCloakUserInfoService(tokenService, options),
                _ => new DefaultUserInfoService(tokenService, options)
            };
        });

        services.AddSingleton<IFakeIdentityProvider, FakeIdentityProvider>();
        services.AddSingleton<IAuthorizationCodeService, AuthorizationCodeService>();
        services.AddSingleton<IPasswordValidator, PasswordValidator>();

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
        services.AddCors(corsOptions =>
        {
            corsOptions.AddPolicy(nameof(IdentityProvider), policy =>
            {
                policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
            });
        });

        // Register endpoints for the fake identity provider
        services.AddEndpoints<FakeIdentityProviderEndpoints>();

        return services;
    }
}
