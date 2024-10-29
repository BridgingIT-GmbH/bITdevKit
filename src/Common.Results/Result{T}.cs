// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
///     Represents a basic result indicating success or failure.
/// </summary>
public partial class Result<T> : IResult<T>
{
    protected readonly List<IResultError> errors = [];
    protected readonly List<string> messages = [];
    protected bool success = true;

    // /// <summary>
    // ///     Initializes a new instance of the <see cref="Result" /> class.
    // ///     Represents the outcome of an operation, capturing success, failure, associated messages, and errors.
    // /// </summary>
    public Result() { } // needs to be public for mapster

    /// <summary>
    ///     Implicitly converts a Result{TValue} to a boolean value based on its success state.
    /// </summary>
    /// <param name="result">The Result to convert.</param>
    /// <returns>True if the Result is successful; otherwise, false.</returns>
    /// <example>
    /// <code>
    /// Result{int} result = Result{int}.Success(42);
    /// if (result) // Implicitly converts to true
    /// {
    ///     // Handle success
    /// }
    /// </code>
    /// </example>
    public static implicit operator bool(Result<T> result) => result.Match(true, false);

    /// <summary>
    ///     Implicitly converts a Result{TValue} to a non-generic Result,
    ///     preserving the success state, messages, and errors. However losing it's value
    /// </summary>
    /// <param name="result">The generic Result to convert.</param>
    public static implicit operator Result(Result<T> result) =>
        result?.Match(
            _ => Result.Success().WithMessages(result.Messages).WithErrors(result.Errors),
            _ => Result.Failure().WithMessages(result.Messages).WithErrors(result.Errors));

    /// <summary>
    ///     Implicitly converts a non-generic Result to a Result{TValue},
    ///     preserving the success state, messages, and errors.
    /// </summary>
    /// <param name="result">The generic Result to convert.</param>
    public static implicit operator Result<T>(Result result) =>
        result?.Match(
            () => Success().WithMessages(result.Messages).WithErrors(result.Errors),
            _ => Failure().WithMessages(result.Messages).WithErrors(result.Errors));


    /// <summary>
    ///     Gets a read-only list of messages associated with the result.
    /// </summary>
    public IReadOnlyList<string> Messages => this.messages;

    /// <summary>
    ///     A read-only list that stores errors related to the result operation.
    /// </summary>
    public IReadOnlyList<IResultError> Errors => this.errors;

    /// <summary>
    ///     Gets or initializes a value indicating whether the operation was successful.
    /// </summary>
    public bool IsSuccess
    {
        get => this.success;
        protected init => this.success = value;
    }

    /// <summary>
    ///     Gets a value indicating whether the result operation has failed.
    ///     This property returns true if the result was not successful.
    /// </summary>
    public bool IsFailure => !this.success;

    /// <summary>
    ///     Determines if the current <see cref="Result" /> contains any errors.
    /// </summary>
    /// <returns>
    ///     A boolean value indicating whether any errors are present in the result.
    /// </returns>
    public bool HasError<TError>()
        where TError : IResultError
    {
        var errorType = typeof(TError);

        return this.errors.Find(e => e.GetType() == errorType) is not null;
    }

    /// <summary>
    ///     Determines whether the result contains any errors.
    /// </summary>
    /// <returns>True if the result contains any errors; otherwise, false.</returns>
    public bool HasError()
    {
        return this.errors.Count != 0;
    }

    /// <summary>
    ///     Checks if the result contains any errors.
    /// </summary>
    /// <param name="result">An output parameter that will contain all the errors found in the result if any exist.</param>
    /// <typeparam name="TError">The type of error to check for, which must implement IResultError.</typeparam>
    /// <returns>True if the result contains one or more errors of the specified type; otherwise, false.</returns>
    public bool HasError<TError>(out IEnumerable<IResultError> result)
        where TError : IResultError
    {
        var errorType = typeof(TError);
        result = this.errors.Where(e => e.GetType() == errorType);

        return result.Any();
    }

    /// <summary>
    ///     Returns a string that represents the current Result object.
    /// </summary>
    /// <returns>A string that represents the current Result object, including its state, messages, and detailed errors.</returns>
    public override string ToString()
    {
        var stringBuilder = new StringBuilder();
        stringBuilder.AppendLine($"Success: {this.IsSuccess}");

        if (this.messages.Count != 0)
        {
            stringBuilder.AppendLine("Messages:");
            foreach (var message in this.Messages)
            {
                stringBuilder.AppendLine($"- {message}".Trim());
            }
        }

        if (this.errors.Count != 0)
        {
            stringBuilder.AppendLine("Errors:");
            foreach (var error in this.Errors)
            {
                stringBuilder.AppendLine($"- [{error.GetType().Name}] {error.Message}".Trim());
            }
        }

        return stringBuilder.ToString().TrimEnd();
    }

    /// <summary>
    ///     Gets or sets the value of the result.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    public T Value { get; protected init; }

    /// <summary>
    ///     Creates a new instance of <see cref="Result{TValue}" /> with a failure status.
    /// </summary>
    /// <returns>A <see cref="Result{TValue}" /> instance with IsSuccess set to false.</returns>
    public static Result<T> Failure()
    {
        return new Result<T> { IsSuccess = false };
    }

    /// <summary>
    ///     Creates a new failed Result object with default error information.
    /// </summary>
    /// <typeparam name="TError">The type of the error to associate with the Result.</typeparam>
    /// <returns>A new Result object with IsSuccess set to false and an associated error of type TError.</returns>
    public static Result<T> Failure<TError>()
        where TError : IResultError, new()
    {
        return new Result<T> { IsSuccess = false }
            .WithError<TError>();
    }

    /// <summary>
    ///     Creates a failure result with the specified value.
    /// </summary>
    /// <typeparam name="T">The type of the value associated with the result.</typeparam>
    /// <param name="value">The value associated with the result.</param>
    /// <returns>A new failure result containing the specified value.</returns>
    public static Result<T> Failure(T value)
    {
        return new Result<T> { IsSuccess = false, Value = value };
    }

    /// <summary>
    ///     Creates a new failure result with a specified message and optional error.
    /// </summary>
    /// <param name="message">The failure message.</param>
    /// <param name="error">An optional error object that provides additional information about the failure.</param>
    /// <returns>A new failure result containing the message and optional error.</returns>
    public static Result<T> Failure(string message, IResultError error = null)
    {
        return new Result<T> { IsSuccess = false }
            .WithMessage(message).WithError(error);
    }

    /// <summary>
    ///     Creates a failure result with the specified value, message, and error.
    /// </summary>
    /// <param name="value">The value to associate with the failure result.</param>
    /// <param name="message">A message describing the failure.</param>
    /// <param name="error">An optional error object associated with the failure.</param>
    /// <returns>A failure result containing the specified value, message, and error.</returns>
    public static Result<T> Failure(T value, string message, IResultError error = null)
    {
        return new Result<T> { IsSuccess = false, Value = value }
            .WithMessage(message).WithError(error);
    }

    /// <summary>
    ///     Creates a failure Result object.
    /// </summary>
    /// <returns>A Result object representing a failure state.</returns>
    public static Result<T> Failure(
        T value,
        IEnumerable<string> messages,
        IEnumerable<IResultError> errors = null)
    {
        return new Result<T> { IsSuccess = false, Value = value }
            .WithMessages(messages).WithErrors(errors);
    }

    /// <summary>
    ///     Creates a failure result.
    /// </summary>
    /// <returns>A new instance of <see cref="Result{TValue}" /> with <see cref="Result.IsSuccess" /> set to <c>false</c>.</returns>
    public static Result<T> Failure<TError>(string message)
        where TError : IResultError, new()
    {
        return new Result<T> { IsSuccess = false }
            .WithMessage(message).WithError<TError>();
    }

    /// <summary>
    ///     Returns a failure <see cref="Result{TValue}" /> object with the specified error type, value, and message.
    /// </summary>
    /// <typeparam name="TError">The type of error that implements <see cref="IResultError" />.</typeparam>
    /// <param name="value">The value to associate with the result.</param>
    /// <param name="message">The message describing the result.</param>
    /// <returns>A failure result with the specified value and message.</returns>
    public static Result<T> Failure<TError>(T value, string message)
        where TError : IResultError, new()
    {
        return new Result<T> { IsSuccess = false, Value = value }
            .WithMessage(message).WithError<TError>();
    }

    /// <summary>
    ///     Creates a failure Result object that includes the specified messages and errors.
    /// </summary>
    /// <param name="messages">A collection of error messages to be included in the result.</param>
    /// <param name="errors">A collection of error objects to be included in the result. Default is null.</param>
    /// <returns>A Result object indicating failure, containing the provided messages and errors.</returns>
    public static Result<T> Failure(IEnumerable<string> messages, IEnumerable<IResultError> errors = null)
    {
        return new Result<T> { IsSuccess = false }
            .WithMessages(messages).WithErrors(errors);
    }

    /// <summary>
    ///     Creates a new Result instance representing a failure.
    /// </summary>
    /// <returns>A Result instance configured as a failure.</returns>
    public static Result<T> Failure<TError>(IEnumerable<string> messages)
        where TError : IResultError, new()
    {
        return new Result<T> { IsSuccess = false }
            .WithMessages(messages).WithError<TError>();
    }

    /// <summary>
    ///     Creates a successful Result object with a default value.
    /// </summary>
    /// <returns>
    ///     A new instance of <see cref="Result{TValue}" /> representing success.
    /// </returns>
    public static Result<T> Success()
    {
        return new Result<T> { IsSuccess = true };
    }

    /// <summary>
    ///     Creates a new successful result.
    /// </summary>
    /// <returns>A new successful Result object.</returns>
    public static Result<T> Success(string message)
    {
        return new Result<T> { IsSuccess = true }.WithMessage(message);
    }

    /// <summary>
    ///     Creates a Result instance representing a successful operation.
    /// </summary>
    /// <returns>A Result instance indicating success.</returns>
    public static Result<T> Success(IEnumerable<string> messages)
    {
        return new Result<T> { IsSuccess = true }
            .WithMessages(messages);
    }

    /// <summary>
    ///     Creates a successful Result object with the provided value.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value for the result object.</param>
    /// <returns>A Result object indicating success with the provided value.</returns>
    public static Result<T> Success(T value)
    {
        return new Result<T> { IsSuccess = true, Value = value };
    }

    /// <summary>
    ///     Creates a successful <see cref="Result{TValue}" /> with the specified value and an optional message.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value to be associated with the result.</param>
    /// <param name="message">An optional message providing additional context or information.</param>
    /// <returns>A successful result containing the specified value and message.</returns>
    public static Result<T> Success(T value, string message)
    {
        return new Result<T> { IsSuccess = true, Value = value }
            .WithMessage(message);
    }

    /// <summary>
    ///     Represents an operation that returns a successful result with value and messages.
    /// </summary>
    /// <param name="value">The value to be returned as part of the success result.</param>
    /// <param name="messages">A collection of messages to be included in the success result.</param>
    /// <returns>A result object containing the value and messages indicating success.</returns>
    public static Result<T> Success(T value, IEnumerable<string> messages)
    {
        return new Result<T> { IsSuccess = true, Value = value}
            .WithMessages(messages);
    }

    /// <summary>
    ///     Adds a message to the result.
    /// </summary>
    /// <param name="message">The message to add.</param>
    /// <returns>The current result instance with the added message.</returns>
    public Result<T> WithMessage(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            this.messages.Add(message);
        }

        return this;
    }

    /// <summary>
    ///     Appends multiple messages to the result.
    /// </summary>
    /// <param name="messages">A collection of messages to add to the result.</param>
    /// <returns>A Result object with the added messages.</returns>
    public Result<T> WithMessages(IEnumerable<string> messages)
    {
        if (messages is null)
        {
            return this;
        }

        foreach (var message in messages)
        {
            this.WithMessage(message);
        }

        return this;
    }

    /// <summary>
    ///     Adds an error to the Result instance and sets the success flag to false.
    /// </summary>
    /// <param name="error">The error to add to the Result.</param>
    /// <returns>The updated Result instance.</returns>
    public Result<T> WithError(IResultError error)
    {
        if (error is null)
        {
            return this;
        }

        this.errors.Add(error);
        this.success = false;

        return this;
    }

    /// <summary>
    ///     Adds a collection of errors to the current result.
    /// </summary>
    /// <param name="errors">The collection of errors to add.</param>
    /// <returns>The current result instance with the added errors.</returns>
    public Result<T> WithErrors(IEnumerable<IResultError> errors)
    {
        if (errors is null)
        {
            return this;
        }

        foreach (var error in errors)
        {
            this.WithError(error);
        }

        return this;
    }

    /// <summary>
    ///     Adds an error of the specified type to the result and marks it as unsuccessful.
    /// </summary>
    /// <typeparam name="TError">The type of the error to add.</typeparam>
    /// <returns>The result instance with the error added.</returns>
    public Result<T> WithError<TError>()
        where TError : IResultError, new()
    {
        this.WithError(Activator.CreateInstance<TError>());
        this.success = false;

        return this;
    }

    /// <summary>
    ///     Converts a generic Result{TValue} to a non-generic Result.
    /// </summary>
    /// <typeparam name="T">The type for the generic Result.</typeparam>
    /// <returns>A generic Result with the same success state, messages, and errors.</returns>
    /// <example>
    /// <code>
    /// var userResult = Result{User}.Success(user)
    ///     .WithMessage("Operation completed");
    /// var result = result.For();
    /// </code>
    /// </example>
    public Result For()
    {
        return this; // implicit conversion
    }

    /// <summary>
    ///     Converts a generic Result{TValue} to a generic Result{TOutput} Result.
    /// </summary>
    /// <typeparam name="TOutput">The type for the generic Result.</typeparam>
    /// <returns>A generic Result with the same success state, messages, and errors.</returns>
    /// <example>
    /// <code>
    /// var userResult = Result{User}.Success(user)
    ///     .WithMessage("Operation completed");
    /// var result = result.For(customer);
    /// </code>
    /// </example>
    public Result<TOutput> For<TOutput>()
    {
        return this.For<TOutput>(default);
    }

    /// <summary>
    ///     Converts a generic Result{TValue} to a generic Result{TOutput} Result.
    /// </summary>
    /// <typeparam name="TOutput">The type for the generic Result.</typeparam>
    /// <returns>A generic Result with the same success state, messages, and errors.</returns>
    /// <example>
    /// <code>
    /// var userResult = Result{User}.Success(user)
    ///     .WithMessage("Operation completed");
    /// var result = result.For(customer);
    /// </code>
    /// </example>
    public Result<TOutput> For<TOutput>(TOutput value)
    {
        return this.Match(
            _ => Result<TOutput>.Success(value)
                .WithMessages(this.Messages).WithErrors(this.Errors),
            _ => Result<TOutput>.Failure(value)
                .WithMessages(this.Messages).WithErrors(this.Errors));
    }
}