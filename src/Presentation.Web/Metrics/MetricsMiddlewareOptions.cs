// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

/// <summary>
/// Configures the runtime metrics middleware behavior.
/// </summary>
public class MetricsMiddlewareOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether per-route ASP.NET request metrics should be captured.
    /// </summary>
    public bool RouteMetricsEnabled { get; set; } = true;
}
