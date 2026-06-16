// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

/// <summary>
///     Provides default authentication scheme names used by the fake identity provider.
/// </summary>
/// <example>
/// <code>
/// options.CookieAuthenticationScheme = FakeIdentityProviderAuthenticationDefaults.CookieScheme;
/// </code>
/// </example>
public static class FakeIdentityProviderAuthenticationDefaults
{
    /// <summary>
    ///     The fake identity provider cookie authentication scheme.
    /// </summary>
    /// <example>
    /// <code>
    /// var scheme = FakeIdentityProviderAuthenticationDefaults.CookieScheme;
    /// </code>
    /// </example>
    public const string CookieScheme = "_bdk.fakeidentity.cookie";
}
