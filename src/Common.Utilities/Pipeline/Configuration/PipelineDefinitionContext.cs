// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents definition-time context passed to structural step conditions.
/// </summary>
public class PipelineDefinitionContext
{
    /// <summary>
    /// Gets the definition-time property bag for arbitrary metadata.
    /// </summary>
    public PropertyBag Items { get; } = new();
}
