// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.IdentityProvider.Pages;

public class SigninViewModel
{
    public AuthorizeRequest Request { get; set; } = new AuthorizeRequest();

    public FakeIdentityProviderEndpointsOptions Options { get; set; } = new FakeIdentityProviderEndpointsOptions();
}