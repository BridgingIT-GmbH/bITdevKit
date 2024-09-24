// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

using Common;

/// <summary>
///     Represents the result of a domain policy execution, including the outcome (success or failure) and any associated
///     messages or errors.
/// </summary>
/// <typeparam name="TValue">The type of the value contained in the result.</typeparam>
public class DomainPolicyResult<TValue> : Result<TValue>
{
    /// <summary>
    ///     Gets or sets the results of the policies that have been executed.
    /// </summary>
    /// <remarks>
    ///     The <c>PolicyResults</c> property holds the outcomes of various policies applied to a domain-specific
    ///     context. This can be used to retrieve the result of specific policies by their type.
    /// </remarks>
    public DomainPolicyResults<TValue> PolicyResults { get; set; } = new();

    /// <summary>
    ///     Creates a new successful <see cref="DomainPolicyResult{TValue}" /> with the specified value.
    /// </summary>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="value">The value to be set in the result.</param>
    /// <returns>
    ///     A new instance of <see cref="DomainPolicyResult{TValue}" /> indicating success and holding the specified
    ///     value.
    /// </returns>
    public static new DomainPolicyResult<TValue> Success(TValue value)
    {
        return new DomainPolicyResult<TValue> { Value = value };
    }

    /// <summary>
    ///     Represents a successful domain policy result.
    /// </summary>
    /// <typeparam name="TValue">The type of the value associated with the result.</typeparam>
    /// <returns>A new instance of <see cref="DomainPolicyResult{TValue}" /> representing a successful result.</returns>
    public static new DomainPolicyResult<TValue> Success()
    {
        return new DomainPolicyResult<TValue>();
    }

    /// <summary>
    ///     Creates a failed DomainPolicyResult.
    /// </summary>
    /// <returns>A new DomainPolicyResult with IsSuccess set to false.</returns>
    public static new DomainPolicyResult<TValue> Failure()
    {
        return new DomainPolicyResult<TValue> { IsSuccess = false };
    }

    /// <summary>
    ///     Adds a message to the result.
    /// </summary>
    /// <param name="message">The message to be added.</param>
    /// <returns>The current instance of <see cref="DomainPolicyResult{TValue}" /> with the message added.</returns>
    public new DomainPolicyResult<TValue> WithMessage(string message)
    {
        base.WithMessage(message);
        return this;
    }

    /// <summary>
    ///     Adds a collection of messages to the DomainPolicyResult.
    /// </summary>
    /// <param name="messages">The collection of messages to be added.</param>
    /// <returns>The updated DomainPolicyResult object with the additional messages.</returns>
    public new DomainPolicyResult<TValue> WithMessages(IEnumerable<string> messages)
    {
        base.WithMessages(messages);
        return this;
    }

    /// <summary>
    ///     Adds an error to the result.
    /// </summary>
    /// <param name="error">The error to be added.</param>
    /// <returns>The updated DomainPolicyResult instance with the added error.</returns>
    public new DomainPolicyResult<TValue> WithError(IResultError error)
    {
        base.WithError(error);
        return this;
    }

    /// <summary>
    ///     Adds a collection of errors to the current DomainPolicyResult instance.
    /// </summary>
    /// <param name="errors">The collection of IResultError instances to add to the result.</param>
    /// <returns>The updated DomainPolicyResult instance containing the added errors.</returns>
    public new DomainPolicyResult<TValue> WithErrors(IEnumerable<IResultError> errors)
    {
        base.WithErrors(errors);
        return this;
    }

    /// <summary>
    ///     Associates the given policy results with the current DomainPolicyResult instance.
    /// </summary>
    /// <param name="results">The DomainPolicyResults to be associated.</param>
    /// <returns>The current DomainPolicyResult instance with the specified policy results.</returns>
    public DomainPolicyResult<TValue> WithPolicyResults(DomainPolicyResults<TValue> results)
    {
        this.PolicyResults = results;
        return this;
    }
}