// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents how a step is represented inside a built pipeline definition.
/// </summary>
public enum PipelineStepSourceKind
{
    /// <summary>
    /// The step is backed by a step type.
    /// </summary>
    Type,

    /// <summary>
    /// The step is backed by an inline delegate.
    /// </summary>
    Inline
}
