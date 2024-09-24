// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
///     Represents the result of an operation, providing information about messages, errors, and success status.
/// </summary>
public interface IResult
{
    /// <summary>
    ///     Gets a read-only list of messages associated with the result.
    ///     These messages can provide additional context or information about the result.
    /// </summary>
    IReadOnlyList<string> Messages { get; }

    /// <summary>
    ///     Gets a read-only list of errors associated with the result.
    /// </summary>
    /// <remarks>
    ///     Errors provide detailed information about the issues encountered
    ///     during the operation which resulted in this result object.
    /// </remarks>
    IReadOnlyList<IResultError> Errors { get; }

    /// <summary>
    ///     Indicates whether the operation represented by the result was successful.
    ///     If true, the operation completed successfully.
    ///     If false, the operation encountered one or more errors.
    /// </summary>
    bool IsSuccess { get; }

    /// <summary>
    ///     Indicates whether the result of an operation represents a failure.
    /// </summary>
    /// <remarks>
    ///     This property returns the inverse of the <see cref="IsSuccess" /> property.
    ///     If <see cref="IsSuccess" /> is false, this property will return true, indicating a failure.
    /// </remarks>
    bool IsFailure { get; }

    /// <summary>
    ///     Adds a message to the Result's Messages list.
    /// </summary>
    /// <param name="message">The message to add. It should be a non-null and non-whitespace string.</param>
    /// <returns>Returns the current Result instance.</returns>
    Result WithMessage(string message);

    /// <summary>
    ///     Adds an error to the current result instance and marks the result as unsuccessful.
    /// </summary>
    /// <param name="error">The error to be added to the result.</param>
    /// <return>The current result instance with the error added.</return>
    Result WithError(IResultError error);

    /// <summary>
    ///     Adds a new instance of the specified error type to the result and marks it as a failure.
    /// </summary>
    /// <typeparam name="TError">The type of error to add.</typeparam>
    /// <return>An instance of <see cref="Result" /> with the specified error added and marked as failure.</return>
    Result WithError<TError>()
        where TError : IResultError, new();

    /// <summary>
    ///     Appends multiple messages to the current <see cref="Result" /> instance.
    /// </summary>
    /// <param name="messages">An enumerable collection of messages to add.</param>
    /// <returns>The current <see cref="Result" /> instance with the added messages.</returns>
    Result WithMessages(IEnumerable<string> messages);

    /// <summary>
    ///     Appends a collection of <see cref="IResultError" /> objects to the current result instance.
    /// </summary>
    /// <param name="errors">The collection of <see cref="IResultError" /> objects to be added.</param>
    /// <returns>The current instance of <see cref="Result" /> with the errors added.</returns>
    Result WithErrors(IEnumerable<IResultError> errors);

    /// <summary>
    ///     Determines whether the result contains any errors.
    /// </summary>
    /// <returns>True if the result contains any errors; otherwise, false.</returns>
    bool HasError();

    /// <summary>
    ///     Determines if the current <see cref="Result" /> contains any errors.
    /// </summary>
    /// <returns>
    ///     A boolean value indicating whether any errors are present in the result.
    /// </returns>
    bool HasError<TError>()
        where TError : IResultError;

    /// <summary>
    ///     Checks if the result contains any errors.
    /// </summary>
    /// <param name="result">An output parameter that will contain all the errors found in the result if any exist.</param>
    /// <typeparam name="TError">The type of error to check for, which must implement IResultError.</typeparam>
    /// <returns>True if the result contains one or more errors of the specified type; otherwise, false.</returns>
    bool HasError<TError>(out IEnumerable<IResultError> result)
        where TError : IResultError;
}

/// <summary>
///     Represents the result of an operation, encapsulating messages, errors,
///     and the success or failure state.
/// </summary>
public interface IResult<out TValue> : IResult
{
    /// <summary>
    ///     Gets the value associated with the result.
    /// </summary>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    TValue Value { get; }
}