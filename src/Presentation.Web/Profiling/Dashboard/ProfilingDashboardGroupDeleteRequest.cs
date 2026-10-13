// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Profiling.Dashboard;

using BridgingIT.DevKit.Common;

/// <summary>Confirms deletion of every retained execution matching an exact displayed operation group and its current filters.</summary>
/// <example><code>var request = new ProfilingDashboardGroupDeleteRequest { Confirmed = true, Selection = selector };</code></example>
public sealed record ProfilingDashboardGroupDeleteRequest
{
    /// <summary>Gets explicit confirmation to delete the selected group.</summary>
    /// <example><code>var confirmed = request.Confirmed;</code></example>
    public bool Confirmed { get; init; }

    /// <summary>Gets the exact key, kind, UTC interval and group conditions; row limits do not limit deletion.</summary>
    /// <example><code>var selection = request.Selection;</code></example>
    public OperationProfilingQuery Selection { get; init; }
}
