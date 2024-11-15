// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents an asynchronous function-based rule.
/// </summary>
/// <remarks>
/// This class inherits from <see cref="AsyncRuleBase"/> and encapsulates a predicate function
/// that asynchronously evaluates a condition. If the condition is met, the rule succeeds;
/// otherwise, it fails with a provided message.
/// </remarks>
public class AsyncFuncRule(Func<CancellationToken, Task<bool>> predicate, string message = "Async predicate rule not satisfied")
    : AsyncRuleBase
{
    /// <summary>
    /// Gets the message associated with the asynchronous validation rule.
    /// </summary>
    /// <remarks>
    /// This message is used to describe the reason why the rule was not satisfied.
    /// Developers can override this property to provide custom failure messages specific to their validation logic.
    /// </remarks>
    public override string Message { get; } = message;

    /// <summary>
    /// Executes the asynchronous rule predicate and returns a result indicating success or failure.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="Result"/> that indicates whether the predicate was satisfied.</returns>
    protected override async Task<Result> ExecuteAsync(CancellationToken cancellationToken) =>
        Result.SuccessIf(await predicate(cancellationToken));
}