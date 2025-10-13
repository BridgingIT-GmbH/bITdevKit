// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

using System;

/// <summary>
/// Configuration options for a specific sequence.
/// </summary>
public class SequenceOptions
{
    /// <summary>
    /// Gets or sets the lock timeout for this specific sequence.
    /// Overrides the global LockTimeout setting.
    /// </summary>
    public TimeSpan? LockTimeout { get; set; }

    /// <summary>
    /// Gets or sets the operation timeout for this specific sequence.
    /// Overrides the global OperationTimeout setting.
    /// </summary>
    public TimeSpan? OperationTimeout { get; set; }
}
