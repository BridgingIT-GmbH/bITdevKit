// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Storage;

using BridgingIT.DevKit.Presentation.Web;

/// <summary>
/// Provides a fluent builder for <see cref="DocumentStorageReadEndpointsOptions" />.
/// </summary>
/// <example>
/// <code>
/// var options = new DocumentStorageReadEndpointsOptionsBuilder()
///     .GroupPath("/content/documents")
///     .AllowAnonymous()
///     .Build();
/// </code>
/// </example>
public class DocumentStorageReadEndpointsOptionsBuilder
    : EndpointsOptionsBuilderBase<DocumentStorageReadEndpointsOptions, DocumentStorageReadEndpointsOptionsBuilder>
{
}
