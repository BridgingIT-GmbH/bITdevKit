// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;
using System.Collections.Generic;
using BridgingIT.DevKit.Common;

public class FakeIdentityProviderEndpointsOptionsBuilder
{
    private readonly FakeIdentityProviderEndpointsOptions options;

    public FakeIdentityProviderEndpointsOptionsBuilder()
    {
        this.options = new FakeIdentityProviderEndpointsOptions();
    }

    public FakeIdentityProviderEndpointsOptionsBuilder Enabled(bool enabled = true)
    {
        this.options.Enabled = enabled;

        return this;
    }

    public FakeIdentityProviderEndpointsOptionsBuilder WithGroupPath(string path)
    {
        this.options.GroupPath = path;

        return this;
    }

    public FakeIdentityProviderEndpointsOptionsBuilder WithGroupTag(string tag)
    {
        this.options.GroupTag = tag;

        return this;
    }

    /// <summary>
    /// Set the issuer for the identity provider.
    /// </summary>
    /// <param name="issuer"></param>
    /// <returns></returns>
    public FakeIdentityProviderEndpointsOptionsBuilder WithIssuer(string issuer)
    {
        this.options.Issuer = issuer;

        return this;
    }

    /// <summary>
    /// Add a user to the identity provider.
    /// </summary>
    /// <param name="user"></param>
    /// <returns></returns>
    public FakeIdentityProviderEndpointsOptionsBuilder WithUser(FakeUser user)
    {
        var usersList = this.options.Users.ToList();
        if (!usersList.Any(u => u.Id == user.Id))
        {
            usersList.Add(user);
            this.options.Users = usersList.AsReadOnly();
        }

        return this;
    }

    /// <summary>
    /// Add multiple users to the identity provider.
    /// </summary>
    /// <param name="users"></param>
    /// <returns></returns>
    // Add multiple users only if they don't already exist by Id
    public FakeIdentityProviderEndpointsOptionsBuilder WithUsers(IEnumerable<FakeUser> users)
    {
        var usersList = this.options.Users.ToList();
        foreach (var user in users.SafeNull())
        {
            if (!usersList.Any(u => u.Id == user.Id))
            {
                usersList.Add(user);
            }
        }
        this.options.Users = usersList.AsReadOnly();

        return this;
    }

    /// <summary>
    /// Configure the endpoint paths for the identity provider.
    /// </summary>
    /// <param name="configurePaths"></param>
    /// <returns></returns>
    public FakeIdentityProviderEndpointsOptionsBuilder WithPaths(Action<IdentityProviderEndpointPathsBuilder> configurePaths)
    {
        var pathsBuilder = new IdentityProviderEndpointPathsBuilder();
        configurePaths(pathsBuilder);
        this.options.EndpointPaths = pathsBuilder.Build();

        return this;
    }

    /// <summary>
    /// Add a client to the identity provider.
    /// </summary>
    /// <param name="client"></param>
    /// <returns></returns>
    public FakeIdentityProviderEndpointsOptionsBuilder WithClient(FakeIdentityProviderClient client)
    {
        var clientsList = this.options.Clients.ToList();
        if (!clientsList.Any(c => c.ClientId == client.ClientId))
        {
            clientsList.Add(client);
            this.options.Clients = clientsList.AsReadOnly();
        }

        return this;
    }

    /// <summary>
    /// Add a client to the identity provider.
    /// </summary>
    /// <param name="name"></param>
    /// <param name="clientId"></param>
    /// <param name="redirectUris"></param>
    /// <returns></returns>
    public FakeIdentityProviderEndpointsOptionsBuilder WithClient(
        string name,
        string clientId,
        params string[] redirectUris)
    {
        return this.WithClient(new FakeIdentityProviderClient
        {
            Name = name,
            ClientId = clientId,
            RedirectUris = redirectUris.ToList().AsReadOnly()
        });
    }

    /// <summary>
    /// Add a confidential client to the identity provider.
    /// </summary>
    /// <param name="name"></param>
    /// <param name="clientId"></param>
    /// <param name="clientSecret"></param>
    /// <param name="redirectUris"></param>
    /// <returns></returns>
    public FakeIdentityProviderEndpointsOptionsBuilder WithConfidentalClient(
        string name,
        string clientId,
        string clientSecret,
        params string[] redirectUris)
    {
        return this.WithClient(new FakeIdentityProviderClient
        {
            Name = name,
            ClientId = clientId,
            ClientSecret = clientSecret,
            RedirectUris = redirectUris.ToList().AsReadOnly(),
            IsConfidentialClient = true
        });
    }

    /// <summary>
    /// Use the EntraId V2 provider for the identity provider.
    /// </summary>
    /// <param name="tenantId"></param>
    /// <returns></returns>
    public FakeIdentityProviderEndpointsOptionsBuilder UseEntraIdV2Provider(string tenantId = null)
    {
        this.options.TokenProvider = TokenProvider.EntraIdV2;
        this.options.TenantId = tenantId ?? "test-tenant";

        return this;
    }

    /// <summary>
    /// Use the ADFS provider for the identity provider.
    /// </summary>
    /// <param name="clientId"></param>
    /// <returns></returns>
    public FakeIdentityProviderEndpointsOptionsBuilder UseAdfsProvider(string clientId = null)
    {
        this.options.TokenProvider = TokenProvider.Adfs;
        this.options.ClientId = clientId;
        return this;
    }

    /// <summary>
    /// Use the Keycloak provider for the identity provider.
    /// </summary>
    /// <param name="realmName"></param>
    /// <returns></returns>
    public FakeIdentityProviderEndpointsOptionsBuilder UseKeyCloakProvider(string realmName = null)
    {
        this.options.TokenProvider = TokenProvider.Keycloak;
        this.options.RealmName = realmName;

        return this;
    }

    /// <summary>
    /// Use the default provider for the identity provider.
    /// </summary>
    /// <returns></returns>
    public FakeIdentityProviderEndpointsOptionsBuilder UseDefaultProvider()
    {
        this.options.TokenProvider = TokenProvider.Default;

        return this;
    }

    public FakeIdentityProviderEndpointsOptionsBuilder WithClientId(string clientId)
    {
        this.options.ClientId = clientId;

        return this;
    }

    /// <summary>
    /// Set the token lifetimes for the identity provider.
    /// </summary>
    /// <param name="accessToken"></param>
    /// <param name="refreshToken"></param>
    /// <returns></returns>
    public FakeIdentityProviderEndpointsOptionsBuilder WithTokenLifetimes(TimeSpan accessToken, TimeSpan refreshToken)
    {
        this.options.AccessTokenLifetime = accessToken;
        this.options.RefreshTokenLifetime = refreshToken;

        return this;
    }

    /// <summary>
    /// Enable persistent refresh tokens for the identity provider.
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public FakeIdentityProviderEndpointsOptionsBuilder EnablePersistentRefreshTokens(bool value = true)
    {
        this.options.EnablePersistentRefreshTokens = value;

        return this;
    }

    /// <summary>
    /// Set the signing key for the identity provider.
    /// </summary>
    /// <param name="signingKey"></param>
    /// <returns></returns>
    public FakeIdentityProviderEndpointsOptionsBuilder WithSigningKey(string signingKey)
    {
        this.options.SigningKey = signingKey;

        return this;
    }

    /// <summary>
    /// Enable user cards in the sign in UI for the identity provider.
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public FakeIdentityProviderEndpointsOptionsBuilder EnableUserCards(bool value = true)
    {
        this.options.EnableUserCards = value;

        return this;
    }

    /// <summary>
    /// Enable the login card in the sign in UI for the identity provider.
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public FakeIdentityProviderEndpointsOptionsBuilder EnableLoginCard(bool value = true)
    {
        this.options.EnableLoginCard = value;

        return this;
    }

    /// <summary>
    /// Build the identity provider options.
    /// </summary>
    /// <returns></returns>
    public FakeIdentityProviderEndpointsOptions Build()
    {
        return this.options;
    }
}
