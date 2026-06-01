// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

/// <summary>
/// Represents one child occurrence request inside a batch dispatch or attach operation.
/// </summary>
public class JobBatchDispatchItem
{
    /// <summary>
    /// Gets or sets the target job name.
    /// </summary>
    public string JobName { get; set; }

    /// <summary>
    /// Gets or sets the payload data.
    /// </summary>
    public object Data { get; set; }

    /// <summary>
    /// Gets or sets child dispatch options.
    /// </summary>
    public JobDispatchOptions Options { get; set; }

    /// <summary>
    /// Gets or sets the optional sequence value.
    /// </summary>
    public int? Sequence { get; set; }

    /// <summary>
    /// Gets or sets the optional explicit due UTC override.
    /// </summary>
    public DateTimeOffset? DueUtc { get; set; }

    /// <summary>
    /// Gets or sets the optional source step label.
    /// </summary>
    public string SourceStep { get; set; }
}