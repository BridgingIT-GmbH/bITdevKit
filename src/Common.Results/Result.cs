// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents a result of an operation that can either succeed or fail. This struct provides
/// a robust way to handle operation outcomes with additional context through messages and typed errors.
/// The Result type is immutable and implements value semantics, making it thread-safe and predictable.
/// </summary>
/// <remarks>
/// Key features:
/// <list type="bullet">
/// <item><description>Immutable value type for thread-safety and predictable behavior</description></item>
/// <item><description>Support for multiple messages and strongly-typed errors</description></item>
/// <item><description>Memory-efficient storage for common cases (0-2 messages/errors)</description></item>
/// <item><description>Rich API for combining and transforming results</description></item>
/// <item><description>Async operation support</description></item>
/// </list>
/// </remarks>
/// <example>
/// Basic usage:
/// <code>
/// // Successful result
/// var success = Result.Success("Operation completed");
/// if (success.IsSuccess)
/// {
///     Console.WriteLine(success.Messages.First());
/// }
///
/// // Failed result with typed error
/// var failure = Result.Failure()
///     .WithError(new ValidationError("Invalid input"))
///     .WithMessage("Please check your input");
///
/// // Checking for specific errors
/// if (failure.HasError{ValidationError}())
/// {
///     var error = failure.GetError{ValidationError}();
///     Console.WriteLine(error.Message);
/// }
///
/// // Conditional results
/// var result = Result.SuccessIf(
///     age >= 18,
///     new ValidationError("Must be 18 or older")
/// );
///
/// // Combining results
/// var combined = Result.Combine(
///     ValidateEmail(email),
///     ValidatePassword(password)
/// );
///
/// // Using with async operations
/// var asyncResult = await GetDataAsync()
///     .OrElseAsync(async ct => await GetBackupDataAsync(ct));
///
/// // Converting to generic result
/// var user = new User { Id = 1, Name = "John" };
/// var userResult = Result.Success()
///     .WithMessage("User created")
///     .To(user);
///
/// // Pattern matching
/// var message = result.Match(
///     success: "Operation succeeded",
///     failure: "Operation failed"
/// );
///
/// // Composition with LINQ-style methods
/// var finalResult = await Result.Success()
///     .AndThenAsync(ValidateInput)
///     .AndThenAsync(ProcessData)
///     .AndThenAsync(SaveToDatabase)
///     .OrElseAsync(GetFallbackData);
/// </code>
/// </example>
[DebuggerDisplay("{IsSuccess ? \"✓\" : \"✗\"} {messages.Count}Msg {errors.Count}Err {FirstMessageOrError}")]
public readonly partial struct Result : IResult
{
    private readonly bool success;
    private readonly ValueList<string> messages;
    private readonly ValueList<IResultError> errors;

    private Result(bool isSuccess, ValueList<string> messages = default, ValueList<IResultError> errors = default)
    {
        this.success = isSuccess;
        this.messages = messages;
        this.errors = errors;
    }

    public static ResultSettings Settings { get; private set; }

    static Result()
    {
        Settings = new ResultSettingsBuilder().Build();
    }

    private string FirstMessageOrError => // used by DebuggerDisplay
        !this.messages.IsEmpty ? $" | {this.messages.AsEnumerable().First()}" :
        !this.errors.IsEmpty ? $" | {this.errors.AsEnumerable().First().GetType().Name}" :
        string.Empty;

    /// <summary>
    /// Configures the global settings for the <see cref="Result"/> type.
    /// </summary>
    /// <param name="settings">A delegate to configure the <see cref="ResultSettingsBuilder"/>.</param>
    public static void Setup(Action<ResultSettingsBuilder> settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var builder = new ResultSettingsBuilder();
        settings(builder);

        Settings = builder.Build();
    }

    /// <summary>
    /// Represents a successful result of an operation.
    /// </summary>
    public static readonly Result SuccessResult = new(true);

    /// <summary>
    /// Represents a default failure result used to create instances of failed operations.
    /// </summary>
    public static readonly Result FailureResult = new(false);

    /// <summary>
    /// Gets a value indicating whether the operation was successful.
    /// </summary>
    /// <example>
    /// <code>
    /// var result = Result.Success(user);
    /// if (result.IsSuccess)
    /// {
    ///     Console.WriteLine($"User {result.Value.Name} retrieved successfully");
    /// }
    /// </code>
    /// </example>
    public bool IsSuccess => this.success;

    /// <summary>
    /// Gets a value indicating whether the operation failed.
    /// </summary>
    /// <example>
    /// <code>
    /// var result = Result.Failure()
    ///     .WithError(new ValidationError("Invalid data"));
    /// if (result.IsFailure)
    /// {
    ///     Console.WriteLine("Operation failed");
    /// }
    /// </code>
    /// </example>
    public bool IsFailure => !this.success;

    /// <summary>
    /// Gets a read-only list of messages associated with the result.
    /// </summary>
    /// <example>
    /// <code>
    /// var result = Result.Success()
    ///     .WithMessage("User retrieved")
    ///     .WithMessage("Cache updated");
    ///
    /// foreach(var message in result.Messages)
    /// {
    ///     Console.WriteLine(message);
    /// }
    /// </code>
    /// </example>
    public IReadOnlyList<string> Messages => this.messages.AsEnumerable().ToList().AsReadOnly();

    /// <summary>
    /// Gets a read-only list of errors associated with the result.
    /// </summary>
    /// <example>
    /// <code>
    /// var result = Result.Failure()
    ///     .WithError(new ValidationError("Invalid email"))
    ///     .WithError(new ValidationError("Invalid password"));
    ///
    /// foreach(var error in result.Errors)
    /// {
    ///     Console.WriteLine(error.Message);
    /// }
    /// </code>
    /// </example>
    public IReadOnlyList<IResultError> Errors => this.errors.AsEnumerable().ToList().AsReadOnly();

    /// <summary>
    /// Creates a new successful Result.
    /// </summary>
    /// <example>
    /// var result = Result.Success();
    /// if (result.IsSuccess)
    /// {
    ///     Console.WriteLine("Operation succeeded");
    /// }
    /// </example>
    public static Result Success() => SuccessResult;

    /// <summary>
    /// Creates a new successful Result with a message.
    /// </summary>
    /// <example>
    /// var result = Result.Success("User profile updated successfully");
    /// Console.WriteLine(result.Messages.First()); // "User profile updated successfully"
    /// </example>
    public static Result Success(string message) => SuccessResult.WithMessage(message);

    /// <summary>
    /// Creates a new successful Result with multiple messages.
    /// </summary>
    /// <example>
    /// var messages = new[] { "Profile updated", "Email sent" };
    /// var result = Result.Success(messages);
    /// foreach(var msg in result.Messages)
    /// {
    ///     Console.WriteLine(msg);
    /// }
    /// </example>
    public static Result Success(IEnumerable<string> messages) => SuccessResult.WithMessages(messages);

    /// <summary>
    /// Creates a new failure Result.
    /// </summary>
    /// <example>
    /// var result = Result.Failure();
    /// if (result.IsFailure)
    /// {
    ///     Console.WriteLine("Operation failed");
    /// }
    /// </example>
    public static Result Failure() => FailureResult;

    /// <summary>
    /// Creates a new failure Result with a message and optional error.
    /// </summary>
    /// <example>
    /// var error = new ValidationError("Invalid email format");
    /// var result = Result.Failure("Profile update failed", error);
    /// if (result.IsFailure)
    /// {
    ///     Console.WriteLine($"{result.Messages.First()}: {result.Errors.First().Message}");
    /// }
    /// </example>
    public static Result Failure(string message, IResultError error = null)
    {
        var result = FailureResult.WithMessage(message);

        return error is null ? result : result.WithError(error);
    }

    /// <summary>
    /// Creates a new failure Result with the specified error type.
    /// </summary>
    /// <example>
    /// var result = Result.Failure{ValidationError}();
    /// if (result.HasError{ValidationError}())
    /// {
    ///     Console.WriteLine("Validation failed");
    /// }
    /// </example>
    public static Result Failure<TError>()
        where TError : IResultError, new() =>
        FailureResult.WithError<TError>();

    /// <summary>
    /// Creates a new failure Result with a message and specified error type.
    /// </summary>
    /// <example>
    /// var result = Result.Failure{ValidationError}("Invalid input data");
    /// Console.WriteLine($"{result.Messages.First()} - {result.GetError{ValidationError}().Message}");
    /// </example>
    public static Result Failure<TError>(string message)
        where TError : IResultError, new() =>
        FailureResult.WithMessage(message).WithError<TError>();

    /// <summary>
    /// Creates a new failure Result with multiple messages and optional errors.
    /// </summary>
    /// <example>
    /// var messages = new[] { "Profile update failed", "Email notification failed" };
    /// var errors = new[] { new ValidationError(), new NotificationError() };
    /// var result = Result.Failure(messages, errors);
    /// </example>
    public static Result Failure(IEnumerable<string> messages, IEnumerable<IResultError> errors = null)
    {
        var result = FailureResult.WithMessages(messages);

        return errors is null ? result : result.WithErrors(errors);
    }

    /// <summary>
    /// Creates a new failure Result with multiple messages and specified error type.
    /// </summary>
    /// <example>
    /// var messages = new[] { "Profile update failed", "Data validation failed" };
    /// var result = Result.Failure{ValidationError}(messages);
    /// </example>
    public static Result Failure<TError>(IEnumerable<string> messages)
        where TError : IResultError, new() =>
        FailureResult.WithMessages(messages).WithError<TError>();

    /// <summary>
    /// Adds a message to the Result.
    /// </summary>
    /// <example>
    /// var result = Result.Success()
    ///     .WithMessage("Profile updated")
    ///     .WithMessage("Notification sent");
    /// </example>
    public Result WithMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return this;
        }

        return new Result(this.success, this.messages.Add(message), this.errors);
    }

    /// <summary>
    /// Adds multiple messages to the Result.
    /// </summary>
    /// <example>
    /// var messages = new[] { "Profile updated", "Email sent" };
    /// var result = Result.Success().WithMessages(messages);
    /// </example>
    public Result WithMessages(IEnumerable<string> messages)
    {
        if (messages is null)
        {
            return this;
        }

        return new Result(this.success, this.messages.AddRange(messages), this.errors);
    }

    /// <summary>
    /// Adds an error to the Result and marks it as failed.
    /// </summary>
    /// <example>
    /// var result = Result.Success()
    ///     .WithError(new ValidationError("Invalid email"));
    /// if (result.HasError{ValidationError}())
    /// {
    ///     Console.WriteLine(result.GetError{ValidationError}().Message);
    /// }
    /// </example>
    public Result WithError(IResultError error)
    {
        if (error is null)
        {
            return this;
        }

        return new Result(false, this.messages, this.errors.Add(error));
    }

    /// <summary>
    /// Adds an exception to the Result and marks it as failed.
    /// </summary>
    /// <example>
    /// var result = Result.Success()
    ///     .WithError(exception);
    /// if (result.HasError{ExceptionError}())
    /// {
    ///     Console.WriteLine(result.GetError{ExceptionError}().Message);
    /// }
    /// </example>
    public Result WithError(Exception ex)
    {
        if (ex is null)
        {
            return this;
        }

        return new Result(false, this.messages, this.errors.Add(new ExceptionError(ex)));
    }

    /// <summary>
    /// Adds multiple errors to the Result and marks it as failed.
    /// </summary>
    /// <example>
    /// var errors = new[] {
    ///     new ValidationError("Invalid email"),
    ///     new ValidationError("Invalid password")
    /// };
    /// var result = Result.Success().WithErrors(errors);
    /// </example>
    public Result WithErrors(IEnumerable<IResultError> errors)
    {
        if (errors?.Any() != true)
        {
            return this;
        }

        return new Result(false, this.messages, this.errors.AddRange(errors));
    }

    /// <summary>
    /// Adds an error of the specified type to the Result and marks it as failed.
    /// </summary>
    /// <example>
    /// var result = Result.Success()
    ///     .WithError{ValidationError}();
    /// if (result.HasError{ValidationError}())
    /// {
    ///     Console.WriteLine("Validation failed");
    /// }
    /// </example>
    public Result WithError<TError>()
        where TError : IResultError, new()
    {
        return this.WithError(Activator.CreateInstance<TError>());
    }

    /// <summary>
    /// Checks if the Result contains an error of the specified type.
    /// </summary>
    /// <example>
    /// var result = Result.Failure()
    ///     .WithError(new ValidationError("Invalid email"));
    /// if (result.HasError{ValidationError}())
    /// {
    ///     Console.WriteLine("Validation error found");
    /// }
    /// </example>
    public bool HasError<TError>()
        where TError : IResultError
    {
        var errorType = typeof(TError);

        return this.errors.AsEnumerable().Any(e => e.GetType() == errorType);
    }

    /// <summary>
    /// Checks if the Result contains any errors.
    /// </summary>
    /// <example>
    /// var result = Result.Success();
    /// if (!result.HasError())
    /// {
    ///     Console.WriteLine("No errors found");
    /// }
    /// </example>
    public bool HasError()
    {
        return !this.errors.IsEmpty;
    }

    /// <summary>
    /// Gets all errors of the specified type from the Result.
    /// </summary>
    /// <example>
    /// if (result.HasError{ValidationError}(out var validationErrors))
    /// {
    ///     foreach(var error in validationErrors)
    ///     {
    ///         Console.WriteLine(error.Message);
    ///     }
    /// }
    /// </example>
    public bool TryGetErrors<TError>(out IEnumerable<IResultError> errors)
        where TError : IResultError
    {
        var errorType = typeof(TError);
        errors = this.errors.AsEnumerable().Where(e => e.GetType() == errorType);

        return errors.Any();
    }

    /// <summary>
    /// Attempts to get an error of the specified type.
    /// </summary>
    /// <example>
    /// if (result.TryGetError{ValidationError}(out var error))
    /// {
    ///     Console.WriteLine($"Validation failed: {error.Message}");
    /// }
    /// else
    /// {
    ///     Console.WriteLine("No validation error found");
    /// }
    /// </example>
    public bool TryGetError<TError>(out IResultError error)
        where TError : IResultError
    {
        error = default;
        var foundError = this.errors.AsEnumerable().FirstOrDefault(e => e is TError);
        if (foundError is null)
        {
            return false;
        }

        error = (TError)foundError;

        return true;
    }

    /// <summary>
    /// Gets an error of the specified type.
    /// </summary>
    /// <example>
    /// var validationError = result.GetError{ValidationError}();
    /// Console.WriteLine($"Validation error: {error.Message}");
    /// </example>
    public IResultError GetError<TError>()
        where TError : IResultError
    {
        var errorType = typeof(TError);

        return this.errors.AsEnumerable().FirstOrDefault(e => e.GetType() == errorType);
    }

    /// <summary>
    /// Gets all errors of the specified type.
    /// </summary>
    /// <example>
    /// var validationErrors = result.GetErrors{ValidationError}();
    /// foreach(var error in validationErrors)
    /// {
    ///     Console.WriteLine($"Validation error: {error.Message}");
    /// }
    /// </example>
    public IEnumerable<IResultError> GetErrors<TError>()
        where TError : IResultError
    {
        var errorType = typeof(TError);

        return this.errors.AsEnumerable().Where(e => e.GetType() == errorType);
    }

    /// <summary>
    /// Determines the Result based on a success condition.
    /// </summary>
    /// <example>
    /// bool IsValid(string email) => email.Contains("@");
    /// var result = Result.SuccessIf(IsValid(email), new ValidationError("Invalid email"));
    /// </example>
    public static Result SuccessIf(bool isSuccess, IResultError error = null)
    {
        return isSuccess ? Success() : Failure().WithError(error);
    }

    /// <summary>
    /// Determines the Result based on a predicate function.
    /// </summary>
    /// <example>
    /// var result = Result.SuccessIf(
    ///     () => email.Contains("@"),
    ///     new ValidationError("Invalid email"));
    /// </example>
    public static Result SuccessIf(Func<bool> predicate, IResultError error = null)
    {
        try
        {
            if (predicate is null)
            {
                return Success();
            }

            var isSuccess = predicate();

            return SuccessIf(isSuccess, error);
        }
        catch (Exception ex)
        {
            return Failure().WithError(Settings.ExceptionErrorFactory(ex));
        }
    }

    /// <summary>
    /// Determines the Result based on a failure condition.
    /// </summary>
    /// <example>
    /// var result = Result.FailureIf(
    ///     string.IsNullOrEmpty(email),
    ///     new ValidationError("Email is required"));
    /// </example>
    public static Result FailureIf(bool isFailure, IResultError error = null)
    {
        return isFailure ? Failure().WithError(error) : Success();
    }

    /// <summary>
    /// Determines the Result based on a failure predicate function.
    /// </summary>
    /// <example>
    /// var result = Result.FailureIf(
    ///     () => string.IsNullOrEmpty(email),
    ///     new ValidationError("Email is required"));
    /// </example>
    public static Result FailureIf(Func<bool> predicate, IResultError error = null)
    {
        try
        {
            if (predicate is null)
            {
                return Success();
            }

            var isFailure = predicate();

            return FailureIf(isFailure, error);
        }
        catch (Exception ex)
        {
            return Failure().WithError(Settings.ExceptionErrorFactory(ex));
        }
    }

    /// <summary>
    /// Executes different functions based on the Result's success state.
    /// </summary>
    /// <example>
    /// // Success case
    /// var result = Result.Success();
    /// var message = result.Match(
    ///     onSuccess: () => "Operation succeeded",
    ///     onFailure: errors => $"Operation failed with {errors.Count} errors"
    /// ); // Returns "Operation succeeded"
    ///
    /// // Failure case
    /// var failed = Result.Failure().WithError(new ValidationError("Invalid input"));
    /// var message = failed.Match(
    ///     onSuccess: () => "All good",
    ///     onFailure: errors => $"Failed: {errors.First().Message}"
    /// ); // Returns "Failed: Invalid input"
    /// </example>
    public TResult Match<TResult>(
        Func<TResult> onSuccess,
        Func<IReadOnlyList<IResultError>, TResult> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        return this.IsSuccess ? onSuccess() : onFailure(this.Errors);
    }

    /// <summary>
    /// Returns different values based on the Result's success state.
    /// </summary>
    /// <example>
    /// var result = Result.Success();
    /// var status = result.Match(
    ///     success: "System operational",
    ///     failure: "System error"
    /// ); // Returns "System operational"
    ///
    /// var failed = Result.Failure();
    /// var status = failed.Match(
    ///     success: 200,
    ///     failure: 500
    /// ); // Returns 500
    /// </example>
    public TResult Match<TResult>(TResult success, TResult failure)
    {
        return this.IsSuccess ? success : failure;
    }

    /// <summary>
    /// Asynchronously executes different functions based on the Result's success state.
    /// </summary>
    /// <example>
    /// var result = Result.Success();
    /// var message = await result.MatchAsync(
    ///     async ct => await GenerateSuccessReportAsync(ct),
    ///     async (errors, ct) => await GenerateErrorReportAsync(errors, ct),
    ///     cancellationToken
    /// );
    ///
    /// // Using with HTTP response
    /// var response = await result.MatchAsync(
    ///     async ct => await CreateSuccessResponseAsync(ct),
    ///     async (errors, ct) => await CreateErrorResponseAsync(errors, ct),
    ///     cancellationToken
    /// );
    /// </example>
    public Task<TResult> MatchAsync<TResult>(
        Func<CancellationToken, Task<TResult>> onSuccess,
        Func<IReadOnlyList<IResultError>, CancellationToken, Task<TResult>> onFailure,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        return this.IsSuccess
            ? onSuccess(cancellationToken)
            : onFailure(this.Errors, cancellationToken);
    }

    /// <summary>
    /// Executes an async success function with a synchronous failure handler.
    /// </summary>
    /// <example>
    /// var result = Result.Success();
    /// var message = await result.MatchAsync(
    ///     async ct => await LoadUserDataAsync(ct),
    ///     errors => "Failed to load user data",
    ///     cancellationToken
    /// );
    ///
    /// // Using with data fetching
    /// var data = await result.MatchAsync(
    ///     async ct => await FetchDataAsync(ct),
    ///     errors => GetCachedData(),
    ///     cancellationToken
    /// );
    /// </example>
    public Task<TResult> MatchAsync<TResult>(
        Func<CancellationToken, Task<TResult>> onSuccess,
        Func<IReadOnlyList<IResultError>, TResult> onFailure,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        return this.IsSuccess
            ? onSuccess(cancellationToken)
            : Task.FromResult(onFailure(this.Errors));
    }

    /// <summary>
    /// Executes a synchronous success function with an async failure handler.
    /// </summary>
    /// <example>
    /// var result = Result.Success();
    /// var message = await result.MatchAsync(
    ///     () => "Operation successful",
    ///     async (errors, ct) => await GenerateErrorReportAsync(errors, ct),
    ///     cancellationToken
    /// );
    ///
    /// // Using with fallback
    /// var data = await result.MatchAsync(
    ///     () => GetCachedValue(),
    ///     async (errors, ct) => await FetchFallbackDataAsync(errors, ct),
    ///     cancellationToken
    /// );
    /// </example>
    public Task<TResult> MatchAsync<TResult>(
        Func<TResult> onSuccess,
        Func<IReadOnlyList<IResultError>, CancellationToken, Task<TResult>> onFailure,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        return this.IsSuccess
            ? Task.FromResult(onSuccess())
            : onFailure(this.Errors, cancellationToken);
    }

    /// <summary>
    /// Creates a new generic Result from a non-generic Result.
    /// </summary>
    /// <example>
    /// var result = Result.Success().WithMessage("Valid");
    /// var typedResult = result.ToResult{User}(); // Creates Result{User} keeping messages/erros
    /// </example>
    public Result<TValue> ToResult<TValue>()
    {
        return this.Match(
            Result<TValue>.Success().WithMessages(this.Messages).WithErrors(this.Errors),
            Result<TValue>.Failure().WithMessages(this.Messages).WithErrors(this.Errors));
    }

    /// <summary>
    /// Creates a new generic Result with a value.
    /// </summary>
    /// <example>
    /// var user = new User { Id = 1, Name = "John" };
    /// var result = Result.Success()
    ///     .WithMessage("User created")
    ///     .ToResult(user);
    /// if(result.IsSuccess)
    /// {
    ///     Console.WriteLine($"Created: {result.Value.Name}");
    /// }
    /// </example>
    public Result<TValue> ToResult<TValue>(TValue value)
    {
        return this.Match(
            Result<TValue>.Success(value)
                .WithMessages(this.Messages)
                .WithErrors(this.Errors),
            Result<TValue>.Failure(value)
                .WithMessages(this.Messages)
                .WithErrors(this.Errors));
    }

    /// <summary>
    /// Creates a new Result instance.
    /// </summary>
    /// <example>
    /// var result = Result.ToResult(); // Same as Result.Success()
    /// </example>
    public static Result ToResult() => SuccessResult;

    /// <summary>
    /// Combines multiple Results into a single Result.
    /// The combined Result is successful only if all Results are successful.
    /// </summary>
    /// <example>
    /// var result1 = Result.Success("First operation");
    /// var result2 = Result.Success("Second operation");
    /// var result3 = Result.Failure("Third operation failed");
    ///
    /// var combined = Result.Combine(result1, result2, result3);
    /// // combined.IsFailure == true
    /// // combined.Messages contains all three messages
    /// </example>
    public static Result Combine(params Result[] results)
    {
        if (results is null || results.Length == 0)
        {
            return SuccessResult;
        }

        var isSuccess = true;
        var combinedMessages = new ValueList<string>();
        var combinedErrors = new ValueList<IResultError>();

        foreach (var result in results)
        {
            if (result.IsFailure)
            {
                isSuccess = false;
            }

            if (!result.messages.IsEmpty)
            {
                combinedMessages = combinedMessages.AddRange(result.messages.AsEnumerable());
            }

            if (!result.errors.IsEmpty)
            {
                combinedErrors = combinedErrors.AddRange(result.errors.AsEnumerable());
            }
        }

        return new Result(isSuccess, combinedMessages, combinedErrors);
    }

    /// <summary>
    /// Provides an implicit conversion from Result to bool.
    /// </summary>
    /// <example>
    /// var result = Result.Success();
    /// if (result) // Implicitly converts to true
    /// {
    ///     Console.WriteLine("Success!");
    /// }
    /// </example>
    public static implicit operator bool(Result result) => result.IsSuccess;

    /// <summary>
    /// Returns a string representation of the Result.
    /// </summary>
    /// <example>
    /// var result = Result.Success("Operation completed")
    ///     .WithError(new ValidationError("Invalid input"));
    /// Console.WriteLine(result.ToResultString());
    /// // Output:
    /// // Success: False
    /// // Messages:
    /// // - Operation completed
    /// // Errors:
    /// // - [ValidationError] Invalid input
    /// </example>
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Success: {this.IsSuccess}");

        if (!this.messages.IsEmpty)
        {
            sb.AppendLine("Messages:");
            foreach (var message in this.messages.AsEnumerable())
            {
                sb.AppendLine($"- {message}");
            }
        }

        if (!this.errors.IsEmpty)
        {
            sb.AppendLine("Errors:");
            foreach (var error in this.errors.AsEnumerable())
            {
                sb.AppendLine($"- [{error.GetType().Name}] {error.Message}");
            }
        }

        return sb.ToString().TrimEnd();
    }
}