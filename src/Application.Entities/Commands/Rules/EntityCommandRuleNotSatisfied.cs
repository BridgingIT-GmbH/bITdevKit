// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Entities;

/// <summary>
/// Represents entity command rule not satisfied.
/// </summary>
public class EntityCommandRuleNotSatisfied : Exception
{
    /// <summary>
    /// Initializes a new instance of the <c>EntityCommandRuleNotSatisfied</c> class.
    /// </summary>
    public EntityCommandRuleNotSatisfied() { }

    /// <summary>
    /// Initializes a new instance of the <c>EntityCommandRuleNotSatisfied</c> class.
    /// </summary>
    /// <param name="message">The message associated with the operation.</param>
    public EntityCommandRuleNotSatisfied(string message)
        : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <c>EntityCommandRuleNotSatisfied</c> class.
    /// </summary>
    /// <param name="message">The message associated with the operation.</param>
    /// <param name="innerException">The inner exception used by the operation.</param>
    public EntityCommandRuleNotSatisfied(string message, Exception innerException)
        : base(message, innerException) { }
}
