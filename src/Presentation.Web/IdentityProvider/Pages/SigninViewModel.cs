// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.IdentityProvider.Pages;

/// <summary>
/// Represents signin view model.
/// </summary>
public class SigninViewModel
{
    /// <summary>
    /// Gets or sets the request.
    /// </summary>
    public AuthorizeRequest Request { get; set; } = new AuthorizeRequest();

    /// <summary>
    /// Gets or sets the options.
    /// </summary>
    public FakeIdentityProviderEndpointsOptions Options { get; set; } = new FakeIdentityProviderEndpointsOptions();
}
