// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
///     Represents an exception that is used to simulate chaotic behavior in a system for testing purposes.
/// </summary>
public class ChaosException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ChaosException" /> class.
    ///     Represents an exception that occurs specifically due to chaos engineering experiments.
    /// </summary>
    public ChaosException() { }

    /// <summary>
    ///     Initializes a new instance of the <see cref="ChaosException" /> class.
    ///     Represents errors that occur during application execution related to the chaos scenarios.
    ///     This exception is specific to unpredictable or intended disturbances in the normal flow.
    /// </summary>
    public ChaosException(string message)
        : base(message) { }

    /// <summary>
    ///     Initializes a new instance of the <see cref="ChaosException" /> class.
    ///     Represents errors that occur during application execution when a chaotic event happens.
    /// </summary>
    public ChaosException(string message, Exception innerException)
        : base(message, innerException) { }
}