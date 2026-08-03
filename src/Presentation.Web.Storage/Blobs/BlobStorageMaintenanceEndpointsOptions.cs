// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Storage;

using BridgingIT.DevKit.Presentation.Web;

/// <summary>
/// Configures the REST endpoint group exposed for Blob Storage maintenance operations.
/// </summary>
/// <example>
/// <code>
/// services.AddBlobStorage()
///     .AddMaintenanceEndpoints(options => options
///         .GroupPath("/_bdk/api/storage/blobs")
///         .RequireAuthorization());
/// </code>
/// </example>
public class BlobStorageMaintenanceEndpointsOptions : EndpointsOptionsBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BlobStorageMaintenanceEndpointsOptions" /> class.
    /// </summary>
    public BlobStorageMaintenanceEndpointsOptions()
    {
        this.GroupPath = "/_bdk/api/storage/blobs";
        this.GroupTag = "_bdk.Storage.Blobs.Maintenance";
        this.RequireAuthorization = true;
    }
}
