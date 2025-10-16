// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

using System;

/// <summary>
/// Error indicating that a sequence lock could not be acquired within the timeout period.
/// </summary>
public class SequenceLockTimeoutError(string name, TimeSpan timeout) : ResultErrorBase($"Failed to acquire lock for sequence '{name}' within {timeout.TotalSeconds} seconds")
{
    /// <summary>
    /// Gets the name.
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// Gets the timeout duration that was exceeded.
    /// </summary>
    public TimeSpan Timeout { get; } = timeout;
}
