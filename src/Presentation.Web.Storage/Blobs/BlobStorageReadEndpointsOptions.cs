// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Storage;

using BridgingIT.DevKit.Presentation.Web;

/// <summary>
/// Configures the read-only REST endpoint group exposed for Blob Storage content downloads.
/// </summary>
/// <example>
/// <code>
/// services.AddBlobStorage()
///     .AddReadEndpoints(options => options
///         .GroupPath("/_bdk/api/storage/blobs")
///         .AllowAnonymous());
/// </code>
/// </example>
public class BlobStorageReadEndpointsOptions : EndpointsOptionsBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BlobStorageReadEndpointsOptions" /> class.
    /// </summary>
    public BlobStorageReadEndpointsOptions()
    {
        this.GroupPath = "/_bdk/api/storage/blobs";
        this.GroupTag = "_bdk.Storage.Blobs.Read";
        this.RequireAuthorization = true;
    }
}
