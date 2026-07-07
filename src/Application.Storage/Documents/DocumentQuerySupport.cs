// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Describes how a provider supports a document query shape.
/// </summary>
public enum DocumentQuerySupport
{
    /// <summary>
    /// The provider does not support the query shape.
    /// </summary>
    Unsupported,

    /// <summary>
    /// The provider supports the query shape efficiently.
    /// </summary>
    SupportedEfficiently,

    /// <summary>
    /// The provider supports the query shape on the storage backend.
    /// </summary>
    SupportedServerSide,

    /// <summary>
    /// The provider can support the query shape only by client-side filtering.
    /// </summary>
    SupportedClientSide
}
