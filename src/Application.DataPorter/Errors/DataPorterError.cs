// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

using BridgingIT.DevKit.Common;

/// <summary>
/// Base error class for DataPorter operations.
/// </summary>
public class DataPorterError : ResultErrorBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DataPorterError"/> class.
    /// </summary>
    public DataPorterError()
        : base("A DataPorter error occurred.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DataPorterError"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public DataPorterError(string message)
        : base(message)
    {
    }
}
