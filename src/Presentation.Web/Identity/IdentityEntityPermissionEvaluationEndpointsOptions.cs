// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

/// <summary>
/// Configures identity entity permission evaluation endpoints.
/// </summary>
public class IdentityEntityPermissionEvaluationEndpointsOptions : EndpointsOptionsBase
{
    /// <summary>
    /// Gets or sets the bypass cache.
    /// </summary>
    public bool BypassCache { get; set; }

    /// <summary>
    /// Initializes a new instance of the <c>IdentityEntityPermissionEvaluationEndpointsOptions</c> class.
    /// </summary>
    public IdentityEntityPermissionEvaluationEndpointsOptions()
    {
        this.GroupPath = "/_bdk/api/identity/evaluate/entities/permissions";
        this.GroupTag = "_bdk.Identity";
        this.RequireAuthorization = true;
    }
}
