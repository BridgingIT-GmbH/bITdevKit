// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

/// <summary>
/// Builds identity provider endpoint paths configuration.
/// </summary>
public class IdentityProviderEndpointPathsBuilder
{
    private readonly FakeIdentityProviderEndpointPaths paths;

    /// <summary>
    /// Initializes a new instance of the <c>IdentityProviderEndpointPathsBuilder</c> class.
    /// </summary>
    public IdentityProviderEndpointPathsBuilder()
    {
        this.paths = new FakeIdentityProviderEndpointPaths();
    }

    /// <summary>
    /// Executes the authorize path operation.
    /// </summary>
    /// <param name="path">The path used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public IdentityProviderEndpointPathsBuilder AuthorizePath(string path)
    {
        this.paths.Authorize = path;
        return this;
    }

    /// <summary>
    /// Executes the token path operation.
    /// </summary>
    /// <param name="path">The path used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public IdentityProviderEndpointPathsBuilder TokenPath(string path)
    {
        this.paths.Token = path;
        return this;
    }

    /// <summary>
    /// Configures r info path.
    /// </summary>
    /// <param name="path">The path used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public IdentityProviderEndpointPathsBuilder UserInfoPath(string path)
    {
        this.paths.UserInfo = path;
        return this;
    }

    /// <summary>
    /// Writes a log entry for the out path operation.
    /// </summary>
    /// <param name="path">The path used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public IdentityProviderEndpointPathsBuilder LogoutPath(string path)
    {
        this.paths.Logout = path;
        return this;
    }

    /// <summary>
    /// Executes the well known configuration path operation.
    /// </summary>
    /// <param name="path">The path used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public IdentityProviderEndpointPathsBuilder WellKnownConfigurationPath(string path)
    {
        this.paths.WellKnownConfiguration = path;
        return this;
    }

    /// <summary>
    /// Executes the authorize callback path operation.
    /// </summary>
    /// <param name="path">The path used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public IdentityProviderEndpointPathsBuilder AuthorizeCallbackPath(string path)
    {
        this.paths.AuthorizeCallback = path;
        return this;
    }

    /// <summary>
    /// Executes the build operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public FakeIdentityProviderEndpointPaths Build()
    {
        return this.paths;
    }
}
