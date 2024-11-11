// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents a  rule that can be applied synchronously or asynchronously.
/// </summary>
public interface IRule
{
    /// <summary>
    /// Gets a message describing the purpose or validation of the rule.
    /// </summary>
    string Message { get; }

    /// <summary>
    /// Gets a value indicating whether the rule should be executed.
    /// </summary>
    /// <remarks>
    /// By default, rules are enabled. Override this property to conditionally disable rules.
    /// </remarks>
    bool IsEnabled => true;

    /// <summary>
    /// Applies the rule synchronously and returns the result.
    /// </summary>
    /// <returns>A <see cref="Result"/> indicating success or failure of the rule.</returns>
    Result IsSatisfied();

    /// <summary>
    /// Applies the rule asynchronously and returns the result.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="Result"/> indicating success or failure of the rule.</returns>
    Task<Result> IsSatisfiedAsync(CancellationToken cancellationToken = default);
}