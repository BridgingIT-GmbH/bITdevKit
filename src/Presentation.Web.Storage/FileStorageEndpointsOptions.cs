// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Storage;

using BridgingIT.DevKit.Presentation.Web;

/// <summary>
/// Configures the REST endpoint group exposed for file storage providers.
/// </summary>
/// <example>
/// <code>
/// services.AddFileStorage(factory => factory
///     .RegisterProvider("documents", builder => builder.UseLocal("Documents", rootPath))
///     .RegisterProvider("archive", builder => builder.UseLocal("Archive", archiveRootPath)))
///     .AddEndpoints(options => options
///         .GroupPath("/_bdk/api")
///         .GroupTag("_bdk/storage")
///         .RequireAuthorization());
/// </code>
/// </example>
public class FileStorageEndpointsOptions : EndpointsOptionsBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FileStorageEndpointsOptions" /> class.
    /// </summary>
    public FileStorageEndpointsOptions()
    {
        this.GroupPath = "/_bdk/api";
        this.GroupTag = "_bdk.Storage.Files";
    }
}
