// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Commands.Outbox;

/// <summary>
/// Defines the supported outbox message command result error codes values.
/// </summary>
public enum OutboxMessageCommandResultErrorCodes
{
    /// <summary>
    /// Represents the no error value.
    /// </summary>
    NoError = 0,
    /// <summary>
    /// Represents the duplicated message value.
    /// </summary>
    DuplicatedMessage = 1,
    /// <summary>
    /// Represents the other error value.
    /// </summary>
    OtherError = 2
}
