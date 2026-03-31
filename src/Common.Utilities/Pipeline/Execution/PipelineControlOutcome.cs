// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents the directional control outcome returned by a pipeline step.
/// </summary>
public enum PipelineControlOutcome
{
    /// <summary>
    /// Continue with the next step.
    /// </summary>
    Continue,

    /// <summary>
    /// Skip the current step and proceed to the next step.
    /// </summary>
    Skip,

    /// <summary>
    /// Retry the current step.
    /// </summary>
    Retry,

    /// <summary>
    /// Break the pipeline early.
    /// </summary>
    Break,

    /// <summary>
    /// Terminate the remaining pipeline execution intentionally.
    /// </summary>
    Terminate
}
