// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Provides fluent factories for blob-store query models.
/// </summary>
/// <example>
/// <code>
/// var query = BlobQueries.Query()
///     .InContainer("reports")
///     .WithPrefix("2026/06/")
///     .Build();
/// </code>
/// </example>
public static class BlobQueries
{
    /// <summary>
    /// Creates a blob listing query builder.
    /// </summary>
    /// <returns>A new blob query builder.</returns>
    /// <example>
    /// <code>
    /// var builder = BlobQueries.Query();
    /// </code>
    /// </example>
    public static BlobQueryBuilder Query() => BlobQueryBuilder.Create();
}
