// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Commands;

/// <summary>
/// Represents command response.
/// </summary>
public class CommandResponse
{
    /// <summary>
    /// Initializes a new instance of the <c>CommandResponse</c> class.
    /// </summary>
    /// <param name="cancelledReason">The cancelled reason used by the operation.</param>
    public CommandResponse(string cancelledReason = null)
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
    /// Executes the for operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public static CommandResponse For()
    {
        return new CommandResponse();
    }

    /// <summary>
    /// Executes the for operation.
    /// </summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <returns>The result of the operation.</returns>
    public static CommandResponse<TResult> For<TResult>()
    {
        return new CommandResponse<TResult>();
    }

    /// <summary>
    /// Executes the for operation.
    /// </summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="result">The result used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public static CommandResponse<TResult> For<TResult>(TResult result)
    {
        return new CommandResponse<TResult> { Result = result };
    }

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
