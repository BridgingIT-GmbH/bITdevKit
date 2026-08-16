// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Entities;

using Common;

/// <summary>
/// Configures entity command messaging behavior.
/// </summary>
public class EntityCommandMessagingBehaviorOptions : OptionsBase
{
    /// <summary>
    /// Gets or sets the enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the excluded entity types.
    /// </summary>
    public List<Type> ExcludedEntityTypes { get; set; }

    /// <summary>
    /// Gets or sets the publish delay.
    /// </summary>
    public int PublishDelay { get; set; } = 100;
}
