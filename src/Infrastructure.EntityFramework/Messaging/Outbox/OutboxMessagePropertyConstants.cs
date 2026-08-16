// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Messaging;

/// <summary>
/// Represents outbox message property constants.
/// </summary>
public struct OutboxMessagePropertyConstants
{
    /// <summary>
    /// Defines the process status key value.
    /// </summary>
    public const string ProcessStatusKey = "ProcessStatus";

    /// <summary>
    /// Defines the process message key value.
    /// </summary>
    public const string ProcessMessageKey = "ProcessMessage";

    /// <summary>
    /// Defines the process attempts key value.
    /// </summary>
    public const string ProcessAttemptsKey = "ProcessAttempts";
}
