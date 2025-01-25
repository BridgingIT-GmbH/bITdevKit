// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

public class IdentityProviderEndpointPathsBuilder
{
    private readonly FakeIdentityProviderEndpointPaths _paths;

    public IdentityProviderEndpointPathsBuilder()
    {
        this._paths = new FakeIdentityProviderEndpointPaths();
    }

    public IdentityProviderEndpointPathsBuilder AuthorizePath(string path)
    {
        this._paths.Authorize = path;
        return this;
    }

    public IdentityProviderEndpointPathsBuilder TokenPath(string path)
    {
        this._paths.Token = path;
        return this;
    }

    public IdentityProviderEndpointPathsBuilder UserInfoPath(string path)
    {
        this._paths.UserInfo = path;
        return this;
    }

    public IdentityProviderEndpointPathsBuilder LogoutPath(string path)
    {
        this._paths.Logout = path;
        return this;
    }

    public IdentityProviderEndpointPathsBuilder WellKnownConfigurationPath(string path)
    {
        this._paths.WellKnownConfiguration = path;
        return this;
    }

    public IdentityProviderEndpointPathsBuilder AuthorizeCallbackPath(string path)
    {
        this._paths.AuthorizeCallback = path;
        return this;
    }

    public FakeIdentityProviderEndpointPaths Build()
    {
        return this._paths;
    }
}
