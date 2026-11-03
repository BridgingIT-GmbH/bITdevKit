namespace BridgingIT.DevKit.Presentation.Web;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Aligns the in-process fake identity provider with an active development instance: the issuer becomes the resolved
/// public origin and absolute loopback callbacks on other origins are rejected. Cookie names are handled by
/// <see cref="DevAppInstanceCookieIsolation"/>.
/// </summary>
internal static class DevAppInstanceIdentityAlignment
{
    private const string DefaultIssuer = "https://localhost:5001";

    /// <summary>
    /// Applies the issuer rules and validates the client callbacks of a registered fake identity provider.
    /// </summary>
    /// <param name="services">The application services.</param>
    /// <param name="context">The active instance context.</param>
    /// <param name="ordinaryAuthorities">The authority values configured before the managed source was added.</param>
    /// <returns>The log step to report, or <see langword="null"/> when no fake identity provider is registered.</returns>
    public static DevAppInstanceLogStep AlignIdentityProvider(
        IServiceCollection services,
        DevAppInstanceContext context,
        IEnumerable<string> ordinaryAuthorities)
    {
        var options = services
            .Where(d => d.ServiceType == typeof(FakeIdentityProviderEndpointsOptions))
            .Select(d => d.ImplementationInstance)
            .OfType<FakeIdentityProviderEndpointsOptions>()
            .LastOrDefault();
        if (options is null)
        {
            return null;
        }

        var origin = context.PublicOrigin;
        var issuer = options.Issuer?.TrimEnd('/');
        var replaceable = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { DefaultIssuer, origin };
        foreach (var authority in ordinaryAuthorities ?? [])
        {
            if (RedirectUriNormalizer.IsLoopback(authority))
            {
                replaceable.Add(authority.TrimEnd('/'));
            }
        }

        if (string.IsNullOrWhiteSpace(issuer) || replaceable.Contains(issuer))
        {
            options.Issuer = origin;
        }
        else if (RedirectUriNormalizer.IsLoopback(issuer))
        {
            throw new DevAppInstanceException(
                DevAppInstanceErrorCodes.ConfigurationConflict,
                $"The fake identity provider issuer '{options.Issuer}' is a loopback origin other than the instance origin '{origin}'.",
                "Remove the WithIssuer call, or derive it from 'Authentication:Authority' after AddDevAppInstance.")
            { AppId = context.AppId, Slot = context.Slot };
        }
        else
        {
            return new("identity provider kept external issuer (issuer={Issuer})", [options.Issuer]);
        }

        var conflicts = (options.Clients ?? [])
            .SelectMany(c => (c.RedirectUris ?? []).Select(uri => (Client: c.ClientId, Uri: uri)))
            .Where(r => !RedirectUriNormalizer.IsRelative(r.Uri) &&
                RedirectUriNormalizer.IsLoopback(r.Uri) &&
                !string.Equals(RedirectUriNormalizer.GetOrigin(r.Uri), origin, StringComparison.OrdinalIgnoreCase))
            .Select(r => $"{r.Client}: {r.Uri}")
            .ToList();
        if (conflicts.Count > 0)
        {
            throw new DevAppInstanceException(
                DevAppInstanceErrorCodes.ConfigurationConflict,
                $"Fake identity provider clients register loopback callbacks on another origin than '{origin}': {string.Join(", ", conflicts)}.",
                "Register origin-relative callbacks such as '/authentication/login-callback', or derive them from 'Authentication:Authority'.")
            { AppId = context.AppId, Slot = context.Slot };
        }

        var relative = (options.Clients ?? []).Sum(c => (c.RedirectUris ?? []).Count(RedirectUriNormalizer.IsRelative));

        return new("identity provider aligned (issuer={Issuer}, relativeRedirectUris={RelativeRedirectUris})", [options.Issuer, relative]);
    }
}
