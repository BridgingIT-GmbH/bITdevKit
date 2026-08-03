// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Presentation.Web.Storage;

using BridgingIT.DevKit.Presentation.Web;

/// <summary>
/// Configures stable Storage Permalink download endpoints.
/// </summary>
/// <example>
/// <code>
/// services.AddStoragePermalinkEndpoints(options => options.RequireAuthorization());
/// </code>
/// </example>
public class StoragePermalinkEndpointsOptions : EndpointsOptionsBase
{
    /// <summary>
    /// Initializes anonymous permalink downloads at the standard storage route.
    /// </summary>
    public StoragePermalinkEndpointsOptions()
    {
        this.GroupPath = StoragePermalinkRoutes.GroupPath;
        this.GroupTag = "_bdk.Storage.Permalinks";
        this.RequireAuthorization = false;
    }
}
