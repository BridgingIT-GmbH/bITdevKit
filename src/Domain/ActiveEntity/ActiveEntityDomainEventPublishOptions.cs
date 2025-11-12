// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

/// <summary>
/// Options for configuring domain event publishing behavior.
/// </summary>
public class ActiveEntityDomainEventPublishOptions
{
    /// <summary>
    /// Gets or sets whether to publish child events before parent events.
    /// Default is false (parent-first).
    /// </summary>
    public bool ReverseOrder { get; set; }

    /// <summary>
    /// Gets or sets whether to clear events after publishing.
    /// Default is true.
    /// </summary>
    public bool ClearEvents { get; set; } = true;
}
