// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
///     Represents the result of an operation, which can be either a success or a failure.
/// </summary>
public class Result : IResult
{
    /// <summary>
    ///     A list containing error details.
    /// </summary>
    protected readonly List<IResultError> errors = [];

    /// <summary>
    ///     A list that stores messages related to the result operation.
    /// </summary>
    protected readonly List<string> messages = [];

    /// <summary>
    ///     Indicates whether the result of an operation is successful.
    /// </summary>
    protected bool success = true;

    /// <summary>
    ///     Initializes a new instance of the <see cref="Result" /> class.
    ///     Represents the outcome of an operation, capturing success, failure, associated messages, and errors.
    /// </summary>
    protected Result() { }

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
        init => this.success = value;
    }

    /// <summary>
    ///     Gets a value indicating whether the result operation has failed.
    ///     This property returns true if the result was not successful.
    /// </summary>
    public bool IsFailure
    {
        get => !this.success;
        init => this.success = !value;
    }

    /// <summary>
    ///     Adds a message to the Result's Messages list.
    /// </summary>
    /// <param name="message">The message to add. It should be a non-null and non-whitespace string.</param>
    /// <return>Returns the current Result instance.</return>
    public Result WithMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return this;
        }

        this.messages.Add(message);

        return this;
    }

    /// <summary>
    ///     Appends multiple messages to the current <see cref="Result" /> instance.
    /// </summary>
    /// <param name="messages">An enumerable collection of messages to add.</param>
    /// <returns>The current <see cref="Result" /> instance with the added messages.</returns>
    public Result WithMessages(IEnumerable<string> messages)
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
    ///     Appends a collection of <see cref="IResultError" /> objects to the current result instance.
    /// </summary>
    /// <param name="errors">The collection of <see cref="IResultError" /> objects to be added.</param>
    /// <returns>The current instance of <see cref="Result" /> with the errors added.</returns>
    public Result WithErrors(IEnumerable<IResultError> errors)
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
    ///     Adds an error to the current result instance and marks the result as unsuccessful.
    /// </summary>
    /// <param name="error">The error to be added to the result.</param>
    /// <return>The current result instance with the error added.</return>
    public Result WithError(IResultError error)
    {
        if (error is not null)
        {
            this.errors.Add(error);
            this.success = false;
        }

        return this;
    }

    /// <summary>
    ///     Adds a specific error to the current Result instance and marks the result as a failure.
    ///     This method is useful for associating a strongly-typed error with the outcome of an operation.
    ///     The generic parameter TError should implement the IResultError interface and have a parameterless constructor.
    /// </summary>
    /// <typeparam name="TError">The type of error that implements IResultError.</typeparam>
    /// <returns>The current Result instance, with the error added and IsSuccess set to false.</returns>
    public Result WithError<TError>()
        where TError : IResultError, new()
    {
        this.WithError(Activator.CreateInstance<TError>());
        this.success = false;

        return this;
    }

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
        return this.Errors.Any();
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

        if (this.Messages.Any())
        {
            stringBuilder.AppendLine("Messages:");
            foreach (var message in this.Messages)
            {
                stringBuilder.AppendLine($"- {message}".Trim());
            }
        }

        if (this.Errors.Any())
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
    ///     Creates a new failed result.
    /// </summary>
    /// <returns>A new instance of <see cref="Result" /> with a failure state.</returns>
#pragma warning disable SA1204
    public static Result Failure()
#pragma warning restore SA1204
    {
        return new Result { IsSuccess = false };
    }

    //public static Result Failure<TValue>()
    //{
    //    return new Result { IsSuccess = false };
    //}

    /// <summary>
    ///     Creates a new instance of the <see cref="Result" /> class representing a failure.
    /// </summary>
    /// <returns>
    ///     A new <see cref="Result" /> instance with success set to false.
    /// </returns>
    public static Result Failure<TError>()
        where TError : IResultError, new()
    {
        return new Result { IsSuccess = false }.WithError<TError>();
    }

    /// <summary>
    ///     Creates a failed Result instance with the specified message and optional error.
    /// </summary>
    /// <param name="message">The failure message to be included in the Result.</param>
    /// <param name="error">An optional IResultError instance to be included in the Result.</param>
    /// <returns>A Result instance representing a failure with the provided message and error.</returns>
    public static Result Failure(string message, IResultError error = null)
    {
        return new Result { IsSuccess = false }.WithMessage(message).WithError(error);
    }

    /// <summary>
    ///     Creates a new <see cref="Result" /> instance representing a failure.
    /// </summary>
    /// <returns>A new <see cref="Result" /> instance with <see cref="Result.IsSuccess" /> set to <c>false</c>.</returns>
    public static Result Failure<TError>(string message)
        where TError : IResultError, new()
    {
        return new Result { IsSuccess = false }.WithMessage(message).WithError<TError>();
    }

    /// <summary>
    ///     Creates a new failed <see cref="Result" /> instance.
    /// </summary>
    /// <param name="messages">The messages associated with the failure.</param>
    /// <param name="errors">The errors associated with the failure. This parameter is optional.</param>
    /// <returns>A <see cref="Result" /> instance representing a failure with the specified messages and errors.</returns>
    public static Result Failure(IEnumerable<string> messages, IEnumerable<IResultError> errors = null)
    {
        return new Result { IsSuccess = false }.WithMessages(messages).WithErrors(errors);
    }

    /// <summary>
    ///     Generates a failure result with no messages or errors.
    /// </summary>
    /// <returns>A failure <see cref="Result" /> instance.</returns>
    public static Result Failure<TError>(IEnumerable<string> messages)
        where TError : IResultError, new()
    {
        return new Result { IsSuccess = false }.WithMessages(messages).WithError<TError>();
    }

    /// <summary>
    ///     Creates a successful <see cref="Result" /> instance.
    /// </summary>
    /// <returns>A <see cref="Result" /> instance indicating success.</returns>
    public static Result Success()
    {
        return new Result();
    }

    /// <summary>
    ///     Creates a successful <see cref="Result" /> with the specified message.
    /// </summary>
    /// <param name="message">The message to include in the result.</param>
    /// <returns>A successful <see cref="Result" /> containing the specified message.</returns>
    public static Result Success(string message)
    {
        return new Result().WithMessage(message);
    }

    /// <summary>
    ///     Creates a successful <see cref="Result" /> with specified messages.
    /// </summary>
    /// <param name="messages">A collection of messages to include in the result.</param>
    /// <return>Returns a new instance of <see cref="Result" /> marked as successful, containing the specified messages.</return>
    public static Result Success(IEnumerable<string> messages)
    {
        return new Result().WithMessages(messages);
    }
}

/// <summary>
///     Represents a basic result indicating success or failure.
/// </summary>
public class Result<TValue> : Result, IResult<TValue>
{
    /// <summary>
    ///     Gets or sets the value of the result.
    /// </summary>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    public TValue Value { get; set; }

    /// <summary>
    ///     Creates a new instance of <see cref="Result{TValue}" /> with a failure status.
    /// </summary>
    /// <returns>A <see cref="Result{TValue}" /> instance with IsSuccess set to false.</returns>
    public static new Result<TValue> Failure()
    {
        return new Result<TValue> { IsSuccess = false };
    }

    /// <summary>
    ///     Creates a new failed Result object with default error information.
    /// </summary>
    /// <typeparam name="TError">The type of the error to associate with the Result.</typeparam>
    /// <returns>A new Result object with IsSuccess set to false and an associated error of type TError.</returns>
    public static new Result<TValue> Failure<TError>()
        where TError : IResultError, new()
    {
        return new Result<TValue> { IsSuccess = false }.WithError<TError>();
    }

    /// <summary>
    ///     Creates a failure result with the specified value.
    /// </summary>
    /// <typeparam name="TValue">The type of the value associated with the result.</typeparam>
    /// <param name="value">The value associated with the result.</param>
    /// <returns>A new failure result containing the specified value.</returns>
    public static Result<TValue> Failure(TValue value)
    {
        return new Result<TValue> { IsSuccess = false, Value = value };
    }

    /// <summary>
    ///     Creates a new failure result with a specified message and optional error.
    /// </summary>
    /// <param name="message">The failure message.</param>
    /// <param name="error">An optional error object that provides additional information about the failure.</param>
    /// <returns>A new failure result containing the message and optional error.</returns>
    public static new Result<TValue> Failure(string message, IResultError error = null)
    {
        return new Result<TValue> { IsSuccess = false }.WithMessage(message).WithError(error);
    }

    /// <summary>
    ///     Creates a failure result with the specified value, message, and error.
    /// </summary>
    /// <param name="value">The value to associate with the failure result.</param>
    /// <param name="message">A message describing the failure.</param>
    /// <param name="error">An optional error object associated with the failure.</param>
    /// <returns>A failure result containing the specified value, message, and error.</returns>
    public static Result<TValue> Failure(TValue value, string message, IResultError error = null)
    {
        return new Result<TValue> { IsSuccess = false, Value = value }.WithMessage(message).WithError(error);
    }

    /// <summary>
    ///     Creates a failure Result object.
    /// </summary>
    /// <returns>A Result object representing a failure state.</returns>
    public static Result<TValue> Failure(
        TValue value,
        IEnumerable<string> messages,
        IEnumerable<IResultError> errors = null)
    {
        return new Result<TValue> { IsSuccess = false, Value = value }.WithMessages(messages).WithErrors(errors);
    }

    /// <summary>
    ///     Creates a failure result.
    /// </summary>
    /// <returns>A new instance of <see cref="Result{TValue}" /> with <see cref="Result.IsSuccess" /> set to <c>false</c>.</returns>
    public static new Result<TValue> Failure<TError>(string message)
        where TError : IResultError, new()
    {
        return new Result<TValue> { IsSuccess = false }.WithMessage(message).WithError<TError>();
    }

    /// <summary>
    ///     Returns a failure <see cref="Result{TValue}" /> object with the specified error type, value, and message.
    /// </summary>
    /// <typeparam name="TError">The type of error that implements <see cref="IResultError" />.</typeparam>
    /// <param name="value">The value to associate with the result.</param>
    /// <param name="message">The message describing the result.</param>
    /// <returns>A failure result with the specified value and message.</returns>
    public static Result<TValue> Failure<TError>(TValue value, string message)
        where TError : IResultError, new()
    {
        return new Result<TValue> { IsSuccess = false, Value = value }.WithMessage(message).WithError<TError>();
    }

    /// <summary>
    ///     Creates a failure Result object that includes the specified messages and errors.
    /// </summary>
    /// <param name="messages">A collection of error messages to be included in the result.</param>
    /// <param name="errors">A collection of error objects to be included in the result. Default is null.</param>
    /// <returns>A Result object indicating failure, containing the provided messages and errors.</returns>
    public static new Result<TValue> Failure(IEnumerable<string> messages, IEnumerable<IResultError> errors = null)
    {
        return new Result<TValue> { IsSuccess = false }.WithMessages(messages).WithErrors(errors);
    }

    /// <summary>
    ///     Creates a new Result instance representing a failure.
    /// </summary>
    /// <returns>A Result instance configured as a failure.</returns>
    public static new Result Failure<TError>(IEnumerable<string> messages)
        where TError : IResultError, new()
    {
        return new Result<TValue> { IsSuccess = false }.WithMessages(messages).WithError<TError>();
    }

    /// <summary>
    ///     Creates a successful Result object with a default value.
    /// </summary>
    /// <returns>
    ///     A new instance of <see cref="Result{TValue}" /> representing success.
    /// </returns>
    public static new Result<TValue> Success()
    {
        return new Result<TValue>();
    }

    /// <summary>
    ///     Creates a new successful result.
    /// </summary>
    /// <returns>A new successful Result object.</returns>
    public static new Result<TValue> Success(string message)
    {
        return new Result<TValue>().WithMessage(message);
    }

    /// <summary>
    ///     Creates a Result instance representing a successful operation.
    /// </summary>
    /// <returns>A Result instance indicating success.</returns>
    public static new Result<TValue> Success(IEnumerable<string> messages)
    {
        return new Result<TValue>().WithMessages(messages);
    }

    /// <summary>
    ///     Creates a successful Result object with the provided value.
    /// </summary>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="value">The value for the result object.</param>
    /// <returns>A Result object indicating success with the provided value.</returns>
    public static Result<TValue> Success(TValue value)
    {
        return new Result<TValue> { Value = value };
    }

    /// <summary>
    ///     Creates a successful <see cref="Result{TValue}" /> with the specified value and an optional message.
    /// </summary>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="value">The value to be associated with the result.</param>
    /// <param name="message">An optional message providing additional context or information.</param>
    /// <returns>A successful result containing the specified value and message.</returns>
    public static Result<TValue> Success(TValue value, string message)
    {
        return new Result<TValue> { Value = value }.WithMessage(message);
    }

    /// <summary>
    ///     Represents an operation that returns a successful result with value and messages.
    /// </summary>
    /// <param name="value">The value to be returned as part of the success result.</param>
    /// <param name="messages">A collection of messages to be included in the success result.</param>
    /// <returns>A result object containing the value and messages indicating success.</returns>
    public static Result<TValue> Success(TValue value, IEnumerable<string> messages)
    {
        return new Result<TValue> { Value = value }.WithMessages(messages);
    }

    /// <summary>
    ///     Adds a message to the result.
    /// </summary>
    /// <param name="message">The message to add.</param>
    /// <returns>The current result instance with the added message.</returns>
    public new Result<TValue> WithMessage(string message)
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
    public new Result<TValue> WithMessages(IEnumerable<string> messages)
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
    public new Result<TValue> WithError(IResultError error)
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
    public new Result<TValue> WithErrors(IEnumerable<IResultError> errors)
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
    public new Result<TValue> WithError<TError>()
        where TError : IResultError, new()
    {
        this.WithError(Activator.CreateInstance<TError>());
        this.success = false;

        return this;
    }
}