// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

/// <summary>
/// Defines well-known metadata keys stored in <see cref="OutboxDomainEvent.Properties" />.
/// </summary>
public struct OutboxDomainEventPropertyConstants
{
    /// <summary>
    /// Gets the metadata key used to store the latest processing status.
    /// </summary>
    public const string ProcessStatusKey = "ProcessStatus";

    /// <summary>
    /// Gets the metadata key used to store the latest processing message.
    /// </summary>
    public const string ProcessMessageKey = "ProcessMessage";

    /// <summary>
    /// Gets the metadata key used to store the processing attempt count.
    /// </summary>
    public const string ProcessAttemptsKey = "ProcessAttempts";
}
