// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Queries;

/// <summary>
/// Provides factory methods for creating Result-based query responses.
/// </summary>
public static class QueryResult
{
    /// <summary>
    /// Generates a successful <see cref="QueryResponse{Result}"/> object.
    /// </summary>
    /// <returns>A <see cref="QueryResponse{Result}"/> instance with a success result.</returns>
    public static QueryResponse<Result> For()
    {
        return new QueryResponse<Result> { Result = Result.Success() };
    }

    /// <summary>
    /// Creates a QueryResponse containing a successful Result object with the specified generic type value.
    /// </summary>
    /// <typeparam name="TValue">The type of the value for the Result object.</typeparam>
    /// <returns>A QueryResponse containing a successful Result object of type TValue.</returns>
    public static QueryResponse<Result<TValue>> For<TValue>()
    {
        return new QueryResponse<Result<TValue>> { Result = Result<TValue>.Success() };
    }

    /// <summary>
    /// Creates a new QueryResponse object encapsulating a successful Result object with the given value.
    /// </summary>
    /// <typeparam name="TValue">The type of the value to be encapsulated in the Result object.</typeparam>
    /// <param name="value">The value to be encapsulated in the Result object.</param>
    /// <returns>A QueryResponse object containing a successful Result object with the provided value.</returns>
    public static QueryResponse<Result<TValue>> For<TValue>(TValue value)
    {
        return new QueryResponse<Result<TValue>> { Result = Result<TValue>.Success(value) };
    }

    /// <summary>
    /// Creates a query response containing the given result object.
    /// </summary>
    /// <typeparam name="TValue">The type of the value contained in the result.</typeparam>
    /// <param name="result">The result to include in the query response.</param>
    /// <returns>A QueryResponse object containing the given result.</returns>
    public static QueryResponse<Result<TValue>> For<TValue>(Result result)
    {
        return new QueryResponse<Result<TValue>> { Result = result };
    }

    /// <summary>
    /// Creates a <see cref="QueryResponse{Result{TValue}}"/> from a given <see cref="Result{TValue}"/>.
    /// </summary>
    /// <typeparam name="TValue">The type of the value contained in the result.</typeparam>
    /// <param name="result">The result to wrap in a <see cref="QueryResponse{Result{TValue}}"/>.</param>
    /// <returns>A <see cref="QueryResponse{Result{TValue}}"/> containing the given result.</returns>
    public static QueryResponse<Result<TValue>> For<TValue>(Result<TValue> result)
    {
        return new QueryResponse<Result<TValue>> { Result = result };
    }

    /// <summary>
    /// Creates a <see cref="QueryResponse{Result{TValue}}"/> from a given <see cref="Result{TValue}"/>.
    /// </summary>
    /// <typeparam name="TValue">The type of the value contained in the result.</typeparam>
    /// <param name="result">The result to wrap in a <see cref="QueryResponse{Result{TValue}}"/>.</param>
    /// <returns>A <see cref="QueryResponse{Result{TValue}}"/> containing the given result.</returns>
    public static QueryResponse<ResultPaged<TValue>> For<TValue>(ResultPaged<TValue> result)
    {
        return new QueryResponse<ResultPaged<TValue>> { Result = result };
    }

    /// <summary>
    /// Creates a QueryResponse with a Result containing a new output value mapped from the provided Result and TValue.
    /// </summary>
    /// <param name="result">The original Result containing a TValue.</param>
    /// <param name="value">The output value to map from the Result.</param>
    /// <typeparam name="TValue">The type of the original result value.</typeparam>
    /// <typeparam name="TOuput">The type of the output value.</typeparam>
    /// <returns>A QueryResponse containing a Result with the new output value.</returns>
    public static QueryResponse<Result<TOuput>> For<TValue, TOuput>(Result<TValue> result, TOuput value = default)
    {
        return new QueryResponse<Result<TOuput>> { Result = result.ToResult(value) };
    }

    /// <summary>
    /// Creates a successful query response with a result of type <typeparamref name="TValue"/>.
    /// </summary>
    /// <typeparam name="TValue">The type of the result value.</typeparam>
    /// <returns>A successful <see cref="QueryResponse{TValue}"/> with a result of type <typeparamref name="TValue"/>.</returns>
    public static QueryResponse<Result<TValue>> Success<TValue>()
    {
        return new QueryResponse<Result<TValue>> { Result = Result<TValue>.Success() };
    }

    /// <summary>
    /// Creates a successful query response containing the provided value.
    /// </summary>
    /// <typeparam name="TValue">The type of the value being returned.</typeparam>
    /// <param name="value">The value to be included in the result of the query response.</param>
    /// <returns>A query response containing a success result with the provided value.</returns>
    public static QueryResponse<Result<TValue>> Success<TValue>(TValue value)
    {
        return new QueryResponse<Result<TValue>> { Result = Result<TValue>.Success(value) };
    }

    /// <summary>
    /// Creates a successful query response containing the given value and a message.
    /// </summary>
    /// <typeparam name="TValue">The type of the value in the query response.</typeparam>
    /// <param name="value">The value to include in the query response.</param>
    /// <param name="message">A message describing the successful result.</param>
    /// <returns>A QueryResponse containing a Result with the provided value and message.</returns>
    public static QueryResponse<Result<TValue>> Success<TValue>(TValue value, string message)
    {
        return new QueryResponse<Result<TValue>> { Result = Result<TValue>.Success(value, message) };
    }

    /// <summary>
    /// Generates a successful query response containing the specified value and a collection of messages.
    /// </summary>
    /// <typeparam name="TValue">The type of value contained in the query response.</typeparam>
    /// <param name="value">The value to include in the result.</param>
    /// <param name="messages">A collection of messages detailing information about the result.</param>
    /// <returns>A query response containing a successful result with the specified value and messages.</returns>
    public static QueryResponse<Result<TValue>> Success<TValue>(TValue value, IEnumerable<string> messages)
    {
        return new QueryResponse<Result<TValue>> { Result = Result<TValue>.Success(value, messages) };
    }

    /// <summary>
    /// Creates a QueryResponse object with a failure Result of the specified type.
    /// </summary>
    /// <typeparam name="TValue">The type of the value contained in the Result.</typeparam>
    /// <returns>A QueryResponse object containing a failure Result.</returns>
    public static QueryResponse<Result<TValue>> Failure<TValue>()
    {
        return new QueryResponse<Result<TValue>> { Result = Result<TValue>.Failure() };
    }

    /// <summary>
    /// Creates a failure query response containing the specified value.
    /// </summary>
    /// <typeparam name="TValue">The type of the value to be included in the result.</typeparam>
    /// <param name="value">The value to be included in the result.</param>
    /// <returns>A query response containing a failure result with the specified value.</returns>
    public static QueryResponse<Result<TValue>> Failure<TValue>(TValue value)
    {
        return new QueryResponse<Result<TValue>> { Result = Result<TValue>.Failure(value) };
    }

    /// <summary>
    /// Creates a failure query response with the specified value, message, and error.
    /// </summary>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="value">The value associated with the failure.</param>
    /// <param name="message">The failure message.</param>
    /// <param name="error">The error information associated with the failure, if any.</param>
    /// <returns>A query response containing the failure result.</returns>
    public static QueryResponse<Result<TValue>> Failure<TValue>(TValue value, string message, IResultError error = default)
    {
        return new QueryResponse<Result<TValue>> { Result = Result<TValue>.Failure(value, message, error) };
    }

    /// <summary>
    /// Creates an instance of <see cref="QueryResponse{TValue}"/> containing a failure result
    /// with the provided value, error messages, and error details.
    /// </summary>
    /// <param name="value">The value associated with the failure result.</param>
    /// <param name="messages">A collection of error messages providing details about the failure.</param>
    /// <param name="errors">A collection of <see cref="IResultError"/> instances providing additional error details. Defaults to null.</param>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <returns>A <see cref="QueryResponse{TValue}"/> containing a failure result with the specified value, messages, and errors.</returns>
    public static QueryResponse<Result<TValue>> Failure<TValue>(TValue value, IEnumerable<string> messages, IEnumerable<IResultError> errors = null)
    {
        return new QueryResponse<Result<TValue>> { Result = Result<TValue>.Failure(value, messages, errors) };
    }

    /// <summary>
    /// Creates a query response indicating a failure, with a specified error message.
    /// </summary>
    /// <typeparam name="TValue">The type of the result value.</typeparam>
    /// <param name="message">The error message associated with the failure.</param>
    /// <returns>A query response containing a failure result with the specified error message.</returns>
    public static QueryResponse<Result<TValue>> Failure<TValue>(string message)
    {
        return new QueryResponse<Result<TValue>> { Result = Result<TValue>.Failure(message) };
    }

    /// <summary>
    /// Generates a failed query response containing a message and an error.
    /// </summary>
    /// <typeparam name="TValue">The type of the value associated with the failed result.</typeparam>
    /// <param name="message">The failure message to include in the response.</param>
    /// <param name="error">An optional error object associated with the failure.</param>
    /// <returns>A query response containing the failed result with the specified message and error.</returns>
    public static QueryResponse<Result<TValue>> Failure<TValue>(string message = null, IResultError error = default)
    {
        return new QueryResponse<Result<TValue>> { Result = Result<TValue>.Failure(message, error) };
    }

    /// <summary>
    /// Creates a query response with a failed result.
    /// </summary>
    /// <typeparam name="TValue">The type of the result.</typeparam>
    /// <param name="messages">A collection of messages describing the failure.</param>
    /// <param name="errors">A collection of errors associated with the failure.</param>
    /// <returns>A query response containing the failure result and associated metadata.</returns>
    public static QueryResponse<Result<TValue>> Failure<TValue>(IEnumerable<string> messages, IEnumerable<IResultError> errors = default)
    {
        return new QueryResponse<Result<TValue>> { Result = Result<TValue>.Failure(messages, errors) };
    }

    /// <summary>
    /// Creates a query response indicating a failure with a specified value, message, and error.
    /// </summary>
    /// <typeparam name="TValue">Type of the value associated with the result.</typeparam>
    /// <typeparam name="TError">Type of the error associated with the result, implementing IResultError.</typeparam>
    /// <param name="value">The value to be associated with the result.</param>
    /// <param name="message">An optional message describing the failure.</param>
    /// <param name="error">An optional error instance providing additional failure details.</param>
    /// <returns>A <see cref="QueryResponse{Result{TValue}}"/> indicating failure with the provided value, message, and error.</returns>
    public static QueryResponse<Result<TValue>> Failure<TValue, TError>(TValue value = default, string message = null, IResultError error = null)
        where TError : IResultError, new()
    {
        return new QueryResponse<Result<TValue>>
        {
            Result = Result<TValue>.Failure(value)
                .WithMessage(message).WithError(error)
        };
    }
}

public static class QueryResponse
{
    public static QueryResponse<TResult> For<TResult>()
    {
        return new QueryResponse<TResult>();
    }

    public static QueryResponse<TResult> For<TResult>(TResult result)
    {
        return new QueryResponse<TResult> { Result = result };
    }
}