// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Queries;

/// <summary>
/// Represents query response.
/// </summary>
/// <typeparam name="TValue">The value type.</typeparam>
public class QueryResponse<TValue>
{
    /// <summary>
    /// Initializes a new instance of the <c>QueryResponse</c> class.
    /// </summary>
    /// <param name="cancelledReason">The cancelled reason used by the operation.</param>
    public QueryResponse(string cancelledReason = null)
    {
        if (string.IsNullOrEmpty(cancelledReason))
        {
            return;
        }

        this.Cancelled = true;
        this.CancelledReason = cancelledReason;
    }

    /// <summary>
    /// Gets or sets the cancelled.
    /// </summary>
    public bool Cancelled { get; private set; }

    /// <summary>
    /// Gets or sets the cancelled reason.
    /// </summary>
    public string CancelledReason { get; private set; }

    /// <summary>
    /// Gets or sets the result.
    /// </summary>
    public TValue Result { get; set; }

    /// <summary>
    /// Executes the set cancelled operation.
    /// </summary>
    /// <param name="cancelledReason">The cancelled reason used by the operation.</param>
    public void SetCancelled(string cancelledReason)
    {
        if (string.IsNullOrEmpty(cancelledReason))
        {
            return;
        }

        this.Cancelled = true;
        this.CancelledReason = cancelledReason;
    }
}
