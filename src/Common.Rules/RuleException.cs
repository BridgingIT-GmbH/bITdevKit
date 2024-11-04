﻿// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
///     The RuleException is thrown when a rule is violated.
/// </summary>
public class RuleException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="RuleException" /> class.
    ///     Represents errors that occur due to rule violations.
    /// </summary>
    public RuleException() { }

    /// <summary>
    ///     Initializes a new instance of the <see cref="RuleException" /> class.
    ///     Represents errors that occur when a rule is violated.
    /// </summary>
    public RuleException(string message)
        : base(message) { }

    /// <summary>
    ///     Initializes a new instance of the <see cref="RuleException" /> class.
    ///     Represents errors that occur when a rule is violated.
    /// </summary>
    public RuleException(string message, Exception innerException)
        : base(message, innerException) { }
}