// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Defines a validation rule that checks whether a given value is not null.
/// The rule returns a success result if the value is not null; otherwise, it returns a failure result.
/// </summary>
/// <typeparam name="T">The type of the value to be validated.</typeparam>
public class IsNotNullRule<T>(T value) : RuleBase
{
    /// <summary>
    /// Sends a message to the console and logs the message.
    /// </summary>
    /// <param name="message">The message to be sent and logged.</param>
    public override string Message => "Value must not be null";

    /// <summary>
    /// Executes a business rule encapsulated within an Action delegate.
    /// Ensures that the provided rule is invoked with the given context.
    /// </summary>
    /// <param name="rule">The business rule to be executed as an Action delegate.</param>
    /// <param name="context">The context or parameters required by the rule to operate.</param>
    public override Result Execute() =>
        Result.SuccessIf(value is not null);
}