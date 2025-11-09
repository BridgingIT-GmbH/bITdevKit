// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Utilities;

using System.Collections.Generic;

/// <summary>
/// Represents the response for a log query, containing paged log entries and pagination metadata.
/// </summary>
public class LogEntryQueryResponse
{
    /// <summary>
    /// Gets or sets the list of log entries returned by the query.
    /// </summary>
    public IReadOnlyList<LogEntryModel> Items { get; set; } = new List<LogEntryModel>();

    /// <summary>
    /// Gets or sets the continuation token for retrieving the next page of results.
    /// Null if no more pages are available.
    /// </summary>
    public string ContinuationToken { get; set; }

    /// <summary>
    /// Gets or sets the number of items in the current page.
    /// </summary>
    public int PageSize { get; set; }
}
