// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Provides fluent factories for document-store query models.
/// </summary>
public static class DocumentQueries
{
    /// <summary>
    /// Creates a document page query builder.
    /// </summary>
    public static DocumentQueryBuilder Query() => DocumentQueryBuilder.Create();

    /// <summary>
    /// Creates a document count query builder.
    /// </summary>
    public static DocumentCountQueryBuilder Count() => DocumentCountQueryBuilder.Create();
}
