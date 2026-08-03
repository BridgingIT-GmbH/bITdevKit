// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Storage;

using BridgingIT.DevKit.Presentation.Web;

/// <summary>
/// Provides a fluent builder for <see cref="BlobStorageMaintenanceEndpointsOptions" />.
/// </summary>
/// <example>
/// <code>
/// var options = new BlobStorageMaintenanceEndpointsOptionsBuilder()
///     .GroupPath("/ops/blobs")
///     .RequireAuthorization()
///     .Build();
/// </code>
/// </example>
public class BlobStorageMaintenanceEndpointsOptionsBuilder
    : EndpointsOptionsBuilderBase<BlobStorageMaintenanceEndpointsOptions, BlobStorageMaintenanceEndpointsOptionsBuilder>
{
}
