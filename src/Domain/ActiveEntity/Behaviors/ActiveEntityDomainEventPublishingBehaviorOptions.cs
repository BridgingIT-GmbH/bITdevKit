// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

/// <summary>
/// Options for configuring domain event publishing behavior timing.
/// </summary>
public class ActiveEntityDomainEventPublishingBehaviorOptions
{
    /// <summary>
    /// Gets or sets whether to publish events in Before* hooks.
    /// Default is false (publish in After* hooks).
    /// </summary>
    public bool PublishBefore { get; set; }
}
