// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using System.Globalization;

/// <summary>
/// Computes stable hashes for logical document-store queries.
/// </summary>
/// <example>
/// <code>
/// var hash = DocumentQueryHash.Compute&lt;Person&gt;("find", query, 100);
/// </code>
/// </example>
public static class DocumentQueryHash
{
    /// <summary>
    /// Computes a query hash for page operations.
    /// </summary>
    /// <typeparam name="T">The document payload type used by the query.</typeparam>
    /// <param name="operation">The logical operation name, such as <c>find</c> or <c>list</c>.</param>
    /// <param name="query">The document query to hash.</param>
    /// <param name="take">The normalized page size used by the query.</param>
    /// <returns>A stable hash for the document type, operation, query shape, and page size.</returns>
    /// <example>
    /// <code>
    /// var hash = DocumentQueryHash.Compute&lt;Person&gt;("find", query, 100);
    /// </code>
    /// </example>
    public static string Compute<T>(string operation, DocumentQuery query, int take)
        where T : class, new()
    {
        var key = query?.DocumentKey;
        return HashHelper.ComputeSha256(string.Join("|",
            typeof(T).FullName?.ToLowerInvariant(),
            operation,
            key?.PartitionKey ?? string.Empty,
            key?.RowKey ?? string.Empty,
            query?.Filter.ToString() ?? DocumentKeyFilter.FullMatch.ToString(),
            take.ToString(CultureInfo.InvariantCulture),
            query?.AllowFullScan.ToString(CultureInfo.InvariantCulture) ?? bool.FalseString));
    }

    /// <summary>
    /// Computes a query hash for count operations.
    /// </summary>
    /// <typeparam name="T">The document payload type used by the query.</typeparam>
    /// <param name="operation">The logical operation name, such as <c>count</c>.</param>
    /// <param name="query">The document count query to hash.</param>
    /// <returns>A stable hash for the document type, operation, and count query shape.</returns>
    /// <example>
    /// <code>
    /// var hash = DocumentQueryHash.Compute&lt;Person&gt;("count", query);
    /// </code>
    /// </example>
    public static string Compute<T>(string operation, DocumentCountQuery query)
        where T : class, new()
    {
        var key = query?.DocumentKey;
        return HashHelper.ComputeSha256(string.Join("|",
            typeof(T).FullName?.ToLowerInvariant(),
            operation,
            key?.PartitionKey ?? string.Empty,
            key?.RowKey ?? string.Empty,
            query?.Filter.ToString() ?? DocumentKeyFilter.FullMatch.ToString(),
            query?.AllowFullScan.ToString(CultureInfo.InvariantCulture) ?? bool.FalseString));
    }
}
