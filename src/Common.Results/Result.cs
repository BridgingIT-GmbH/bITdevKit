// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
///     Represents the result of an operation, which can be either a success or a failure.
/// </summary>
public partial class Result : IResult
{
    private readonly List<IResultError> errors = [];
    private readonly List<string> messages = [];
    private bool success = true;

    // /// <summary>
    // ///     Initializes a new instance of the <see cref="Result" /> class.
    // ///     Represents the outcome of an operation, capturing success, failure, associated messages, and errors.
    // /// </summary>
    public Result() { } // needs to be public for mapster

    /// <summary>
    ///     Implicitly converts a Result to a boolean value based on its success state.
    /// </summary>
    /// <param name="result">The Result to convert.</param>
    /// <returns>True if the Result is successful; otherwise, false.</returns>
    /// <example>
    /// <code>
    /// Result result = Result.Success();
    /// if (result) // Implicitly converts to true
    /// {
    ///     // Handle success
    /// }
    /// </code>
    /// </example>
    public static implicit operator bool(Result result) =>
        result.Match(true, false);

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
        private init => this.success = value;
    }

    /// <summary>
    ///     Gets a value indicating whether the result operation has failed.
    ///     This property returns true if the result was not successful.
    /// </summary>
    public bool IsFailure => !this.success;

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
        if (error is null)
        {
            return this;
        }

        this.errors.Add(error);
        this.success = false;

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
            foreach (var error in this.errors)
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
    public static Result Failure()
    {
        return new Result { IsSuccess = false };
    }

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
        return new Result { IsSuccess = false }
            .WithMessage(message).WithError(error);
    }

    /// <summary>
    ///     Creates a new <see cref="Result" /> instance representing a failure.
    /// </summary>
    /// <returns>A new <see cref="Result" /> instance with <see cref="Result.IsSuccess" /> set to <c>false</c>.</returns>
    public static Result Failure<TError>(string message)
        where TError : IResultError, new()
    {
        return new Result { IsSuccess = false }
            .WithMessage(message).WithError<TError>();
    }

    /// <summary>
    ///     Creates a new failed <see cref="Result" /> instance.
    /// </summary>
    /// <param name="messages">The messages associated with the failure.</param>
    /// <param name="errors">The errors associated with the failure. This parameter is optional.</param>
    /// <returns>A <see cref="Result" /> instance representing a failure with the specified messages and errors.</returns>
    public static Result Failure(IEnumerable<string> messages, IEnumerable<IResultError> errors = null)
    {
        return new Result { IsSuccess = false }
            .WithMessages(messages).WithErrors(errors);
    }

    /// <summary>
    ///     Generates a failure result with no messages or errors.
    /// </summary>
    /// <returns>A failure <see cref="Result" /> instance.</returns>
    public static Result Failure<TError>(IEnumerable<string> messages)
        where TError : IResultError, new()
    {
        return new Result { IsSuccess = false }
            .WithMessages(messages).WithError<TError>();
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
    ///     Creates a new <see cref="Result" /> instance.
    /// </summary>
    /// <returns>A <see cref="Result" /> instance indicating success.</returns>
    public static Result For()
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

    /// <summary>
    ///     Converts a non-generic Result to a generic Result{TValue}.
    /// </summary>
    /// <typeparam name="TValue">The type for the generic Result.</typeparam>
    /// <returns>A generic Result with the same success state, messages, and errors.</returns>
    /// <example>
    /// <code>
    /// var result = Result.Success()
    ///     .WithMessage("Operation completed");
    /// var userResult = result.For{User}();
    /// </code>
    /// </example>
    public Result<TValue> For<TValue>()
    {
        return this; // implicit conversion
    }

    /// <summary>
    ///     Converts a non-generic Result to a generic Result{TValue} with a specified value.
    /// </summary>
    /// <typeparam name="TValue">The type for the generic Result.</typeparam>
    /// <param name="value">The value to include in the generic Result.</param>
    /// <returns>A generic Result with the same success state, messages, errors, and the provided value.</returns>
    /// <example>
    /// <code>
    /// var user = new User { Id = 1, Name = "John" };
    /// var result = Result.Success()
    ///     .WithMessage("User validated")
    ///     .For(user);
    /// </code>
    /// </example>
    public Result<TValue> For<TValue>(TValue value)
    {
        return this.Match(
            Result<TValue>.Success(value)
                .WithMessages(this.Messages).WithErrors(this.Errors),
            Result<TValue>.Failure(value)
                .WithMessages(this.Messages).WithErrors(this.Errors));
    }
}