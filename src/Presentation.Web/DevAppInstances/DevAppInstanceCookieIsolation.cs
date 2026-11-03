namespace BridgingIT.DevKit.Presentation.Web;

using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Session;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

/// <summary>
/// Gives every named cookie of an active development instance the suffix <c>.&lt;appId&gt;.w&lt;slot&gt;</c>.
/// Browsers do not isolate cookies by port, so instances on <c>localhost</c> would otherwise overwrite each other's
/// authentication, session, antiforgery and temp-data cookies.
/// </summary>
/// <remarks>
/// Covers every <see cref="CookieAuthenticationOptions"/> scheme (including app-defined schemes, the DevKit dashboard
/// and the fake identity provider), <see cref="SessionOptions"/>, <see cref="AntiforgeryOptions"/> and
/// <see cref="CookieTempDataProviderOptions"/>. OpenID Connect correlation and nonce cookies already carry a unique
/// suffix per challenge and stay unchanged.
/// </remarks>
internal static class DevAppInstanceCookieIsolation
{
    /// <summary>
    /// Registers the post-configure callbacks that suffix the cookie names.
    /// </summary>
    /// <param name="services">The application services.</param>
    /// <param name="context">The active instance context.</param>
    /// <param name="isActive">
    /// Optional check that runs when the options are built. Cookie names stay unchanged once it returns <c>false</c>,
    /// for example after the host fell back to its configured defaults.
    /// </param>
    public static void Apply(IServiceCollection services, DevAppInstanceContext context, Func<bool> isActive = null)
    {
        var suffix = GetSuffix(context);
        services.AddSingleton<IPostConfigureOptions<CookieAuthenticationOptions>>(new AuthenticationCookiePostConfigure(suffix, isActive));
        services.PostConfigure<AntiforgeryOptions>(o =>
        {
            if (isActive?.Invoke() != false)
            {
                o.Cookie.Name = Suffix(o.Cookie.Name ?? ".AspNetCore.Antiforgery", suffix);
            }
        });
        services.PostConfigure<SessionOptions>(o =>
        {
            if (isActive?.Invoke() != false)
            {
                o.Cookie.Name = Suffix(o.Cookie.Name ?? SessionDefaults.CookieName, suffix);
            }
        });
        services.PostConfigure<CookieTempDataProviderOptions>(o =>
        {
            if (isActive?.Invoke() != false)
            {
                o.Cookie.Name = Suffix(o.Cookie.Name ?? CookieTempDataProvider.CookieName, suffix);
            }
        });
    }

    /// <summary>
    /// Gets the cookie name suffix of an instance.
    /// </summary>
    /// <param name="context">The active instance context.</param>
    /// <returns>The suffix, for example <c>.forecasthub.w02</c>.</returns>
    public static string GetSuffix(DevAppInstanceContext context) => $".{context.AppId}.{context.SlotName}";

    /// <summary>
    /// Appends the suffix to a cookie name unless the name already ends with it.
    /// </summary>
    /// <param name="name">The configured cookie name.</param>
    /// <param name="suffix">The instance suffix.</param>
    /// <returns>The suffixed cookie name.</returns>
    public static string Suffix(string name, string suffix) =>
        name.EndsWith(suffix, StringComparison.Ordinal) ? name : name + suffix;

    private sealed class AuthenticationCookiePostConfigure(string suffix, Func<bool> isActive) : IPostConfigureOptions<CookieAuthenticationOptions>
    {
        public void PostConfigure(string name, CookieAuthenticationOptions options)
        {
            if (isActive?.Invoke() == false)
            {
                return;
            }

            // ASP.NET Core derives the default name from the scheme in its own post-configure, which may run later.
            var cookieName = options.Cookie.Name ?? CookieAuthenticationDefaults.CookiePrefix + name;
            options.Cookie.Name = Suffix(cookieName, suffix);
        }
    }
}
