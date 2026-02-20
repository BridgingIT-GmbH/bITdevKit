// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Options for configuring default chaos injection behavior.
/// </summary>
public class ChaosOptions
{
    /// <summary>
    /// Gets or sets the default injection rate (0.0 to 1.0).
    /// Used when the <see cref="HandlerChaosAttribute"/> doesn't specify an injection rate.
    /// </summary>
    public double? DefaultInjectionRate { get; set; }

    /// <summary>
    /// Gets or sets the default value indicating whether chaos injection is enabled.
    /// Used when the <see cref="HandlerChaosAttribute"/> doesn't specify this setting.
    /// </summary>
    public bool? DefaultEnabled { get; set; }
}