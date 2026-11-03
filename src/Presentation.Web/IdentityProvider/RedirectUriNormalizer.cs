// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

/// <summary>
/// Resolves origin-relative client redirect URIs against the fake identity provider issuer and matches requested URIs
/// exactly after resolution. The issuer is the only origin source; request Host headers are never used.
/// </summary>
internal static class RedirectUriNormalizer
{
    /// <summary>
    /// Resolves a registered redirect URI. Values starting with a single <c>/</c> resolve against the issuer origin;
    /// all other values are returned unchanged.
    /// </summary>
    /// <param name="registered">The registered redirect URI.</param>
    /// <param name="issuer">The effective issuer.</param>
    /// <returns>The absolute redirect URI.</returns>
    public static string Resolve(string registered, string issuer)
    {
        if (!IsRelative(registered) || string.IsNullOrWhiteSpace(issuer))
        {
            return registered;
        }

        return GetOrigin(issuer) + registered;
    }

    /// <summary>
    /// Resolves every registered redirect URI of a client.
    /// </summary>
    /// <param name="registered">The registered redirect URIs.</param>
    /// <param name="issuer">The effective issuer.</param>
    /// <returns>The resolved redirect URIs.</returns>
    public static IReadOnlyList<string> ResolveAll(IEnumerable<string> registered, string issuer) =>
        [.. (registered ?? []).Select(uri => Resolve(uri, issuer))];

    /// <summary>
    /// Determines whether a requested redirect URI exactly matches one of the resolved registered URIs.
    /// </summary>
    /// <param name="registered">The registered redirect URIs.</param>
    /// <param name="requested">The requested redirect URI.</param>
    /// <param name="issuer">The effective issuer.</param>
    /// <returns><see langword="true"/> when the requested URI is registered.</returns>
    public static bool Matches(IEnumerable<string> registered, string requested, string issuer) =>
        requested is not null && (registered ?? []).Any(uri => string.Equals(Resolve(uri, issuer), requested, StringComparison.Ordinal));

    /// <summary>
    /// Determines whether a registered URI is origin-relative.
    /// </summary>
    /// <param name="registered">The registered redirect URI.</param>
    /// <returns><see langword="true"/> for values such as <c>/authentication/login-callback</c>.</returns>
    public static bool IsRelative(string registered) =>
        registered?.StartsWith('/') == true && !registered.StartsWith("//", StringComparison.Ordinal);

    /// <summary>
    /// Returns the scheme, host and port of an absolute URI without a trailing slash.
    /// </summary>
    /// <param name="uri">The absolute URI.</param>
    /// <returns>The origin, or <see langword="null"/> when the value is not absolute.</returns>
    public static string GetOrigin(string uri) =>
        Uri.TryCreate(uri, UriKind.Absolute, out var parsed) ? parsed.GetLeftPart(UriPartial.Authority) : null;

    /// <summary>
    /// Determines whether an absolute URI targets a loopback host.
    /// </summary>
    /// <param name="uri">The URI.</param>
    /// <returns><see langword="true"/> for absolute loopback URIs.</returns>
    public static bool IsLoopback(string uri) =>
        Uri.TryCreate(uri, UriKind.Absolute, out var parsed) &&
        (parsed.Scheme == Uri.UriSchemeHttps || parsed.Scheme == Uri.UriSchemeHttp) &&
        parsed.IsLoopback;
}
