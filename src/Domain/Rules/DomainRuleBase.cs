// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

/// <summary>
///     Represents the base class for domain rules.
/// </summary>
public abstract class DomainRuleBase : IDomainRule
{
    /// <summary>
    ///     Gets the message associated with the domain rule.
    /// </summary>
    /// <remarks>
    ///     The message typically explains why the rule was not satisfied.
    /// </remarks>
    public virtual string Message => "Rule not satisfied";

    /// <summary>
    ///     Determines whether the current rule is enabled.
    /// </summary>
    /// <param name="cancellationToken">Optional. A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains true if the rule is enabled;
    ///     otherwise, false.
    /// </returns>
    public virtual Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    /// <summary>
    ///     Asynchronously applies a domain rule.
    /// </summary>
    /// <param name="cancellationToken">
    ///     A cancellation token that can be used by other objects or threads to receive notice of
    ///     cancellation.
    /// </param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains a boolean value indicating whether
    ///     the rule was applied successfully.
    /// </returns>
    public abstract Task<bool> ApplyAsync(CancellationToken cancellationToken = default);
    // TODO: maybe refactor and use Result with success/failure and optional messages/errors
}