// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Storage;

using BridgingIT.DevKit.Presentation.Web;

/// <summary>
/// Configures the REST endpoint group exposed for Document Storage maintenance operations.
/// </summary>
/// <example>
/// <code>
/// services.AddDocumentStorage()
///     .AddMaintenanceEndpoints(options => options
///         .GroupPath("/ops/documents")
///         .RequireAuthorization());
/// </code>
/// </example>
public class DocumentStorageMaintenanceEndpointsOptions : EndpointsOptionsBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentStorageMaintenanceEndpointsOptions" /> class.
    /// </summary>
    public DocumentStorageMaintenanceEndpointsOptions()
    {
        this.GroupPath = "/_bdk/api/storage/documents";
        this.GroupTag = "_bdk.Storage.Documents.Maintenance";
        this.RequireAuthorization = true;
    }
}
