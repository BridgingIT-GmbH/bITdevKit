// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;
using System.Collections.Generic;
using BridgingIT.DevKit.Common;

public class FakeIdentityProviderEndpointsOptions : EndpointsOptionsBase
{
    public FakeIdentityProviderEndpointsOptions()
    {
        this.Enabled = true;
        this.GroupPath = "/api/_system/identity/connect";
        this.GroupTag = "_system/identity/connect";
        this.RequireAuthorization = false;
        this.Issuer = "https://localhost:5001"; // should match Client Authority  "https://localhost:5001/api/_system/identity/connect"
        this.EndpointPaths = new FakeIdentityProviderEndpointPaths(); // Default endpoint paths
        this.AccessTokenLifetime = TimeSpan.FromMinutes(30);    // 30 minutes
        this.RefreshTokenLifetime = TimeSpan.FromDays(1);       // 24 hours
        this.SigningKey = string.Empty; // "your-256-bit-secret-your-256-bit-secret-your-256-bit-secret";
    }

    public IReadOnlyList<FakeUser> Users { get; set; } = [];

    public string Issuer { get; set; }

    public FakeIdentityProviderEndpointPaths EndpointPaths { get; set; }

    public IReadOnlyList<FakeIdentityProviderClient> Clients { get; set; } = [];

    public TimeSpan AccessTokenLifetime { get; set; }

    public TimeSpan RefreshTokenLifetime { get; set; }

    public bool EnablePersistentRefreshTokens { get; set; } = true;

    public string SigningKey { get; set; }

    public bool EnableUserCards { get; set; } = true;

    public bool EnableLoginCard { get; set; } = true;

    public TokenProvider TokenProvider { get; set; } = TokenProvider.Default;

    // Add provider-specific properties
    public string TenantId { get; set; }    // For Azure AD
    public string RealmName { get; set; }   // For Keycloak
    public string ClientId { get; set; }    // Common
}

public enum TokenProvider
{
    Default,
    EntraIdV2,
    Keycloak,
    Adfs
}