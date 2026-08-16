// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Commands.Outbox;

/// <summary>
/// Represents outbox message command result.
/// </summary>
public class OutboxMessageCommandResult
{
    /// <summary>
    /// Initializes a new instance of the <c>OutboxMessageCommandResult</c> class.
    /// </summary>
    public OutboxMessageCommandResult()
    {
        this.ErrorCode = OutboxMessageCommandResultErrorCodes.NoError;
    }

    /// <summary>
    /// Initializes a new instance of the <c>OutboxMessageCommandResult</c> class.
    /// </summary>
    /// <param name="errorCode">The error code used by the operation.</param>
    public OutboxMessageCommandResult(OutboxMessageCommandResultErrorCodes errorCode)
    {
        this.ErrorCode = errorCode;
    }

    /// <summary>
    /// Gets the has error.
    /// </summary>
    public bool HasError => this.ErrorCode != OutboxMessageCommandResultErrorCodes.NoError;

    // ReSharper disable once MemberCanBePrivate.Global
    /// <summary>
    /// Gets or sets the error code.
    /// </summary>
    public OutboxMessageCommandResultErrorCodes ErrorCode { get; set; }
}
