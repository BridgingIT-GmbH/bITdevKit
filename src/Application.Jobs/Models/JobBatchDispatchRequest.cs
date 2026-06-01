// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

/// <summary>
/// Represents a request to create or attach batch child occurrences.
/// </summary>
public class JobBatchDispatchRequest : JobBatchCreateRequest
{
    /// <summary>
    /// Gets or sets the child dispatch items.
    /// </summary>
    public IReadOnlyList<JobBatchDispatchItem> Items { get; set; } = [];
}