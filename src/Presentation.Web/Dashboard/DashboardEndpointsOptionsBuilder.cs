// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Dashboard;

using System.Reflection;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

/// <summary>
///     Builds <see cref="DashboardEndpointsOptions" /> for dashboard endpoint registration.
/// </summary>
/// <example>
/// <code>
/// builder.Services.AddDashboard(options => options
///     .Enabled()
///     .WithTitle("Operations")
///     .WithPluginAssemblyContaining&lt;OperationsDashboard&gt;());
/// </code>
/// </example>
public class DashboardEndpointsOptionsBuilder
{
    private readonly DashboardEndpointsOptions options;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DashboardEndpointsOptionsBuilder" /> class.
    /// </summary>
    /// <example>
    /// <code>
    /// var builder = new DashboardEndpointsOptionsBuilder();
    /// </code>
    /// </example>
    public DashboardEndpointsOptionsBuilder()
    {
        this.options = new DashboardEndpointsOptions();
    }

    /// <summary>
    ///     Enables or disables dashboard endpoint registration.
    /// </summary>
    /// <param name="enabled">A value indicating whether dashboard endpoints should be enabled.</param>
    /// <returns>The same builder instance.</returns>
    /// <example>
    /// <code>
    /// options.Enabled(builder.Environment.IsDevelopment());
    /// </code>
    /// </example>
    public DashboardEndpointsOptionsBuilder Enabled(bool enabled = true)
    {
        this.options.Enabled = enabled;

        return this;
    }

    /// <summary>
    ///     Sets the dashboard endpoint group path.
    /// </summary>
    /// <param name="path">The dashboard base path, for example <c>/_bdk/dashboard</c>.</param>
    /// <returns>The same builder instance.</returns>
    /// <example>
    /// <code>
    /// options.WithGroupPath("/admin/dashboard");
    /// </code>
    /// </example>
    public DashboardEndpointsOptionsBuilder WithGroupPath(string path)
    {
        this.options.GroupPath = path;

        return this;
    }

    /// <summary>
    ///     Sets the primary OpenAPI tag used for the dashboard endpoint group.
    /// </summary>
    /// <param name="tag">The endpoint group tag.</param>
    /// <returns>The same builder instance.</returns>
    /// <example>
    /// <code>
    /// options.WithGroupTag("_bdk.Dashboard");
    /// </code>
    /// </example>
    public DashboardEndpointsOptionsBuilder WithGroupTag(string tag)
    {
        this.options.GroupTag = tag;

        return this;
    }

    /// <summary>
    ///     Sets the title shown by the dashboard shell.
    /// </summary>
    /// <param name="title">The dashboard title. Blank values fall back to <c>BDK Dashboard</c>.</param>
    /// <returns>The same builder instance.</returns>
    /// <example>
    /// <code>
    /// options.WithTitle("Operations Dashboard");
    /// </code>
    /// </example>
    public DashboardEndpointsOptionsBuilder WithTitle(string title)
    {
        this.options.Title = string.IsNullOrWhiteSpace(title) ? "BDK Dashboard" : title.Trim();

        return this;
    }

    /// <summary>
    ///     Hides dashboard pages by stable page key.
    /// </summary>
    /// <param name="pageKeys">The page keys to hide, optionally followed by a final <see cref="bool" /> that enables or disables this operation.</param>
    /// <returns>The same builder instance.</returns>
    /// <remarks>
    ///     When the final argument is <c>false</c>, the supplied keys are removed from the disabled set. This supports
    ///     environment-specific configuration such as <c>DisablePages("metrics", !env.IsDevelopment())</c>.
    /// </remarks>
    /// <example>
    /// <code>
    /// options.DisablePages("metrics", "storage.documents", !builder.Environment.IsDevelopment());
    /// </code>
    /// </example>
    public DashboardEndpointsOptionsBuilder DisablePages(params object[] pageKeys)
    {
        if (pageKeys is null || pageKeys.Length == 0)
        {
            return this;
        }

        var disabled = true;
        var length = pageKeys.Length;
        if (pageKeys[^1] is bool value)
        {
            disabled = value;
            length--;
        }

        for (var i = 0; i < length; i++)
        {
            if (pageKeys[i] is not string key)
            {
                throw new ArgumentException("Dashboard page keys must be strings, with an optional boolean as the last argument.", nameof(pageKeys));
            }

            key = key?.Trim();
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            if (disabled)
            {
                this.options.DisabledPageKeys.Add(key);
            }
            else
            {
                this.options.DisabledPageKeys.Remove(key);
            }
        }

        return this;
    }

    /// <summary>
    ///     Configures dashboard authorization and marks the dashboard as requiring authorization.
    /// </summary>
    /// <param name="configure">An optional delegate for configuring dashboard authorization requirements.</param>
    /// <returns>The same builder instance.</returns>
    /// <example>
    /// <code>
    /// options.Authorize(authorization => authorization
    ///     .Auto()
    ///     .AuthenticationScheme("Dashboard")
    ///     .RequireRole(Role.Administrators));
    /// </code>
    /// </example>
    public DashboardEndpointsOptionsBuilder Authorize(Action<DashboardAuthorizationOptionsBuilder> configure = null)
    {
        this.options.AuthorizationMode = DashboardAuthorizationMode.RequireAuthenticated;
        this.options.RequireAuthorization = true;
        this.options.AllowAnonymous = false;

        var builder = new DashboardAuthorizationOptionsBuilder(this.options);
        configure?.Invoke(builder);

        return this;
    }

    /// <summary>
    ///     Explicitly allows anonymous access to dashboard routes.
    /// </summary>
    /// <returns>The same builder instance.</returns>
    /// <example>
    /// <code>
    /// options.AllowAnonymous();
    /// </code>
    /// </example>
    public DashboardEndpointsOptionsBuilder AllowAnonymous()
    {
        this.options.AuthorizationMode = DashboardAuthorizationMode.Anonymous;
        this.options.AllowAnonymous = true;
        this.options.RequireAuthorization = false;

        return this;
    }

    /// <summary>
    /// Adds an assembly that contains dashboard plugin endpoints.
    /// </summary>
    /// <param name="assembly">The assembly to scan for <see cref="IDashboardEndpoints" /> implementations.</param>
    /// <returns>The same builder instance.</returns>
    /// <example>
    /// <code>
    /// options.WithPluginAssembly(typeof(MyDashboardPlugin).Assembly);
    /// </code>
    /// </example>
    public DashboardEndpointsOptionsBuilder WithPluginAssembly(Assembly assembly)
    {
        if (assembly is not null && !this.options.PluginAssemblies.Contains(assembly))
        {
            this.options.PluginAssemblies.Add(assembly);
        }

        return this;
    }

    /// <summary>
    /// Adds assemblies that contain dashboard plugin endpoints.
    /// </summary>
    /// <param name="assemblies">The assemblies to scan for <see cref="IDashboardEndpoints" /> implementations.</param>
    /// <returns>The same builder instance.</returns>
    /// <example>
    /// <code>
    /// options.WithPluginAssemblies(
    ///     typeof(CatalogDashboard).Assembly,
    ///     typeof(OrdersDashboard).Assembly);
    /// </code>
    /// </example>
    public DashboardEndpointsOptionsBuilder WithPluginAssemblies(params Assembly[] assemblies)
    {
        foreach (var assembly in assemblies ?? [])
        {
            this.WithPluginAssembly(assembly);
        }

        return this;
    }

    /// <summary>
    /// Adds the assembly containing <typeparamref name="T" /> as a dashboard plugin assembly.
    /// </summary>
    /// <typeparam name="T">A marker type from the plugin assembly.</typeparam>
    /// <returns>The same builder instance.</returns>
    /// <example>
    /// <code>
    /// options.WithPluginAssemblyContaining&lt;CatalogDashboard&gt;();
    /// </code>
    /// </example>
    public DashboardEndpointsOptionsBuilder WithPluginAssemblyContaining<T>()
    {
        return this.WithPluginAssembly(typeof(T).Assembly);
    }

    /// <summary>
    ///     Builds the configured dashboard endpoint options.
    /// </summary>
    /// <returns>The configured <see cref="DashboardEndpointsOptions" /> instance.</returns>
    /// <example>
    /// <code>
    /// var dashboardOptions = new DashboardEndpointsOptionsBuilder()
    ///     .WithTitle("Operations")
    ///     .Build();
    /// </code>
    /// </example>
    public DashboardEndpointsOptions Build()
    {
        return this.options;
    }
}

/// <summary>
///     Builds dashboard authorization settings.
/// </summary>
/// <param name="options">The dashboard options instance to configure.</param>
/// <example>
/// <code>
/// options.Authorize(authorization => authorization
///     .AuthenticationScheme("Dashboard")
///     .RequireRole(Role.Administrators));
/// </code>
/// </example>
public sealed class DashboardAuthorizationOptionsBuilder(DashboardEndpointsOptions options)
{
    /// <summary>
    ///     Requires dashboard authorization only when the host application has registered authentication schemes.
    /// </summary>
    /// <returns>The same builder instance.</returns>
    /// <example>
    /// <code>
    /// authorization.Auto();
    /// </code>
    /// </example>
    public DashboardAuthorizationOptionsBuilder Auto()
    {
        options.AuthorizationMode = DashboardAuthorizationMode.Auto;
        options.RequireAuthorization = false;
        options.AllowAnonymous = false;

        return this;
    }

    /// <summary>
    ///     Always requires an authenticated principal for dashboard routes.
    /// </summary>
    /// <returns>The same builder instance.</returns>
    /// <example>
    /// <code>
    /// authorization.RequireAuthenticated();
    /// </code>
    /// </example>
    public DashboardAuthorizationOptionsBuilder RequireAuthenticated()
    {
        options.AuthorizationMode = DashboardAuthorizationMode.RequireAuthenticated;
        options.RequireAuthorization = true;
        options.AllowAnonymous = false;

        return this;
    }

    /// <summary>
    ///     Explicitly allows anonymous access to dashboard routes.
    /// </summary>
    /// <returns>The same builder instance.</returns>
    /// <example>
    /// <code>
    /// authorization.Anonymous();
    /// </code>
    /// </example>
    public DashboardAuthorizationOptionsBuilder Anonymous()
    {
        options.AuthorizationMode = DashboardAuthorizationMode.Anonymous;
        options.RequireAuthorization = false;
        options.AllowAnonymous = true;

        return this;
    }

    /// <summary>
    ///     Requires at least one of the specified roles for dashboard routes.
    /// </summary>
    /// <param name="roles">The role names to require.</param>
    /// <returns>The same builder instance.</returns>
    /// <example>
    /// <code>
    /// authorization.RequireRole(Role.Administrators);
    /// </code>
    /// </example>
    public DashboardAuthorizationOptionsBuilder RequireRole(params string[] roles)
    {
        options.RequireRoles = roles ?? [];

        return this;
    }

    /// <summary>
    ///     Requires the specified authorization policy for dashboard routes.
    /// </summary>
    /// <param name="policy">The policy name to require.</param>
    /// <returns>The same builder instance.</returns>
    /// <example>
    /// <code>
    /// authorization.RequirePolicy("DashboardAccess");
    /// </code>
    /// </example>
    public DashboardAuthorizationOptionsBuilder RequirePolicy(string policy)
    {
        options.RequirePolicy = policy;

        return this;
    }

    /// <summary>
    ///     Sets the authentication schemes used by dashboard authorization metadata.
    /// </summary>
    /// <param name="schemes">The authentication scheme names.</param>
    /// <returns>The same builder instance.</returns>
    /// <example>
    /// <code>
    /// authorization.AuthenticationScheme("Dashboard");
    /// </code>
    /// </example>
    public DashboardAuthorizationOptionsBuilder AuthenticationScheme(params string[] schemes)
    {
        options.RequireAuthenticationSchemes = schemes ?? [];

        return this;
    }

    /// <summary>
    ///     Sets the authentication schemes used when the dashboard sign-out endpoint is invoked.
    /// </summary>
    /// <param name="schemes">The authentication scheme names to sign out.</param>
    /// <returns>The same builder instance.</returns>
    /// <example>
    /// <code>
    /// authorization.SignOutAuthenticationScheme("Dashboard");
    /// </code>
    /// </example>
    public DashboardAuthorizationOptionsBuilder SignOutAuthenticationScheme(params string[] schemes)
    {
        options.SignOutAuthenticationSchemes = schemes ?? [];

        return this;
    }

    /// <summary>
    ///     Reuses an authentication scheme that is already registered by the host application.
    /// </summary>
    /// <param name="scheme">The existing authentication scheme name.</param>
    /// <param name="signOut">A value indicating whether dashboard sign-out should also sign out of the scheme.</param>
    /// <returns>The same builder instance.</returns>
    /// <example>
    /// <code>
    /// authorization.UseExistingScheme("ApplicationCookie", signOut: true);
    /// </code>
    /// </example>
    public DashboardAuthorizationOptionsBuilder UseExistingScheme(string scheme, bool signOut = false)
    {
        scheme = string.IsNullOrWhiteSpace(scheme) ? null : scheme.Trim();
        if (string.IsNullOrWhiteSpace(scheme))
        {
            return this;
        }

        options.Authentication.Kind = DashboardAuthenticationRegistrationKind.ExistingScheme;
        options.Authentication.Scheme = scheme;
        options.Authentication.SignOutEnabled = signOut;
        options.AuthorizationMode = DashboardAuthorizationMode.RequireAuthenticated;
        options.RequireAuthorization = true;
        options.AllowAnonymous = false;
        options.RequireAuthenticationSchemes = [scheme];
        options.SignOutAuthenticationSchemes = signOut ? [scheme] : [];

        return this;
    }

    /// <summary>
    ///     Reuses an existing cookie authentication scheme registered by the host application.
    /// </summary>
    /// <param name="scheme">The existing cookie authentication scheme name.</param>
    /// <param name="signOut">A value indicating whether dashboard sign-out should also sign out of the application cookie.</param>
    /// <returns>The same builder instance.</returns>
    /// <example>
    /// <code>
    /// authorization.UseCookie(CookieAuthenticationDefaults.AuthenticationScheme, signOut: true);
    /// </code>
    /// </example>
    public DashboardAuthorizationOptionsBuilder UseCookie(
        string scheme = CookieAuthenticationDefaults.AuthenticationScheme,
        bool signOut = true)
    {
        return this.UseExistingScheme(scheme, signOut);
    }

    /// <summary>
    ///     Registers dashboard-owned cookie and OpenID Connect schemes using convention-based OpenID Connect defaults.
    /// </summary>
    /// <param name="authority">The OpenID Connect authority, for example <c>https://idp.example</c>.</param>
    /// <param name="configure">Configures dashboard OpenID Connect conventions before they are applied to the handler.</param>
    /// <param name="configureCookie">Configures the dashboard cookie handler.</param>
    /// <returns>The same builder instance.</returns>
    /// <example>
    /// <code>
    /// authorization.UseOpenIdConnect(
    ///     "https://idp.example",
    ///     oidc => oidc.WithClientId("dashboard"));
    /// </code>
    /// </example>
    public DashboardAuthorizationOptionsBuilder UseOpenIdConnect(
        string authority,
        Action<DashboardOpenIdConnectOptionsBuilder> configure = null,
        Action<CookieAuthenticationOptions> configureCookie = null)
    {
        return this.UseOpenIdConnect(authority, DashboardAuthenticationDefaults.ClientId, configure, configureCookie);
    }

    /// <summary>
    ///     Registers dashboard-owned cookie and OpenID Connect schemes using convention-based OpenID Connect defaults.
    /// </summary>
    /// <param name="authority">The OpenID Connect authority, for example <c>https://idp.example</c>.</param>
    /// <param name="clientId">The OpenID Connect client id. Defaults to <c>dashboard</c> when blank.</param>
    /// <param name="configure">Configures dashboard OpenID Connect conventions before they are applied to the handler.</param>
    /// <param name="configureCookie">Configures the dashboard cookie handler.</param>
    /// <returns>The same builder instance.</returns>
    /// <example>
    /// <code>
    /// authorization.UseOpenIdConnect(
    ///     "https://idp.example",
    ///     "dashboard",
    ///     oidc => oidc.RequireHttpsMetadata());
    /// </code>
    /// </example>
    public DashboardAuthorizationOptionsBuilder UseOpenIdConnect(
        string authority,
        string clientId,
        Action<DashboardOpenIdConnectOptionsBuilder> configure = null,
        Action<CookieAuthenticationOptions> configureCookie = null)
    {
        var builder = new DashboardOpenIdConnectOptionsBuilder(authority, clientId);
        configure?.Invoke(builder);

        return this.UseOpenIdConnect(builder.Apply, configureCookie);
    }

    /// <summary>
    ///     Registers dashboard-owned cookie and OpenID Connect schemes for interactive dashboard sign-in.
    /// </summary>
    /// <param name="configureOpenIdConnect">Configures the dashboard OpenID Connect handler.</param>
    /// <param name="configureCookie">Configures the dashboard cookie handler.</param>
    /// <returns>The same builder instance.</returns>
    /// <example>
    /// <code>
    /// authorization.UseOpenIdConnect(options =>
    /// {
    ///     options.Authority = "https://idp.example";
    ///     options.ClientId = "dashboard";
    /// });
    /// </code>
    /// </example>
    public DashboardAuthorizationOptionsBuilder UseOpenIdConnect(
        Action<OpenIdConnectOptions> configureOpenIdConnect,
        Action<CookieAuthenticationOptions> configureCookie = null)
    {
        options.Authentication.Kind = DashboardAuthenticationRegistrationKind.OpenIdConnect;
        options.Authentication.Scheme = DashboardAuthenticationDefaults.AuthenticationScheme;
        options.Authentication.CookieScheme = DashboardAuthenticationDefaults.CookieScheme;
        options.Authentication.OpenIdConnectScheme = DashboardAuthenticationDefaults.OpenIdConnectScheme;
        options.Authentication.ConfigureOpenIdConnect = configureOpenIdConnect;
        options.Authentication.ConfigureCookie = configureCookie;
        options.Authentication.SignOutEnabled = true;
        options.AuthorizationMode = DashboardAuthorizationMode.RequireAuthenticated;
        options.RequireAuthorization = true;
        options.AllowAnonymous = false;
        options.RequireAuthenticationSchemes = [options.Authentication.Scheme];
        options.SignOutAuthenticationSchemes =
        [
            options.Authentication.CookieScheme,
            options.Authentication.OpenIdConnectScheme
        ];

        return this;
    }
}

/// <summary>
///     Builds convention-based dashboard OpenID Connect handler settings.
/// </summary>
/// <param name="authority">The OpenID Connect authority.</param>
/// <param name="clientId">The OpenID Connect client id.</param>
/// <example>
/// <code>
/// authorization.UseOpenIdConnect("https://idp.example");
/// </code>
/// </example>
public sealed class DashboardOpenIdConnectOptionsBuilder(string authority, string clientId)
{
    private string authority = NormalizeAuthority(authority);
    private string clientId = string.IsNullOrWhiteSpace(clientId)
        ? DashboardAuthenticationDefaults.ClientId
        : clientId.Trim();
    private string metadataAddress;
    private bool requireHttpsMetadata = true;
    private bool validateIssuer = true;
    private bool validateAudience = true;
    private bool validateLifetime = true;
    private bool validateIssuerSigningKey;
    private bool requireSignedTokens;
    private Action<OpenIdConnectOptions> configure;

    /// <summary>
    ///     Sets the OpenID Connect authority.
    /// </summary>
    /// <param name="value">The authority, for example <c>https://idp.example</c>.</param>
    /// <returns>The same builder instance.</returns>
    /// <example>
    /// <code>
    /// oidc.WithAuthority("https://idp.example");
    /// </code>
    /// </example>
    public DashboardOpenIdConnectOptionsBuilder WithAuthority(string value)
    {
        this.authority = NormalizeAuthority(value);

        return this;
    }

    /// <summary>
    ///     Sets the OpenID Connect client id.
    /// </summary>
    /// <param name="value">The client id. Blank values fall back to <c>dashboard</c>.</param>
    /// <returns>The same builder instance.</returns>
    /// <example>
    /// <code>
    /// oidc.WithClientId("dashboard");
    /// </code>
    /// </example>
    public DashboardOpenIdConnectOptionsBuilder WithClientId(string value)
    {
        this.clientId = string.IsNullOrWhiteSpace(value)
            ? DashboardAuthenticationDefaults.ClientId
            : value.Trim();

        return this;
    }

    /// <summary>
    ///     Sets the OpenID Connect metadata address.
    /// </summary>
    /// <param name="value">The metadata address. Blank values use <c>{authority}/.well-known/openid-configuration</c>.</param>
    /// <returns>The same builder instance.</returns>
    /// <example>
    /// <code>
    /// oidc.WithMetadataAddress("https://idp.example/.well-known/openid-configuration");
    /// </code>
    /// </example>
    public DashboardOpenIdConnectOptionsBuilder WithMetadataAddress(string value)
    {
        this.metadataAddress = string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        return this;
    }

    /// <summary>
    ///     Configures whether the metadata endpoint must use HTTPS.
    /// </summary>
    /// <param name="value">A value indicating whether HTTPS metadata is required.</param>
    /// <returns>The same builder instance.</returns>
    /// <example>
    /// <code>
    /// oidc.RequireHttpsMetadata(false);
    /// </code>
    /// </example>
    public DashboardOpenIdConnectOptionsBuilder RequireHttpsMetadata(bool value = true)
    {
        this.requireHttpsMetadata = value;

        return this;
    }

    /// <summary>
    ///     Configures issuer validation for dashboard tokens.
    /// </summary>
    /// <param name="value">A value indicating whether issuer validation is enabled.</param>
    /// <returns>The same builder instance.</returns>
    /// <example>
    /// <code>
    /// oidc.ValidateIssuer();
    /// </code>
    /// </example>
    public DashboardOpenIdConnectOptionsBuilder ValidateIssuer(bool value = true)
    {
        this.validateIssuer = value;

        return this;
    }

    /// <summary>
    ///     Configures audience validation for dashboard tokens.
    /// </summary>
    /// <param name="value">A value indicating whether audience validation is enabled.</param>
    /// <returns>The same builder instance.</returns>
    /// <example>
    /// <code>
    /// oidc.ValidateAudience();
    /// </code>
    /// </example>
    public DashboardOpenIdConnectOptionsBuilder ValidateAudience(bool value = true)
    {
        this.validateAudience = value;

        return this;
    }

    /// <summary>
    ///     Configures lifetime validation for dashboard tokens.
    /// </summary>
    /// <param name="value">A value indicating whether lifetime validation is enabled.</param>
    /// <returns>The same builder instance.</returns>
    /// <example>
    /// <code>
    /// oidc.ValidateLifetime();
    /// </code>
    /// </example>
    public DashboardOpenIdConnectOptionsBuilder ValidateLifetime(bool value = true)
    {
        this.validateLifetime = value;

        return this;
    }

    /// <summary>
    ///     Allows unsigned tokens for identity providers that do not emit signed ID tokens.
    /// </summary>
    /// <returns>The same builder instance.</returns>
    /// <example>
    /// <code>
    /// oidc.AllowUnsignedTokens();
    /// </code>
    /// </example>
    public DashboardOpenIdConnectOptionsBuilder AllowUnsignedTokens()
    {
        this.requireSignedTokens = false;
        this.validateIssuerSigningKey = false;

        return this;
    }

    /// <summary>
    ///     Requires signed tokens and issuer signing key validation.
    /// </summary>
    /// <returns>The same builder instance.</returns>
    /// <example>
    /// <code>
    /// oidc.RequireSignedTokens();
    /// </code>
    /// </example>
    public DashboardOpenIdConnectOptionsBuilder RequireSignedTokens()
    {
        this.requireSignedTokens = true;
        this.validateIssuerSigningKey = true;

        return this;
    }

    /// <summary>
    ///     Applies additional low-level <see cref="OpenIdConnectOptions" /> configuration after dashboard conventions.
    /// </summary>
    /// <param name="configure">The low-level configuration delegate.</param>
    /// <returns>The same builder instance.</returns>
    /// <example>
    /// <code>
    /// oidc.Configure(options => options.ResponseMode = "query");
    /// </code>
    /// </example>
    public DashboardOpenIdConnectOptionsBuilder Configure(Action<OpenIdConnectOptions> configure)
    {
        this.configure += configure;

        return this;
    }

    /// <summary>
    ///     Applies the configured dashboard OpenID Connect conventions to the specified options instance.
    /// </summary>
    /// <param name="options">The OpenID Connect options to configure.</param>
    /// <example>
    /// <code>
    /// oidc.Apply(options);
    /// </code>
    /// </example>
    public void Apply(OpenIdConnectOptions options)
    {
        var normalizedAuthority = string.IsNullOrWhiteSpace(this.authority)
            ? options.Authority?.TrimEnd('/')
            : this.authority;
        var normalizedClientId = string.IsNullOrWhiteSpace(this.clientId)
            ? DashboardAuthenticationDefaults.ClientId
            : this.clientId;

        options.Authority = normalizedAuthority;
        options.MetadataAddress = this.metadataAddress ??
            (string.IsNullOrWhiteSpace(normalizedAuthority)
                ? null
                : $"{normalizedAuthority}/.well-known/openid-configuration");
        options.ClientId = normalizedClientId;
        options.RequireHttpsMetadata = this.requireHttpsMetadata;
        options.TokenValidationParameters ??= new TokenValidationParameters();
        options.TokenValidationParameters.ValidIssuer = normalizedAuthority;
        options.TokenValidationParameters.ValidAudience = normalizedClientId;
        options.TokenValidationParameters.ValidateIssuer = this.validateIssuer;
        options.TokenValidationParameters.ValidateAudience = this.validateAudience;
        options.TokenValidationParameters.ValidateLifetime = this.validateLifetime;
        options.TokenValidationParameters.ValidateIssuerSigningKey = this.validateIssuerSigningKey;
        options.TokenValidationParameters.RequireSignedTokens = this.requireSignedTokens;

        this.configure?.Invoke(options);
    }

    private static string NormalizeAuthority(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim().TrimEnd('/');
    }
}
