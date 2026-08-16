// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

/// <summary>
/// Defines the supported outbox message processing mode values.
/// </summary>
public enum OutboxMessageProcessingMode
{
    /// <summary>
    /// Represents the interval value.
    /// </summary>
    Interval = 0,
    /// <summary>
    /// Represents the immediate value.
    /// </summary>
    Immediate = 1
}
