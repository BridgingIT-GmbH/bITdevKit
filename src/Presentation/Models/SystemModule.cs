// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

/// <summary>
/// Represents system module.
/// </summary>
public class SystemModule
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }
    /// <summary>
    /// Gets or sets the enabled.
    /// </summary>
    public bool Enabled { get; set; }
    /// <summary>
    /// Gets or sets the is registered.
    /// </summary>
    public bool IsRegistered { get; set; }
    /// <summary>
    /// Gets or sets the priority.
    /// </summary>
    public int Priority { get; set; }
}
