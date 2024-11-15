// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Commands;

/// <summary>
/// Provides factory methods to create command responses based on the Result type.
/// </summary>
public static class CommandResult
{
    /// <summary>
    /// Creates a command response with a success result.
    /// </summary>
    /// <returns>A command response containing a success result.</returns>
    public static CommandResponse<Result> For()
    {
        return new CommandResponse<Result> { Result = Result.Success() };
    }

    /// <summary>
    /// Creates a new CommandResponse containing a default success result of type Result&lt;TValue&gt;.
    /// </summary>
    /// <typeparam name="TValue">The type of the value contained in the result.</typeparam>
    /// <returns>
    /// A CommandResponse containing a success Result of type Result&lt;TValue&gt;.
    /// </returns>
    public static CommandResponse<Result<TValue>> For<TValue>()
    {
        return new CommandResponse<Result<TValue>> { Result = Result<TValue>.Success() };
    }

    /// <summary>
    /// Creates a CommandResponse containing a successful Result of the specified value.
    /// </summary>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="value">The value to be included in the successful Result.</param>
    /// <returns>A CommandResponse containing a successful Result of the specified value.</returns>
    public static CommandResponse<Result<TValue>> For<TValue>(TValue value)
    {
        return new CommandResponse<Result<TValue>> { Result = Result<TValue>.Success(value) };
    }

    /// <summary>
    /// Creates a command response for the given result.
    /// </summary>
    /// <param name="result">The result to be used in the command response.</param>
    /// <returns>A command response containing the given result.</returns>
    public static CommandResponse<Result> For(Result result)
    {
        return new CommandResponse<Result> { Result = result };
    }

    /// <summary>
    /// Creates a <see cref="CommandResponse{TResult}"/> with the specified result.
    /// </summary>
    /// <typeparam name="TValue">The type of value held by the result.</typeparam>
    /// <param name="result">The result to initialize the command response with.</param>
    /// <returns>A <see cref="CommandResponse{TResult}"/> initialized with the specified result.</returns>
    public static CommandResponse<Result<TValue>> For<TValue>(Result<TValue> result)
    {
        return new CommandResponse<Result<TValue>> { Result = result };
    }

    // public static async Task<CommandResponse<Result<TValue>>> ForAsync<TValue>(
    //     Func<CancellationToken, Task<Result<TValue>>> operation, CancellationToken cancellationToken = default)
    // {
    //     return new CommandResponse<Result<TValue>> { Result = await operation(cancellationToken) };
    // }

    /// <summary>
    /// Creates a command response for a given result and output value.
    /// </summary>
    /// <typeparam name="TValue">The type of the input result value.</typeparam>
    /// <typeparam name="TOuput">The type of the output value.</typeparam>
    /// <param name="result">The result object to create the response for.</param>
    /// <param name="value">The output value for the response, default is used if not provided.</param>
    /// <returns>A command response containing the mapped result with the specified output value.</returns>
    public static CommandResponse<Result<TOuput>> For<TValue, TOuput>(Result<TValue> result, TOuput value = default)
    {
        return new CommandResponse<Result<TOuput>> { Result = result.For(value) };
    }

    /// Creates a <see cref="CommandResponse{Result}"/> object indicating a successful operation.
    /// <return>
    /// A <see cref="CommandResponse{Result}"/> object with the result set to success.
    /// </return>
    public static CommandResponse<Result> Success()
    {
        return new CommandResponse<Result> { Result = Common.Result.Success() };
    }

    /// <summary>
    /// Creates a successful command response with the specified message.
    /// </summary>
    /// <param name="message">The success message to include in the response.</param>
    /// <returns>A command response indicating success, containing the provided message.</returns>
    public static CommandResponse<Result> Success(string message)
    {
        return new CommandResponse<Result> { Result = Common.Result.Success(message) };
    }

    /// <summary>
    /// Generates a success response containing the provided messages.
    /// </summary>
    /// <param name="messages">A collection of success messages to be included in the response.</param>
    /// <returns>A CommandResponse object containing a Result with the success messages.</returns>
    public static CommandResponse<Result> Success(IEnumerable<string> messages)
    {
        return new CommandResponse<Result> { Result = Common.Result.Success(messages) };
    }

    /// <summary>
    /// Creates a successful CommandResponse with a default value of type TResult.
    /// </summary>
    /// <typeparam name="TValue">The type of the result value.</typeparam>
    /// <returns>A CommandResponse object with a successful result of type TValue.</returns>
    public static CommandResponse<Result<TValue>> Success<TValue>()
    {
        return new CommandResponse<Result<TValue>> { Result = Result<TValue>.Success() };
    }

    /// <summary>
    /// Generates a successful CommandResponse containing the specified value wrapped in a Result object.
    /// </summary>
    /// <typeparam name="TValue">The type of the value being wrapped and returned.</typeparam>
    /// <param name="value">The value to be wrapped in a Result object and included in the CommandResponse.</param>
    /// <returns>A CommandResponse containing a Result object that encapsulates the specified value.</returns>
    public static CommandResponse<Result<TValue>> Success<TValue>(TValue value)
    {
        return new CommandResponse<Result<TValue>> { Result = Result<TValue>.Success(value) };
    }

    /// <summary>
    /// Generates a successful command response containing both a value and a message.
    /// </summary>
    /// <param name="value">The value to be included in the command response.</param>
    /// <param name="message">The message to be included in the command response.</param>
    /// <typeparam name="TValue">The type of the value to be included in the command response.</typeparam>
    /// <returns>A <see cref="CommandResponse{Result{TValue}}"/> instance representing the successful command response.</returns>
    public static CommandResponse<Result<TValue>> Success<TValue>(TValue value, string message)
    {
        return new CommandResponse<Result<TValue>> { Result = Result<TValue>.Success(value, message) };
    }

    /// <summary>
    /// Creates a success command response with messages for a specified value.
    /// </summary>
    /// <typeparam name="TValue">The type of the returned value.</typeparam>
    /// <param name="value">The value for the success result.</param>
    /// <param name="messages">Associated success messages.</param>
    /// <returns>A CommandResponse with Result containing the specified value and messages.</returns>
    public static CommandResponse<Result<TValue>> Success<TValue>(TValue value, IEnumerable<string> messages)
    {
        return new CommandResponse<Result<TValue>> { Result = Result<TValue>.Success(value, messages) };
    }

    /// <summary>
    /// Generates a failure response.
    /// </summary>
    /// <returns>A CommandResponse containing a failure result.</returns>
    public static CommandResponse<Result> Failure()
    {
        return new CommandResponse<Result> { Result = Common.Result.Failure() };
    }

    /// <summary>
    /// Creates a <see cref="CommandResponse{Result}"/> indicating failure.
    /// </summary>
    /// <param name="message">The failure message to be conveyed.</param>
    /// <param name="error">An optional object implementing <see cref="IResultError"/> which provides additional error information.</param>
    /// <returns>A <see cref="CommandResponse{Result}"/> object encapsulating the failure result.</returns>
    public static CommandResponse<Result> Failure(string message, IResultError error = default)
    {
        return new CommandResponse<Result> { Result = Common.Result.Failure(message, error) };
    }

    /// <summary>
    /// Generates a failure response with the specified messages and errors.
    /// </summary>
    /// <param name="messages">A collection of strings describing the failure reasons.</param>
    /// <param name="errors">A collection of error objects providing additional failure details (optional).</param>
    /// <returns>A CommandResponse object containing a failure result with the provided messages and errors.</returns>
    public static CommandResponse<Result> Failure(
        IEnumerable<string> messages,
        IEnumerable<IResultError> errors = default)
    {
        return new CommandResponse<Result> { Result = Common.Result.Failure(messages, errors) };
    }

    /// Generates a failure response with a specific error type.
    /// This method creates a CommandResponse object that contains a Result indicating failure and an optional error message.
    /// <param name="message">An optional error message describing the failure. Default is null.</param> <typeparam name="TError">The type of the error implementing the IResultError interface.</typeparam> <return>A CommandResponse object containing the failure Result.</return>
    /// /
    public static CommandResponse<Result> Failure<TError>(string message = null)
        where TError : IResultError, new()
    {
        return new CommandResponse<Result> { Result = Common.Result.Failure<TError>(message) };
    }

    /// <summary>
    /// Generates a command response indicating a failure.
    /// </summary>
    /// <typeparam name="TError">The type of the error information.</typeparam>
    /// <param name="messages">A collection of error messages explaining the failure.</param>
    /// <returns>A <see cref="CommandResponse{Result}"/> instance representing the failure response.</returns>
    public static CommandResponse<Result> Failure<TError>(IEnumerable<string> messages)
        where TError : IResultError, new()
    {
        return new CommandResponse<Result> { Result = Common.Result.Failure<TError>(messages) };
    }

    /// Creates a failure CommandResponse with a specified value.
    /// <param name="value">The value to be associated with the failure result.</param>
    /// <typeparam name="TValue">The type of the value to be included in the failure result.</typeparam>
    /// <return>A CommandResponse containing a Result with the provided value.</return>
    public static CommandResponse<Result<TValue>> Failure<TValue>(TValue value)
    {
        return new CommandResponse<Result<TValue>> { Result = Result<TValue>.Failure(value) };
    }

    /// <summary>
    /// Creates a command response wrapping a failure result with a specified value, message, and optional error.
    /// </summary>
    /// <typeparam name="TValue">The type of the value associated with the result.</typeparam>
    /// <param name="value">The value associated with the result.</param>
    /// <param name="message">The failure message.</param>
    /// <param name="error">An optional error instance implementing the IResultError interface.</param>
    /// <returns>A command response containing a failure result with the specified value, message, and error.</returns>
    public static CommandResponse<Result<TValue>> Failure<TValue>(
        TValue value,
        string message,
        IResultError error = default)
    {
        return new CommandResponse<Result<TValue>> { Result = Result<TValue>.Failure(value, message, error) };
    }

    /// <summary>
    /// Creates a failure command response with the specified value, messages, and errors.
    /// </summary>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="value">The value associated with the failure result.</param>
    /// <param name="messages">The collection of failure messages.</param>
    /// <param name="errors">The collection of errors associated with the failure result.</param>
    /// <returns>A <see cref="CommandResponse{TResult}"/> containing the failure result, messages, and errors.</returns>
    public static CommandResponse<Result<TValue>> Failure<TValue>(TValue value, IEnumerable<string> messages, IEnumerable<IResultError> errors = default)
    {
        return new CommandResponse<Result<TValue>> { Result = Result<TValue>.Failure(value, messages, errors) };
    }

    /// <summary>
    /// Creates a <see cref="CommandResponse{Result{TValue}}"/> indicating a failure with an optional message and error.
    /// </summary>
    /// <typeparam name="TValue">The type of the value for the response.</typeparam>
    /// <param name="message">An optional failure message to include in the response.</param>
    /// <param name="error">An optional error object implementing <see cref="IResultError"/>.</param>
    /// <returns>A <see cref="CommandResponse{Result{TValue}}"/> indicating a failure.</returns>
    public static CommandResponse<Result<TValue>> Failure<TValue>(string message = null, IResultError error = default)
    {
        return new CommandResponse<Result<TValue>> { Result = Result<TValue>.Failure(message, error) };
    }

    /// <summary>
    /// Creates a failed command response with the given value and optional error message.
    /// </summary>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <typeparam name="TError">The type of the error, which must implement IResultError and have a parameterless constructor.</typeparam>
    /// <param name="value">The value of the result.</param>
    /// <param name="message">An optional error message.</param>
    /// <returns>A CommandResponse with a Result containing the failure, value, and error message.</returns>
    public static CommandResponse<Result<TValue>> Failure<TValue, TError>(TValue value = default, string message = null)
        where TError : IResultError, new()
    {
        return new CommandResponse<Result<TValue>> { Result = Result<TValue>.Failure<TError>(message) };
    }
}