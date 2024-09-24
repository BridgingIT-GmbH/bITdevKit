// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

/// <summary>
///     Represents a rule within the domain.
/// </summary>
public interface IDomainRule
{
    /// <summary>
    ///     Gets the message associated with the domain rule, indicating why
    ///     the rule is not satisfied. This is usually a user-friendly
    ///     explanation that can be displayed in the UI or logged for further
    ///     investigation.
    /// </summary>
    string Message { get; }

    /// <summary>
    ///     Determines if the domain rule is enabled asynchronously.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>
    ///     A task representing the asynchronous operation. The task result contains a boolean value indicating whether
    ///     the domain rule is enabled.
    /// </returns>
    Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Evaluates whether the domain rule is satisfied based on the given criteria.
    /// </summary>
    /// <param name="cancellationToken">
    ///     A CancellationToken that can be used by other objects or threads to receive notice of
    ///     cancellation.
    /// </param>
    /// <returns>
    ///     A task representing the asynchronous operation, containing a boolean value indicating whether the rule is
    ///     satisfied.
    /// </returns>
    Task<bool> ApplyAsync(CancellationToken cancellationToken = default);
    // TODO: maybe refactor and use Result with success/failure and optional messages/errors
}

/// <summary>
///     Defines a business rule interface that extends the <see cref="IDomainRule" /> interface.
/// </summary>
[Obsolete("Use IDomainRule from now on (incl IsSatisfiedAsync -> ApplyAsync)")]
public interface IBusinessRule : IDomainRule
{
    /// <summary>
    ///     Determines if the business rule is satisfied asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Optional cancellation token to cancel the operation.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains a boolean value indicating if the
    ///     rule is satisfied.
    /// </returns>
    Task<bool> IsSatisfiedAsync(CancellationToken cancellationToken = default);
}