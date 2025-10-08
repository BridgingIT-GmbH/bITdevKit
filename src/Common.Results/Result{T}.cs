// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents a result of an operation that can either succeed or fail, containing a value of type T.
/// This struct provides value semantics and is optimized for reference type values.
/// </summary>
/// <typeparam name="T">The type of the result value. Typically a reference type.</typeparam>
/// <example>
/// Basic usage:
/// <code>
/// // Create successful result
/// var user = new User { Id = 1, Name = "John" };
/// var success = Result{User}.Success(user);
///
/// // Check success and access value
/// if (success.IsSuccess)
/// {
///     Console.WriteLine($"User: {success.Value.Name}");
/// }
///
/// // Create failure with error
/// var failure = Result{User}.Failure()
///     .WithError(new ValidationError("Invalid user data"));
///
/// // Pattern matching on result
/// var message = success.Match(
///     user => $"Found user: {user.Name}",
///     errors => "User not found"
/// );
/// </code>
/// </example>
[DebuggerDisplay("{IsSuccess ? \"✓\" : \"✗\"} {messages.Count}Msg {errors.Count}Err {FirstMessageOrError}")]
public readonly partial struct Result<T> : IResult<T>
{
    private readonly T value;
    private readonly bool success;
    private readonly ValueList<string> messages;
    private readonly ValueList<IResultError> errors;

    private Result(bool isSuccess, T value = default, ValueList<string> messages = default, ValueList<IResultError> errors = default)
    {
        this.success = isSuccess;
        this.value = value;
        this.messages = messages;
        this.errors = errors;
    }

    private string FirstMessageOrError =>
        !this.messages.IsEmpty ? $" | {this.messages.AsEnumerable().First()}" :
        !this.errors.IsEmpty ? $" | {this.errors.AsEnumerable().First().GetType().Name}" :
        string.Empty;

    /// <summary>
    /// Gets a value indicating whether the operation was successful.
    /// </summary>
    /// <example>
    /// <code>
    /// var result = Result{User}.Success(user);
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
    /// var result = Result{User}.Failure()
    ///     .WithError(new ValidationError("Invalid data"));
    /// if (result.IsFailure)
    /// {
    ///     Console.WriteLine("Operation failed");
    /// }
    /// </code>
    /// </example>
    public bool IsFailure => !this.success;

    /// <summary>
    /// Gets the value if the result is successful. Throws if accessing a failed result's value.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when attempting to access the Value property of a failed result.
    /// </exception>
    /// <example>
    /// <code>
    /// var result = Result{User}.Success(user);
    /// try
    /// {
    ///     var name = result.Value.Name; // Safe if IsSuccess is true
    /// }
    /// catch (InvalidOperationException)
    /// {
    ///     Console.WriteLine("Cannot access value of failed result");
    /// }
    /// </code>
    /// </example>
    public T Value => this.value;

    //public T Value => this.success
    //    ? this.value
    //    : throw new InvalidOperationException("Cannot access Value of failed result");

    /// <summary>
    /// Gets a read-only list of messages associated with the result.
    /// </summary>
    /// <example>
    /// <code>
    /// var result = Result{User}.Success(user)
    ///     .WithMessage("User retrieved")
    ///     .WithMessage("Cache updated");
    ///
    /// foreach(var message in result.Messages)
    /// {
    ///     Console.WriteLine(message);
    /// }
    /// </code>
    /// </example>
    public IReadOnlyList<string> Messages =>
        this.messages.AsEnumerable().ToList().AsReadOnly();

    /// <summary>
    /// Gets a read-only list of errors associated with the result.
    /// </summary>
    /// <example>
    /// <code>
    /// var result = Result{User}.Failure()
    ///     .WithError(new ValidationError("Invalid email"))
    ///     .WithError(new ValidationError("Invalid password"));
    ///
    /// foreach(var error in result.Errors)
    /// {
    ///     Console.WriteLine(error.Message);
    /// }
    /// </code>
    /// </example>
    public IReadOnlyList<IResultError> Errors =>
        this.errors.AsEnumerable().ToList().AsReadOnly();

    /// <summary>
    /// Implicitly converts a value to a successful Result{T}.
    /// </summary>
    /// <param name="value">The value to convert to a Result{T}.</param>
    /// <returns>A successful Result{T} containing the value.</returns>
    /// <example>
    /// <code>
    /// // Direct assignment creates a successful result
    /// var user = new User { Id = 1, Name = "John" };
    /// Result{User} result = user;
    ///
    /// // Method return value automatically converts
    /// public User GetUser() => new User { Id = 1 };
    /// Result{User} userResult = GetUser();
    /// </code>
    /// </example>
    public static implicit operator Result<T>(T value)
    {
        if (value is IResult<T> result)
        {
            return result.IsSuccess
                ? Success(result.Value).WithMessages(result.Messages).WithErrors(result.Errors)
                : Failure(result.Value).WithMessages(result.Messages).WithErrors(result.Errors);
        }

        return Success(value);
    }

    /// <summary>
    /// Implicitly converts an async Task{T} to Result{T}.
    /// Note: This operator will block synchronously - use with caution.
    /// </summary>
    /// <param name="task">The task to convert to a Result{T}.</param>
    /// <returns>A Result{T} based on the task's outcome.</returns>
    /// <example>
    /// <code>
    /// public async Task{User} GetUserAsync() => await _repository.GetUserAsync();
    ///
    /// // Direct conversion (not recommended due to blocking)
    /// Result{User} result = GetUserAsync();
    ///
    /// // Preferred async approach
    /// Result{User} result = await GetUserAsync();
    /// </code>
    /// </example>
    public static implicit operator Result<T>(Task<T> task)
    {
        try
        {
            if (task is null)
            {
                return Failure().WithError(new Error("Task was null"));
            }

            var value = task.GetAwaiter().GetResult();

            return Success(value);
        }
        catch (Exception ex)
        {
            return Failure().WithError(Result.Settings.ExceptionErrorFactory(ex));
        }
    }

    /// <summary>
    /// Implicitly converts a Result{T} to a boolean based on its success state.
    /// </summary>
    /// <param name="result">The Result{T} to convert.</param>
    /// <returns>True if the result is successful; otherwise, false.</returns>
    /// <example>
    /// <code>
    /// var result = Result{User}.Success(user);
    /// if (result) // Implicitly converts to true
    /// {
    ///     Console.WriteLine("Operation succeeded");
    /// }
    /// </code>
    /// </example>
    public static implicit operator bool(Result<T> result) => result.IsSuccess;

    /// <summary>
    /// Implicitly converts a Result{T} to a non-generic Result.
    /// Note: The value is not preserved in the conversion.
    /// </summary>
    /// <param name="result">The Result{T} to convert.</param>
    /// <returns>A Result with the same success state, messages, and errors.</returns>
    /// <example>
    /// <code>
    /// var userResult = Result{User}.Success(user)
    ///     .WithMessage("User created");
    /// Result result = userResult; // Converts but loses the User value
    /// </code>
    /// </example>
    public static implicit operator Result(Result<T> result) =>
        result.Match(
            _ => Result.Success().WithMessages(result.Messages).WithErrors(result.Errors),
            _ => Result.Failure().WithMessages(result.Messages).WithErrors(result.Errors));

    /// <summary>
    /// Implicitly converts a non-generic Result to a Result{T}.
    /// </summary>
    /// <param name="result">The Result to convert.</param>
    /// <returns>A Result{T} with the same success state, messages, and errors.</returns>
    /// <example>
    /// <code>
    /// var result = Result.Success()
    ///     .WithMessage("Operation completed");
    /// Result{User} userResult = result; // Converts to generic result
    /// </code>
    /// </example>
    public static implicit operator Result<T>(Result result) =>
        result.Match(
            () => Success().WithMessages(result.Messages).WithErrors(result.Errors),
            _ => Failure().WithMessages(result.Messages).WithErrors(result.Errors));

    /// <summary>
    ///     Creates a Result from an operation, handling any exceptions that occur.
    /// </summary>
    /// <returns>A Result representing the outcome of the operation.</returns>
    /// <example>
    /// <code>
    /// var result = Result.Bind(() => {
    ///     userRepository.DeleteAll();
    /// });
    /// </code>
    /// </example>
    public static Result<T> Bind(Func<T> operation)
    {
        if (operation is null)
        {
            return Failure()
                .WithError(new Error("Operation cannot be null"));
        }

        try
        {
            var value = operation();

            return Success(value);
        }
        catch (Exception ex)
        {
            return Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Creates a Result from an async operation, handling any exceptions that occur.
    /// </summary>
    /// <param name="operation">The async operation to execute.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A Result representing the outcome of the operation.</returns>
    /// <example>
    /// <code>
    /// var result = await Result.BindAsync(
    ///     async ct => await DeleteAllUsersAsync(ct),
    ///     cancellationToken
    /// );
    /// </code>
    /// </example>
    public static async Task<Result<T>> BindAsync(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        if (operation is null)
        {
            return Failure()
                .WithError(new Error("Operation cannot be null"));
        }

        try
        {
            var value = await operation(cancellationToken);

            return Success(value);
        }
        catch (OperationCanceledException)
        {
            return Failure()
                .WithError(new OperationCancelledError())
                .WithMessage("Operation was cancelled");
        }
        catch (Exception ex)
        {
            return Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessage(ex.Message);
        }
    }

    // /// <summary>
    // /// Implicitly converts a Result{T} to a Result{TOutput}.
    // /// </summary>
    // /// <typeparam name="TOutput">The target type of the conversion.</typeparam>
    // /// <param name="result">The Result{T} to convert.</param>
    // /// <returns>A new Result{TOutput} with the same success state, messages, and errors.</returns>
    // /// <example>
    // /// <code>
    // /// var intResult = Result{int}.Failure()
    // ///     .WithMessage("Processing failed")
    // ///     .WithError(new ValidationError("Invalid value"));
    // ///
    // /// Result{string} strResult = intResult; // Implicit conversion
    // /// </code>
    // /// </example>
    // public static implicit operator Result<TOutput>(Result<T> result)
    // {
    //     return result.Match(
    //         _ => Result<TOutput>.Success()
    //             .WithMessages(result.Messages)
    //             .WithErrors(result.Errors),
    //         _ => Result<TOutput>.Failure()
    //             .WithMessages(result.Messages)
    //             .WithErrors(result.Errors));
    // }

    /// <summary>
    /// Creates a successful Result{T} with a specified value.
    /// </summary>
    /// <param name="value">The value to be contained in the successful result.</param>
    /// <returns>A new successful Result{T} containing the specified value.</returns>
    /// <example>
    /// <code>
    /// var user = new User { Id = 1, Name = "John" };
    /// var result = Result{User}.Success(user);
    /// Console.WriteLine($"Created user: {result.Value.Name}"); // Safe to access Value
    /// </code>
    /// </example>
    public static Result<T> Success(T value) => new Result<T>(true, value);

    /// <summary>
    /// Creates a successful Result{T} with no value.
    /// </summary>
    /// <returns>A new successful Result{T} containing the default value of T.</returns>
    /// <example>
    /// <code>
    /// var result = Result{User}.Success();
    /// Console.WriteLine($"Success: {result.IsSuccess}"); // True
    /// </code>
    /// </example>
    public static Result<T> Success() => new Result<T>(true);

    /// <summary>
    /// Creates a successful Result{T} with a value and a message.
    /// </summary>
    /// <param name="value">The value to be contained in the successful result.</param>
    /// <param name="message">A message describing the successful operation.</param>
    /// <returns>A new successful Result{T} containing the value and message.</returns>
    /// <example>
    /// <code>
    /// var user = new User { Id = 1, Name = "John" };
    /// var result = Result{User}.Success(user, "User created successfully");
    /// Console.WriteLine(result.Messages.First()); // "User created successfully"
    /// </code>
    /// </example>
    public static Result<T> Success(T value, string message) => new Result<T>(true, value).WithMessage(message);

    /// <summary>
    /// Creates a successful Result{T} with a value and multiple messages.
    /// </summary>
    /// <param name="value">The value to be contained in the successful result.</param>
    /// <param name="messages">Collection of messages describing the successful operation.</param>
    /// <returns>A new successful Result{T} containing the value and messages.</returns>
    /// <example>
    /// <code>
    /// var user = new User { Id = 1, Name = "John" };
    /// var messages = new[] { "User created", "Welcome email sent" };
    /// var result = Result{User}.Success(user, messages);
    /// foreach(var msg in result.Messages)
    /// {
    ///     Console.WriteLine(msg);
    /// }
    /// </code>
    /// </example>
    public static Result<T> Success(T value, IEnumerable<string> messages) => new Result<T>(true, value).WithMessages(messages);

    /// <summary>
    /// Creates a Result based on a condition and value.
    /// </summary>
    /// <param name="isSuccess">Condition determining success or failure.</param>
    /// <param name="value">The value to include in the Result.</param>
    /// <param name="error">Optional error to include if condition is false.</param>
    /// <returns>A successful Result with the value if condition is true; otherwise, a failure Result with the error.</returns>
    /// <example>
    /// <code>
    /// var user = new User { Age = 25 };
    /// var result = Result{User}.SuccessIf(
    ///     user.Age >= 18,
    ///     user,
    ///     new ValidationError("User must be 18 or older")
    /// );
    /// </code>
    /// </example>
    public static Result<T> SuccessIf(bool isSuccess, T value, IResultError error = null) => isSuccess ? Success(value) : Failure().WithError(error);

    /// <summary>
    /// Creates a Result based on a predicate function evaluating the value.
    /// </summary>
    /// <param name="predicate">Function to evaluate the value.</param>
    /// <param name="value">The value to evaluate and include in the Result.</param>
    /// <param name="error">Optional error to include if predicate returns false.</param>
    /// <returns>A successful Result if predicate returns true; otherwise, a failure Result.</returns>
    /// <example>
    /// <code>
    /// var user = new User { Email = "test@example.com" };
    /// var result = Result{User}.SuccessIf(
    ///     u => u.Email.Contains("@"),
    ///     user,
    ///     new ValidationError("Invalid email format")
    /// );
    /// </code>
    /// </example>
    public static Result<T> SuccessIf(Func<T, bool> predicate, T value, IResultError error = null)
    {
        try
        {
            if (predicate is null)
            {
                return Success(value);
            }

            var isSuccess = predicate(value);

            return SuccessIf(isSuccess, value, error);
        }
        catch (Exception ex)
        {
            return Failure().WithError(Result.Settings.ExceptionErrorFactory(ex));
        }
    }

    /// <summary>
    /// Creates a failed Result{T} with no value.
    /// </summary>
    /// <returns>A new failed Result{T}.</returns>
    /// <example>
    /// <code>
    /// var result = Result{User}.Failure();
    /// Console.WriteLine($"Failed: {result.IsFailure}"); // True
    /// </code>
    /// </example>
    public static Result<T> Failure() => new Result<T>(false);

    /// <summary>
    /// Creates a failed Result{T} with a specified value.
    /// </summary>
    /// <param name="value">The value to be contained in the failed result.</param>
    /// <returns>A new failed Result{T} containing the specified value.</returns>
    /// <example>
    /// <code>
    /// var invalidUser = new User(); // Invalid user state
    /// var result = Result{User}.Failure(invalidUser)
    ///     .WithError(new ValidationError("Invalid user data"));
    /// </code>
    /// </example>
    public static Result<T> Failure(T value) => new Result<T>(false, value);

    /// <summary>
    /// Creates a failed Result{T} with a message and optional error.
    /// </summary>
    /// <param name="message">Message describing the failure.</param>
    /// <param name="error">Optional error providing additional failure details.</param>
    /// <returns>A new failed Result{T} with the specified message and error.</returns>
    /// <example>
    /// <code>
    /// var result = Result{User}.Failure(
    ///     "User creation failed",
    ///     new ValidationError("Invalid email format")
    /// );
    /// </code>
    /// </example>
    public static Result<T> Failure(string message, IResultError error = null)
    {
        var result = new Result<T>(false).WithMessage(message);

        return error is null ? result : result.WithError(error);
    }

    /// <summary>
    /// Creates a failed Result{T} with a value, message, and optional error.
    /// </summary>
    /// <param name="value">The value to be contained in the failed result.</param>
    /// <param name="message">Message describing the failure.</param>
    /// <param name="error">Optional error providing additional failure details.</param>
    /// <returns>A new failed Result{T} with the specified value, message, and error.</returns>
    /// <example>
    /// <code>
    /// var invalidUser = new User();
    /// var result = Result{User}.Failure(
    ///     invalidUser,
    ///     "Validation failed",
    ///     new ValidationError("Required fields missing")
    /// );
    /// </code>
    /// </example>
    public static Result<T> Failure(T value, string message, IResultError error = null)
    {
        var result = new Result<T>(false, value).WithMessage(message);

        return error is null ? result : result.WithError(error);
    }

    /// <summary>
    /// Creates a failed Result{T} with multiple messages and optional errors.
    /// </summary>
    /// <param name="messages">Collection of messages describing the failure.</param>
    /// <param name="errors">Optional collection of errors providing additional details.</param>
    /// <returns>A new failed Result{T} with the specified messages and errors.</returns>
    /// <example>
    /// <code>
    /// var messages = new[] { "Validation failed", "Database update failed" };
    /// var errors = new IResultError[]
    /// {
    ///     new ValidationError("Invalid email"),
    ///     new DbError("Connection failed")
    /// };
    /// var result = Result{User}.Failure(messages, errors);
    /// </code>
    /// </example>
    public static Result<T> Failure(IEnumerable<string> messages, IEnumerable<IResultError> errors = null)
    {
        var result = new Result<T>(false).WithMessages(messages);

        return errors is null ? result : result.WithErrors(errors);
    }

    /// <summary>
    /// Creates a failed Result{T} with a value and multiple messages and errors.
    /// </summary>
    /// <param name="value">The value to be contained in the failed result.</param>
    /// <param name="messages">Collection of messages describing the failure.</param>
    /// <param name="errors">Optional collection of errors providing additional details.</param>
    /// <returns>A new failed Result{T} with the specified value, messages, and errors.</returns>
    /// <example>
    /// <code>
    /// var invalidUser = new User();
    /// var messages = new[] { "Validation failed", "Cannot save user" };
    /// var errors = new IResultError[]
    /// {
    ///     new ValidationError("Invalid email"),
    ///     new ValidationError("Invalid password")
    /// };
    /// var result = Result{User}.Failure(invalidUser, messages, errors);
    /// </code>
    /// </example>
    public static Result<T> Failure(T value, IEnumerable<string> messages, IEnumerable<IResultError> errors = null)
    {
        var result = new Result<T>(false, value).WithMessages(messages);

        return errors is null ? result : result.WithErrors(errors);
    }

    /// <summary>
    /// Creates a failed Result{T} with a specific error type.
    /// </summary>
    /// <typeparam name="TError">The type of error to create.</typeparam>
    /// <returns>A new failed Result{T} with the specified error type.</returns>
    /// <example>
    /// <code>
    /// var result = Result{User}.Failure{ValidationError}();
    /// if (result.HasError{ValidationError}())
    /// {
    ///     Console.WriteLine("Validation failed");
    /// }
    /// </code>
    /// </example>
    public static Result<T> Failure<TError>()
        where TError : IResultError, new() => new Result<T>(false).WithError<TError>();

    /// <summary>
    /// Creates a failed Result{T} with a specific error instance.7
    /// </summary>
    /// <param name="error"></param>
    /// <returns></returns>
    public static Result<T> Failure(IResultError error) => new Result<T>(false).WithError(error);

    /// <summary>
    /// Creates a failed Result{T} with a specific error type and message.
    /// </summary>
    /// <typeparam name="TError">The type of error to create.</typeparam>
    /// <param name="message">Message describing the failure.</param>
    /// <returns>A new failed Result{T} with the specified error type and message.</returns>
    /// <example>
    /// <code>
    /// var result = Result{User}.Failure{ValidationError}("Invalid user data");
    /// var error = result.GetError{ValidationError}();
    /// Console.WriteLine(error.Message);
    /// </code>
    /// </example>
    public static Result<T> Failure<TError>(string message)
        where TError : IResultError, new() => new Result<T>(false).WithMessage(message).WithError<TError>();

    /// <summary>
    /// Creates a Result based on a failure condition and value.
    /// </summary>
    /// <param name="isFailure">Condition determining failure or success.</param>
    /// <param name="value">The value to include in the Result.</param>
    /// <param name="error">Optional error to include if condition is true.</param>
    /// <returns>A failure Result with the error if condition is true; otherwise, a successful Result with the value.</returns>
    /// <example>
    /// <code>
    /// var user = new User { IsBlocked = false };
    /// var result = Result{User}.FailureIf(
    ///     user.IsBlocked,
    ///     user,
    ///     new ValidationError("User account is blocked")
    /// );
    /// </code>
    /// </example>
    public static Result<T> FailureIf(bool isFailure, T value, IResultError error = null) => isFailure ? Failure().WithError(error) : Success(value);

    /// <summary>
    /// Creates a Result based on a failure predicate function evaluating the value.
    /// </summary>
    /// <param name="predicate">Function to evaluate the value for failure.</param>
    /// <param name="value">The value to evaluate and include in the Result.</param>
    /// <param name="error">Optional error to include if predicate returns true.</param>
    /// <returns>A failure Result if predicate returns true; otherwise, a successful Result.</param>
    /// <example>
    /// <code>
    /// var user = new User { LastLoginDate = DateTime.Now.AddDays(-31) };
    /// var result = Result{User}.FailureIf(
    ///     u => (DateTime.Now - u.LastLoginDate).Days > 30,
    ///     user,
    ///     new ValidationError("Account inactive for too long")
    /// );
    /// </code>
    /// </example>
    public static Result<T> FailureIf(Func<T, bool> predicate, T value, IResultError error = null)
    {
        try
        {
            if (predicate is null)
            {
                return Success(value);
            }

            var isFailure = predicate(value);

            return FailureIf(isFailure, value, error);
        }
        catch (Exception ex)
        {
            return Failure().WithError(Result.Settings.ExceptionErrorFactory(ex));
        }
    }

    /// <summary>
    /// Adds a message to the Result while maintaining immutability.
    /// </summary>
    /// <param name="message">The message to add. If null or whitespace, the Result is returned unchanged.</param>
    /// <returns>A new Result{T} with the added message.</returns>
    /// <example>
    /// <code>
    /// var result = Result{User}.Success(user)
    ///     .WithMessage("User created successfully")
    ///     .WithMessage("Welcome email scheduled");
    ///
    /// foreach(var msg in result.Messages)
    /// {
    ///     Console.WriteLine(msg);
    /// }
    /// </code>
    /// </example>
    public Result<T> WithMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return this;
        }

        return new Result<T>(this.success, this.value, this.messages.Add(message), this.errors);
    }

    /// <summary>
    /// Adds multiple messages to the Result while maintaining immutability.
    /// </summary>
    /// <param name="messages">Collection of messages to add. If null, the Result is returned unchanged.</param>
    /// <returns>A new Result{T} with the added messages.</returns>
    /// <example>
    /// <code>
    /// var messages = new[]
    /// {
    ///     "User profile created",
    ///     "Verification email sent",
    ///     "Welcome notification queued"
    /// };
    ///
    /// var result = Result{User}.Success(user)
    ///     .WithMessages(messages);
    /// </code>
    /// </example>
    public Result<T> WithMessages(IEnumerable<string> messages)
    {
        if (messages is null)
        {
            return this;
        }

        return new Result<T>(this.success, this.value, this.messages.AddRange(messages), this.errors);
    }

    /// <summary>
    /// Adds an error to the Result and marks it as failed while maintaining immutability.
    /// </summary>
    /// <param name="error">The error to add. If null, the Result is returned unchanged.</param>
    /// <returns>A new Result{T} marked as failed with the added error.</returns>
    /// <example>
    /// <code>
    /// var result = Result{User}.Success(user)
    ///     .WithError(new ValidationError("Email is invalid"))
    ///     .WithError(new ValidationError("Password is too short"));
    ///
    /// if (result.HasError{ValidationError}())
    /// {
    ///     var errors = result.GetErrors{ValidationError}();
    ///     foreach(var error in errors)
    ///     {
    ///         Console.WriteLine(error.Message);
    ///     }
    /// }
    /// </code>
    /// </example>
    public Result<T> WithError(IResultError error)
    {
        if (error is null)
        {
            return this;
        }

        return new Result<T>(false, this.value, this.messages, this.errors.Add(error));
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
    public Result<T> WithError(Exception ex)
    {
        if (ex is null)
        {
            return this;
        }

        return new Result<T>(false, default, this.messages, this.errors.Add(new ExceptionError(ex)));
    }

    /// <summary>
    /// Adds multiple errors to the Result and marks it as failed while maintaining immutability.
    /// </summary>
    /// <param name="errors">Collection of errors to add. If null, the Result is returned unchanged.</param>
    /// <returns>A new Result{T} marked as failed with the added errors.</returns>
    /// <example>
    /// <code>
    /// var validationErrors = new IResultError[]
    /// {
    ///     new ValidationError("Invalid email format"),
    ///     new ValidationError("Password too weak"),
    ///     new ValidationError("Username taken")
    /// };
    ///
    /// var result = Result{User}.Success(user)
    ///     .WithErrors(validationErrors);
    /// </code>
    /// </example>
    public Result<T> WithErrors(IEnumerable<IResultError> errors)
    {
        if (errors?.Any() != true)
        {
            return this;
        }

        return new Result<T>(false, this.value, this.messages, this.errors.AddRange(errors));
    }

    /// <summary>
    /// Adds a strongly-typed error to the Result and marks it as failed while maintaining immutability.
    /// </summary>
    /// <typeparam name="TError">The type of error to add. Must implement IResultError and have a parameterless constructor.</typeparam>
    /// <returns>A new Result{T} marked as failed with the added error.</returns>
    /// <example>
    /// <code>
    /// // Define custom error type
    /// public class UserNotFoundError : IResultError
    /// {
    ///     public string Message => "User not found in database";
    /// }
    ///
    /// // Use in Result
    /// var result = Result{User}.Success(user)
    ///     .WithError{UserNotFoundError}();
    ///
    /// if (result.HasError{UserNotFoundError}())
    /// {
    ///     var error = result.GetError{UserNotFoundError}();
    ///     Console.WriteLine(error.Message);
    /// }
    /// </code>
    /// </example>
    public Result<T> WithError<TError>()
        where TError : IResultError, new() => this.WithError(Activator.CreateInstance<TError>());

    /// <summary>
    /// Checks if the Result contains an error of a specific type.
    /// </summary>
    /// <typeparam name="TError">The type of error to check for.</typeparam>
    /// <returns>True if the Result contains at least one error of type TError; otherwise, false.</returns>
    /// <example>
    /// <code>
    /// var result = Result{User}.Failure()
    ///     .WithError(new ValidationError("Invalid email"));
    ///
    /// if (result.HasError{ValidationError}())
    /// {
    ///     var error = result.GetError{ValidationError}();
    ///     Console.WriteLine($"Validation failed: {error.Message}");
    /// }
    /// </code>
    /// </example>
    public bool HasError<TError>()
        where TError : class, IResultError
    {
        var errorType = typeof(TError);

        return this.errors.AsEnumerable().Any(e => e.GetType() == errorType);
    }

    /// <summary>
    /// Checks if the Result contains any errors.
    /// </summary>
    /// <returns>True if the Result contains any errors; otherwise, false.</returns>
    /// <example>
    /// <code>
    /// var result = Result{User}.Success(user);
    /// if (!result.HasError())
    /// {
    ///     Console.WriteLine("Operation completed without errors");
    /// }
    /// </code>
    /// </example>
    public bool HasError() => !this.errors.IsEmpty;

    /// <summary>
    /// Tries to get all errors of a specific type from the Result.
    /// </summary>
    /// <typeparam name="TError">The type of errors to retrieve.</typeparam>
    /// <param name="errors">When this method returns, contains all errors of type TError if any were found; otherwise, empty.</param>
    /// <returns>True if any errors of type TError were found; otherwise, false.</returns>
    /// <example>
    /// <code>
    /// if (result.TryGetErrors{ValidationError}(out var validationErrors))
    /// {
    ///     foreach(var error in validationErrors)
    ///     {
    ///         Console.WriteLine($"Validation error: {error.Message}");
    ///     }
    /// }
    /// else
    /// {
    ///     Console.WriteLine("No validation errors found");
    /// }
    /// </code>
    /// </example>
    public bool TryGetErrors<TError>(out IEnumerable<TError> errors)
        where TError : class, IResultError
    {
        var errorType = typeof(TError);
        errors = this.errors.AsEnumerable().Where(e => e.GetType() == errorType).Cast<TError>();

        return errors.Any();
    }

    /// <summary>
    /// Gets the first error of a specific type from the Result.
    /// </summary>
    /// <typeparam name="TError">The type of error to retrieve.</typeparam>
    /// <returns>The first error of type TError if found; otherwise, null.</returns>
    /// <example>
    /// <code>
    /// var error = result.GetError{ValidationError}();
    /// if (error != null)
    /// {
    ///     Console.WriteLine($"First validation error: {error.Message}");
    /// }
    /// </code>
    /// </example>
    public TError GetError<TError>()
        where TError : class, IResultError
    {
        var errorType = typeof(TError);

        return this.errors.AsEnumerable().FirstOrDefault(e => e.GetType() == errorType) as TError;
    }

    /// <summary>
    /// Tries to get the first error of a specific type from the Result.
    /// </summary>
    /// <typeparam name="TError">The type of error to retrieve.</typeparam>
    /// <param name="error">When this method returns, contains the first error of type TError if found; otherwise, default.</param>
    /// <returns>True if an error of type TError was found; otherwise, false.</returns>
    /// <example>
    /// <code>
    /// if (result.TryGetError{ValidationError}(out var validationError))
    /// {
    ///     Console.WriteLine($"Validation failed: {validationError.Message}");
    /// }
    /// else
    /// {
    ///     Console.WriteLine("No validation error found");
    /// }
    /// </code>
    /// </example>
    public bool TryGetError<TError>(out TError error)
        where TError : class, IResultError
    {
        error = default;
        var foundError = this.errors.AsEnumerable().FirstOrDefault(e => e is TError);
        if (foundError is null)
        {
            return false;
        }

        error = foundError as TError;

        return true;
    }

    //bool IResult.TryGetError<TError>(out TError error)
    //{
    //    throw new NotImplementedException();
    //}

    //bool IResult.TryGetErrors<TError>(out IEnumerable<TError> errors)
    //{
    //    throw new NotImplementedException();
    //}

    /// <summary>
    /// Gets all errors of a specific type from the Result.
    /// </summary>
    /// <typeparam name="TError">The type of errors to retrieve.</typeparam>
    /// <returns>All errors of type TError. Empty enumerable if none found.</returns>
    /// <example>
    /// <code>
    /// foreach(var error in result.GetErrors{ValidationError}())
    /// {
    ///     Console.WriteLine($"Validation error: {error.Message}");
    /// }
    ///
    /// // Using with LINQ
    /// var errorMessages = result.GetErrors{ValidationError}()
    ///     .Select(e => e.Message)
    ///     .ToList();
    /// </code>
    /// </example>
    public IEnumerable<TError> GetErrors<TError>()
        where TError : class, IResultError
    {
        var errorType = typeof(TError);

        return this.errors.AsEnumerable().Where(e => e.GetType() == errorType).Cast<TError>();
    }

    /// <summary>
    ///     Creates a Result from an operation, handling any exceptions that occur.
    /// </summary>
    /// <param name="operation">The operation to execute.</param>
    /// <returns>A Result representing the outcome of the operation.</returns>
    /// <example>
    /// <code>
    /// var result = Result{int}.Try(() => int.Parse("42")); // Success(42)
    /// </code>
    /// </example>
    public static Result<T> Try(Func<T> operation)
    {
        if (operation is null)
        {
            return Failure()
                .WithError(new Error("Operation cannot be null"));
        }

        try
        {
            var result = operation();

            return Success(result);
        }
        catch (Exception ex)
        {
            return Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Creates a Result from an async operation, handling any exceptions that occur.
    /// </summary>
    /// <param name="operation">The async operation to execute.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A Result representing the outcome of the async operation.</returns>
    /// <example>
    /// <code>
    /// var result = await Result{User}.TryAsync(
    ///     async ct => await repository.GetUserAsync(userId, ct),
    ///     cancellationToken
    /// );
    /// </code>
    /// </example>
    public static async Task<Result<T>> TryAsync(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        if (operation is null)
        {
            return Failure()
                .WithError(new Error("Operation cannot be null"));
        }

        try
        {
            var result = await operation(cancellationToken);

            return Success(result);
        }
        catch (OperationCanceledException)
        {
            return Failure()
                .WithError(new OperationCancelledError())
                .WithMessage("Operation was cancelled");
        }
        catch (Exception ex)
        {
            return Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    /// Converts a generic Result{T} to a non-generic Result.
    /// </summary>
    /// <returns>A non-generic Result with the same success state, messages, and errors.</returns>
    /// <example>
    /// <code>
    /// var userResult = Result{User}.Success(user)
    ///     .WithMessage("User created");
    ///
    /// Result result = userResult.ToResult(); // Converts to non-generic Result
    /// Console.WriteLine(result.IsSuccess); // Still maintains success state
    /// </code>
    /// </example>
    public Result Unwrap() => this; // Uses implicit operator

    /// <summary>
    /// Creates a new Result{TOutput} from this Result.
    /// </summary>
    /// <typeparam name="TOutput">The type for the new Result.</typeparam>
    /// <returns>A new Result{TOutput} with the same success state, messages, and errors.</returns>
    /// <example>
    /// <code>
    /// var userResult = Result{User}.Success(user)
    ///     .WithMessage("User validated");
    ///
    /// // Convert to different type
    /// Result{UserDto} dtoResult = userResult.ToResult{UserDto}();
    /// </code>
    /// </example>
    public Result<TOutput> Wrap<TOutput>() => this.Wrap<TOutput>(default);

    /// <summary>
    /// Creates a new Result{TOutput} from this Result with a specified value.
    /// </summary>
    /// <typeparam name="TOutput">The type for the new Result.</typeparam>
    /// <param name="value">The value for the new Result.</param>
    /// <returns>A new Result{TOutput} with the same success state, messages, and errors, containing the specified value.</returns>
    /// <example>
    /// <code>
    /// var userResult = Result{User}.Success(user)
    ///     .WithMessage("User validated");
    ///
    /// var dto = new UserDto { Id = user.Id, Name = user.Name };
    /// var dtoResult = userResult.ToResult(dto); // Creates Result{UserDto}
    ///
    /// // Also useful for type conversion in chains
    /// var result = await GetUserAsync()
    ///     .Map(user => new UserDto(user))
    ///     .To(dto => new ApiResponse(dto));
    /// </code>
    /// </example>
    public Result<TOutput> Wrap<TOutput>(TOutput value)
    {
        var result = this; // struct cannot access this in lambda

        return this.Match(
            _ => Result<TOutput>.Success(value)
                .WithMessages(result.Messages)
                .WithErrors(result.Errors),
            _ => Result<TOutput>.Failure(value)
                .WithMessages(result.Messages)
                .WithErrors(result.Errors));
    }

    /// <summary>
    /// Returns a string representation of the Result, including its state, messages, and errors.
    /// </summary>
    /// <returns>A formatted string containing the Result's details.</returns>
    /// <example>
    /// <code>
    /// var result = Result{User}.Success(user)
    ///     .WithMessage("User created")
    ///     .WithMessage("Email sent");
    ///
    /// Console.WriteLine(result.ToString());
    /// // Output:
    /// // Success: True
    /// // Messages:
    /// // - User created
    /// // - Email sent
    ///
    /// var failed = Result{User}.Failure()
    ///     .WithError(new ValidationError("Invalid email"));
    ///
    /// Console.WriteLine(failed);
    /// // Output:
    /// // Success: False
    /// // Errors:
    /// // - [ValidationError] Invalid email
    /// </code>
    /// </example>
    public override string ToString() => this.ToString(string.Empty);

    public string ToString(string message)
    {
        var sb = new StringBuilder();

        // Append success or failure message
        if (this.IsSuccess)
        {
            sb.Append("Result succeeded ✓");
        }
        else
        {
            sb.Append("Result failed ✗");
        }

        // Append type name
        sb.Append(" [").Append(typeof(T).Name).Append(']');

        // Append message only if not null or empty
        if (!string.IsNullOrEmpty(message))
        {
            sb.Append(' ').Append(message);
        }

        // Remove trailing spaces and add a single newline
        while (sb.Length > 0 && sb[^1] == ' ')
        {
            sb.Length--;
        }
        sb.AppendLine();

        // Append messages if any
        if (!this.messages.IsEmpty)
        {
            sb.AppendLine("  messages:");
            foreach (var m in this.messages.AsEnumerable())
            {
                sb.Append("  - ").AppendLine(m);
            }
        }

        // Append errors if any
        if (!this.errors.IsEmpty)
        {
            sb.AppendLine("  errors:");
            foreach (var e in this.errors.AsEnumerable())
            {
                sb.Append("  - [").Append(e.GetType().Name).Append("] ").AppendLine(e.Message);
            }
        }

        return sb.ToString();
    }

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
    public static Result<T> Merge(params Result<T>[] results)
    {
        if (results is null || results.Length == 0)
        {
            return Success();
        }

        var isSuccess = true;
        T value = default;
        var hasValue = false;

        var combinedMessages = new ValueList<string>();
        var combinedErrors = new ValueList<IResultError>();

        foreach (var result in results)
        {
            // Only overwrite if the new value is not the default
            if (!EqualityComparer<T>.Default.Equals(result.Value, default))
            {
                value = result.Value;
                hasValue = true;
            }

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

        return new Result<T>(isSuccess, hasValue ? value : default, combinedMessages, combinedErrors);
    }
}