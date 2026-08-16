// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
///     Defines structured logging and activity-tag keys used by tracing utilities.
/// </summary>
public struct ActivityConstants
{
    /// <summary>Gets the structured log key used by tracing utilities.</summary>
    public const string LogKey = "TRC";

    /// <summary>Gets the activity tag key used for correlation identifiers.</summary>
    public const string CorrelationIdTagKey = CorrelationId.ActivityBaggageName;

    /// <summary>Gets the activity tag key used for flow identifiers.</summary>
    public const string FlowIdTagKey = "flow_id";

    /// <summary>Gets the activity tag key used for module names.</summary>
    public const string ModuleNameTagKey = "module.name";
}
