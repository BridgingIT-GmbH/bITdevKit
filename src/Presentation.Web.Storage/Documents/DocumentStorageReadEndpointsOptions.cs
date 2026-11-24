// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Storage;

using BridgingIT.DevKit.Presentation.Web;

/// <summary>
/// Configures the read-only REST endpoint group exposed for Document Storage content reads.
/// </summary>
/// <example>
/// <code>
/// services.AddDocumentStorage()
///     .AddReadEndpoints(options => options
///         .GroupPath("/_bdk/api/storage/documents")
///         .AllowAnonymous());
/// </code>
/// </example>
public class DocumentStorageReadEndpointsOptions : EndpointsOptionsBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentStorageReadEndpointsOptions" /> class.
    /// </summary>
    public DocumentStorageReadEndpointsOptions()
    {
        this.GroupPath = "/_bdk/api/storage/documents";
        this.GroupTag = "_bdk.Storage.Documents.Read";
        this.RequireAuthorization = true;
    }
}
