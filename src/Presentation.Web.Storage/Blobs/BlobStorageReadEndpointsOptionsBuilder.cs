// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Storage;

using BridgingIT.DevKit.Presentation.Web;

/// <summary>
/// Provides a fluent builder for <see cref="BlobStorageReadEndpointsOptions" />.
/// </summary>
/// <example>
/// <code>
/// var options = new BlobStorageReadEndpointsOptionsBuilder()
///     .GroupPath("/files/blobs")
///     .AllowAnonymous()
///     .Build();
/// </code>
/// </example>
public class BlobStorageReadEndpointsOptionsBuilder
    : EndpointsOptionsBuilderBase<BlobStorageReadEndpointsOptions, BlobStorageReadEndpointsOptionsBuilder>
{
}
