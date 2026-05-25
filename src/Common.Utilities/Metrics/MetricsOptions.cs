// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Configures the shared devkit metrics feature.
/// </summary>
public class MetricsOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether metrics are enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether metrics endpoints should be registered.
    /// </summary>
    public bool EndpointsEnabled { get; set; }
}

/// <summary>
/// Provides fluent configuration for <see cref="MetricsOptions"/>.
/// </summary>
public class MetricsOptionsBuilder
{
    internal MetricsOptionsBuilder(MetricsOptions target)
    {
        this.Target = target ?? throw new ArgumentNullException(nameof(target));
    }

    internal MetricsOptions Target { get; }

    /// <summary>
    /// Enables or disables the metrics feature.
    /// </summary>
    /// <param name="enabled">Indicates whether metrics should be enabled.</param>
    /// <returns>The current builder.</returns>
    public MetricsOptionsBuilder Enabled(bool enabled = true)
    {
        this.Target.Enabled = enabled;

        return this;
    }

    /// <summary>
    /// Enables or disables metrics endpoint registration.
    /// </summary>
    /// <param name="enabled">Indicates whether the metrics endpoints should be registered.</param>
    /// <returns>The current builder.</returns>
    public MetricsOptionsBuilder AddEndpoints(bool enabled = true)
    {
        this.Target.EndpointsEnabled = enabled;

        return this;
    }
}
