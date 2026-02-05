// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Specifies a chaos injection policy for a handler.
/// </summary>
/// <remarks>
/// When parameters are not specified, defaults from <see cref="ChaosOptions"/> will be used.
/// If no options are configured, an exception will be thrown at runtime.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class HandlerChaosAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HandlerChaosAttribute"/> class with specified values.
    /// </summary>
    /// <param name="injectionRate">The injection rate (0.0 to 1.0).</param>
    /// <param name="enabled">Whether chaos injection is enabled.</param>
    public HandlerChaosAttribute(double injectionRate, bool enabled = true)
    {
        if (injectionRate < 0 || injectionRate > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(injectionRate), "Injection rate must be between 0 and 1.");
        }

        this.InjectionRate = injectionRate;
        this.Enabled = enabled;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HandlerChaosAttribute"/> class using defaults from options.
    /// </summary>
    /// <remarks>
    /// When using this constructor, ensure <see cref="ChaosOptions"/> is configured with default values.
    /// </remarks>
    public HandlerChaosAttribute()
    {
    }

    /// <summary>
    /// Gets the injection rate (0.0 to 1.0), or null to use the default from <see cref="ChaosOptions"/>.
    /// </summary>
    public double? InjectionRate { get; }

    /// <summary>
    /// Gets whether chaos injection is enabled, or null to use the default from <see cref="ChaosOptions"/>.
    /// </summary>
    public bool? Enabled { get; }
}