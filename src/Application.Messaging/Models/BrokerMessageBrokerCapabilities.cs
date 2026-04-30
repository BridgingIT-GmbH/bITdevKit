// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

/// <summary>
/// Describes capabilities supported by the active message broker.
/// </summary>
public class BrokerMessageBrokerCapabilities
{
    /// <summary>
    /// Gets or sets a value indicating whether the broker supports durable message storage.
    /// </summary>
    public bool SupportsDurableStorage { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the broker supports message retry.
    /// </summary>
    public bool SupportsRetry { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the broker supports message archival.
    /// </summary>
    public bool SupportsArchive { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the broker supports lease management.
    /// </summary>
    public bool SupportsLeaseManagement { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the broker supports pause/resume of message types.
    /// </summary>
    public bool SupportsPauseResume { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the broker supports waiting message inspection.
    /// </summary>
    public bool SupportsWaitingMessageInspection { get; set; }
}
