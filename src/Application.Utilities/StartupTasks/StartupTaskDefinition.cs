// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Utilities;

using Common;

/// <summary>
/// Represents startup task definition.
/// </summary>
public class StartupTaskDefinition
{
    /// <summary>
    /// Gets or sets the task type.
    /// </summary>
    public Type TaskType { get; set; }

    /// <summary>
    /// Gets or sets the options.
    /// </summary>
    public StartupTaskOptions Options { get; set; } = new();
}
