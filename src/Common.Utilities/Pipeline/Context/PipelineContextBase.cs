// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Provides the common base for all pipeline execution contexts.
/// </summary>
public abstract class PipelineContextBase
{
    /// <summary>
    /// Gets the framework-owned pipeline execution metadata for the current run.
    /// </summary>
    public PipelineExecutionContext Pipeline { get; } = new();
}
