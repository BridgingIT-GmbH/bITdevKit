// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents the result of an operation, which can either be a success or a failure.
/// Contains functional methods to better work with success and failure results, as well as construct results from actions or tasks.
/// </summary>
public readonly partial struct Result
{
    /// <summary>
    ///     Creates a Result from an async operation, handling any exceptions that occur.
    /// </summary>
    /// <returns>A Result representing the outcome of the operation.</returns>
    /// <example>
    /// <code>
    /// var result = Result.From(() => {
    ///     userRepository.DeleteAll();
    /// });
    /// </code>
    /// </example>
    public static Result From(Action operation)
    {
        if (operation is null)
        {
            return Failure()
                .WithError(new Error("Operation cannot be null"));
        }

        try
        {
            operation();

            return Success();
        }
        catch (Exception ex)
        {
            return Failure()
                .WithError(Settings.ExceptionErrorFactory(ex))
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
    /// var result = await Result.FromAsync(
    ///     async ct => await DeleteAllUsersAsync(ct),
    ///     cancellationToken
    /// );
    /// </code>
    /// </example>
    public static async Task<Result> FromAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        if (operation is null)
        {
            return Failure()
                .WithError(new Error("Operation cannot be null"));
        }

        try
        {
            await operation(cancellationToken);

            return Success();
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
                .WithError(Settings.ExceptionErrorFactory(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    /// Throws an exception if the result indicates failure.
    /// </summary>
    /// <returns>The original result if it represents a success; otherwise, throws an exception.</returns>
    public Result ThrowIfFailed()
    {
        if (this.IsSuccess)
        {
            return this;
        }

        if (this.HasError())
        {
            this.Errors.FirstOrDefault()?.Throw();
        }

        throw new ResultException(this);
    }

    /// <summary>
    /// Throws a specified exception if the result indicates a failure.
    /// </summary>
    /// <typeparam name="TException">The type of exception to throw.</typeparam>
    /// <returns>The current Result if it indicates success.</returns>
    /// <exception>Thrown if the result indicates a failure.
    ///     <cref>TException</cref>
    /// </exception>
    public Result ThrowIfFailed<TException>()
        where TException : Exception
    {
        if (this.IsSuccess)
        {
            return this;
        }

        throw ((TException)Activator.CreateInstance(typeof(TException), this.Errors.FirstOrDefault()?.Message, this))!;
    }

    /// <summary>
    ///     Maps a successful Result to a Result{T} using the provided mapping function.
    /// </summary>
    /// <typeparam name="T">The type to map to.</typeparam>
    /// <param name="value">The value.</param>
    /// <returns>A new Result containing the mapped value or the original errors.</returns>
    /// <example>
    /// <code>
    /// var result = Result.Success();
    /// var stringResult = result.Map("42"); // Result{string}.Success("42")
    /// </code>
    /// </example>
    public Result<T> Map<T>(T value)
    {
        if (!this.IsSuccess)
        {
            return Result<T>.Failure()
                .WithErrors(this.Errors)
                .WithMessages(this.Messages);
        }

        try
        {
            return Result<T>.Success(value)
                .WithMessages(this.Messages);
        }
        catch (Exception ex)
        {
            return Result<T>.Failure()
                .WithError(Settings.ExceptionErrorFactory(ex))
                .WithMessages(this.Messages);
        }
    }

    /// <summary>
    ///     Maps a successful Result to a Result{T} using the provided mapping function.
    /// </summary>
    /// <typeparam name="T">The type to map to.</typeparam>
    /// <param name="mapper">The function to map the value.</param>
    /// <returns>A new Result containing the mapped value or the original errors.</returns>
    /// <example>
    /// <code>
    /// var result = Result.Success();
    /// var stringResult = result.Map(x => x.ToString()); // Result{string}.Success("42")
    /// </code>
    /// </example>
    public Result<T> Map<T>(Func<T> mapper)
    {
        if (!this.IsSuccess || mapper is null)
        {
            return Result<T>.Failure()
                .WithErrors(this.Errors)
                .WithMessages(this.Messages);
        }

        try
        {
            var value = mapper();

            return Result<T>.Success(value)
                .WithMessages(this.Messages);
        }
        catch (Exception ex)
        {
            return Result<T>.Failure()
                .WithError(Settings.ExceptionErrorFactory(ex))
                .WithMessages(this.Messages);
        }
    }

    /// <summary>
    ///     Ensures that a condition is met, converting to a failure if not.
    /// </summary>
    /// <param name="predicate">The condition that must be true.</param>
    /// <param name="error">The error to return if the predicate fails.</param>
    /// <returns>The original result if successful and predicate is met; otherwise, a failure with the specified error.</returns>
    /// <example>
    /// <code>
    /// var result = Result.Success()
    ///     .Ensure(() => userCount > 0, new Error("No users found"));
    /// </code>
    /// </example>
    public Result Ensure(Func<bool> predicate, IResultError error)
    {
        if (!this.IsSuccess)
        {
            return this;
        }

        if (predicate is null)
        {
            return Failure()
                .WithError(new Error("Predicate cannot be null"))
                .WithMessages(this.Messages);
        }

        try
        {
            return predicate()
                ? this
                : Failure()
                    .WithError(error ?? new Error("Predicate condition not met"))
                    .WithMessages(this.Messages);
        }
        catch (Exception ex)
        {
            return Failure()
                .WithError(Settings.ExceptionErrorFactory(ex))
                .WithMessages(this.Messages);
        }
    }

    /// <summary>
    ///     Ensures that an async condition is met, converting to a failure if not.
    /// </summary>
    /// <param name="predicate">The async condition that must be true.</param>
    /// <param name="error">The error to return if the predicate fails.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The original result if successful and predicate is met; otherwise, a failure with the specified error.</returns>
    /// <example>
    /// <code>
    /// var result = await Result.Success()
    ///     .EnsureAsync(
    ///         async ct => await ValidateSystemStateAsync(ct),
    ///         new Error("System in invalid state"),
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public async Task<Result> EnsureAsync(
        Func<CancellationToken, Task<bool>> predicate,
        IResultError error,
        CancellationToken cancellationToken = default)
    {
        if (!this.IsSuccess)
        {
            return this;
        }

        if (predicate is null)
        {
            return Failure()
                .WithError(new Error("Predicate cannot be null"))
                .WithMessages(this.Messages);
        }

        try
        {
            return await predicate(cancellationToken)
                ? this
                : Failure()
                    .WithError(error ?? new Error("Predicate condition not met"))
                    .WithMessages(this.Messages);
        }
        catch (OperationCanceledException)
        {
            return Failure()
                .WithError(new OperationCancelledError())
                .WithMessages(this.Messages);
        }
        catch (Exception ex)
        {
            return Failure()
                .WithError(Settings.ExceptionErrorFactory(ex))
                .WithMessages(this.Messages);
        }
    }

    /// <summary>
    ///     Wraps a synchronous operation in a Result, catching any exceptions.
    /// </summary>
    /// <param name="operation">The action to execute.</param>
    /// <returns>A Result indicating success or containing any caught exceptions.</returns>
    /// <example>
    /// <code>
    /// var result = Result.Try(() => InitializeSystem());
    /// </code>
    /// </example>
    public static Result Try(Action operation)
    {
        if (operation is null)
        {
            return Failure()
                .WithError(new Error("Operation cannot be null"));
        }

        try
        {
            operation();

            return Success();
        }
        catch (Exception ex)
        {
            return Failure()
                .WithError(Settings.ExceptionErrorFactory(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Wraps an async operation with cancellation support in a Result, catching any exceptions.
    /// </summary>
    /// <param name="operation">The async action to execute with cancellation support.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A Result indicating success or containing any caught exceptions.</returns>
    /// <example>
    /// <code>
    /// var result = await Result.TryAsync(
    ///     async ct => await InitializeSystemAsync(ct),
    ///     cancellationToken
    /// );
    /// </code>
    /// </example>
    public static async Task<Result> TryAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        if (operation is null)
        {
            return Failure()
                .WithError(new Error("Operation cannot be null"));
        }

        try
        {
            await operation(cancellationToken);

            return Success();
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
                .WithError(Settings.ExceptionErrorFactory(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Executes an action on a successful result without changing its value or state.
    ///     Useful for performing side effects like logging or monitoring.
    /// </summary>
    /// <param name="operation">The action to execute on success.</param>
    /// <returns>The original Result instance.</returns>
    /// <example>
    /// <code>
    /// var result = Result.Success()
    ///     .Tap(() => _logger.LogInformation("Operation successful"));
    /// </code>
    /// </example>
    public Result Tap(Action operation)
    {
        if (!this.IsSuccess || operation is null)
        {
            return this;
        }

        try
        {
            operation();

            return this;
        }
        catch (Exception ex)
        {
            return Failure()
                .WithError(Settings.ExceptionErrorFactory(ex))
                .WithMessages(this.Messages);
        }
    }

    /// <summary>
    ///     Asynchronously executes an action on a successful result without changing its value or state.
    /// </summary>
    /// <param name="operation">The async action to execute on success.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The original Result instance.</returns>
    /// <example>
    /// <code>
    /// var result = await Result.Success()
    ///     .TapAsync(
    ///         async ct => await _logger.LogAsync("Success", ct),
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public async Task<Result> TapAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        if (!this.IsSuccess || operation is null)
        {
            return this;
        }

        try
        {
            await operation(cancellationToken);

            return this;
        }
        catch (OperationCanceledException)
        {
            return Failure()
                .WithError(new OperationCancelledError())
                .WithMessages(this.Messages);
        }
        catch (Exception ex)
        {
            return Failure()
                .WithError(Settings.ExceptionErrorFactory(ex))
                .WithMessages(this.Messages);
        }
    }

    /// <summary>
    ///     Maps a success value while performing a side effect.
    /// </summary>
    /// <param name="onSuccess">The action to perform on success.</param>
    /// <param name="onFailure">The action to perform on failure.</param>
    /// <returns>The original Result instance.</returns>
    /// <example>
    /// <code>
    /// var result = Result.Success()
    ///     .TeeMap(
    ///         () => _logger.LogInformation("Success"),
    ///         errors => _logger.LogError("Operation failed", errors)
    ///     );
    /// </code>
    /// </example>
    public Result TeeMap(
        Action onSuccess,
        Action<IReadOnlyList<IResultError>> onFailure)
    {
        try
        {
            if (this.IsSuccess)
            {
                onSuccess?.Invoke();
            }
            else
            {
                onFailure?.Invoke(this.Errors);
            }

            return this;
        }
        catch (Exception ex)
        {
            return Failure()
                .WithError(Settings.ExceptionErrorFactory(ex))
                .WithMessages(this.Messages);
        }
    }

    /// <summary>
    ///     Asynchronously maps a success value while performing a side effect.
    /// </summary>
    /// <param name="onSuccess">The async action to perform on success.</param>
    /// <param name="onFailure">The async action to perform on failure.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The original Result instance.</returns>
    /// <example>
    /// <code>
    /// var result = await Result.Success()
    ///     .TeeMapAsync(
    ///         async ct => await _logger.LogAsync("Success", ct),
    ///         async (errors, ct) => await _logger.LogErrorsAsync(errors, ct),
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public async Task<Result> TeeMapAsync(
        Func<CancellationToken, Task> onSuccess,
        Func<IReadOnlyList<IResultError>, CancellationToken, Task> onFailure,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (this.IsSuccess)
            {
                if (onSuccess is not null)
                {
                    await onSuccess(cancellationToken);
                }
            }
            else
            {
                if (onFailure is not null)
                {
                    await onFailure(this.Errors, cancellationToken);
                }
            }

            return this;
        }
        catch (OperationCanceledException)
        {
            return Failure()
                .WithError(new OperationCancelledError())
                .WithMessages(this.Messages);
        }
        catch (Exception ex)
        {
            return Failure()
                .WithError(Settings.ExceptionErrorFactory(ex))
                .WithMessages(this.Messages);
        }
    }

    /// <summary>
    ///     Converts a successful result to a failure if the specified predicate is not met.
    /// </summary>
    /// <param name="predicate">The condition that must be true for the result to remain successful.</param>
    /// <param name="error">The error to return if the predicate fails.</param>
    /// <returns>The original result if successful and predicate is met; otherwise, a failure with the specified error.</returns>
    /// <example>
    /// <code>
    /// var result = Result.Success()
    ///     .Filter(() => isSystemReady, new Error("System not ready"));
    /// </code>
    /// </example>
    public Result Filter(Func<bool> predicate, IResultError error)
    {
        if (!this.IsSuccess || predicate is null)
        {
            return this;
        }

        try
        {
            return predicate()
                ? this
                : Failure()
                    .WithError(error ?? new Error("Predicate condition not met"))
                    .WithMessages(this.Messages);
        }
        catch (Exception ex)
        {
            return Failure()
                .WithError(Settings.ExceptionErrorFactory(ex))
                .WithMessages(this.Messages);
        }
    }

    /// <summary>
    ///     Asynchronously tests a condition and converts a successful result to a failure if the predicate is not met.
    /// </summary>
    /// <param name="predicate">The async condition that must be true.</param>
    /// <param name="error">The error to return if the predicate fails.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The original result if successful and predicate is met; otherwise, a failure with the specified error.</returns>
    /// <example>
    /// <code>
    /// var result = await Result.Success()
    ///     .FilterAsync(
    ///         async ct => await IsSystemReadyAsync(ct),
    ///         new Error("System not ready"),
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public async Task<Result> FilterAsync(
        Func<CancellationToken, Task<bool>> predicate,
        IResultError error,
        CancellationToken cancellationToken = default)
    {
        if (!this.IsSuccess || predicate is null)
        {
            return this;
        }

        try
        {
            return await predicate(cancellationToken)
                ? this
                : Failure()
                    .WithError(error ?? new Error("Predicate condition not met"))
                    .WithMessages(this.Messages);
        }
        catch (OperationCanceledException)
        {
            return Failure()
                .WithError(new OperationCancelledError())
                .WithMessages(this.Messages);
        }
        catch (Exception ex)
        {
            return Failure()
                .WithError(Settings.ExceptionErrorFactory(ex))
                .WithMessages(this.Messages);
        }
    }

    /// <summary>
    ///     Converts a successful result to a failure if the specified predicate is met.
    /// </summary>
    /// <param name="predicate">The condition that must be false for the result to remain successful.</param>
    /// <param name="error">The error to return if the predicate succeeds.</param>
    /// <returns>The original result if successful and predicate is not met; otherwise, a failure with the specified error.</returns>
    /// <example>
    /// <code>
    /// var result = Result.Success()
    ///     .Unless(() => systemIsDown, new Error("System is down"));
    /// </code>
    /// </example>
    public Result Unless(Func<bool> predicate, IResultError error)
    {
        if (!this.IsSuccess || predicate is null)
        {
            return this;
        }

        try
        {
            return predicate()
                ? Failure()
                    .WithError(error ?? new Error("Unless condition met"))
                    .WithMessages(this.Messages)
                : this;
        }
        catch (Exception ex)
        {
            return Failure()
                .WithError(Settings.ExceptionErrorFactory(ex))
                .WithMessages(this.Messages);
        }
    }

    /// <summary>
    ///     Asynchronously tests a condition and converts a successful result to a failure if the predicate is met.
    /// </summary>
    /// <param name="predicate">The async condition that must be false.</param>
    /// <param name="error">The error to return if the predicate succeeds.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The original result if successful and predicate is not met; otherwise, a failure with the specified error.</returns>
    /// <example>
    /// <code>
    /// var result = await Result.Success()
    ///     .UnlessAsync(
    ///         async ct => await IsSystemDownAsync(ct),
    ///         new Error("System is down"),
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public async Task<Result> UnlessAsync(
        Func<CancellationToken, Task<bool>> predicate,
        IResultError error,
        CancellationToken cancellationToken = default)
    {
        if (!this.IsSuccess || predicate is null)
        {
            return this;
        }

        try
        {
            return await predicate(cancellationToken)
                ? Failure()
                    .WithError(error ?? new Error("Unless condition met"))
                    .WithMessages(this.Messages)
                : this;
        }
        catch (OperationCanceledException)
        {
            return Failure()
                .WithError(new OperationCancelledError())
                .WithMessages(this.Messages);
        }
        catch (Exception ex)
        {
            return Failure()
                .WithError(Settings.ExceptionErrorFactory(ex))
                .WithMessages(this.Messages);
        }
    }

    /// <summary>
    ///     Chains a new operation if the current Result is successful.
    /// </summary>
    /// <param name="operation">The operation to execute if successful.</param>
    /// <returns>The result of the next operation if successful; otherwise, a failure.</returns>
    /// <example>
    /// <code>
    /// var result = Result.Success()
    ///     .AndThen(() => ValidateUser(user))
    ///     .AndThen(() => SaveUser(user));
    /// </code>
    /// </example>
    public Result AndThen(Func<Result> operation)
    {
        if (!this.IsSuccess || operation is null)
        {
            return this;
        }

        try
        {
            return operation();
        }
        catch (Exception ex)
        {
            return Failure()
                .WithError(Settings.ExceptionErrorFactory(ex))
                .WithMessages(this.Messages);
        }
    }

    /// <summary>
    ///     Chains a new async operation if the current Result is successful.
    /// </summary>
    /// <param name="operation">The async operation to execute if successful.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The result of the next operation if successful; otherwise, a failure.</returns>
    /// <example>
    /// <code>
    /// var result = await Result.Success()
    ///     .AndThenAsync(
    ///         async (ct) => await ValidateUserAsync(user),
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public async Task<Result> AndThenAsync(
        Func<CancellationToken, Task<Result>> operation,
        CancellationToken cancellationToken = default)
    {
        if (!this.IsSuccess || operation is null)
        {
            return this;
        }

        try
        {
            return await operation(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Failure()
                .WithError(new OperationCancelledError())
                .WithMessages(this.Messages);
        }
        catch (Exception ex)
        {
            return Failure()
                .WithError(Settings.ExceptionErrorFactory(ex))
                .WithMessages(this.Messages);
        }
    }

    /// <summary>
    ///     Provides a fallback result in case of failure.
    /// </summary>
    /// <param name="fallback">The result to return if this result is a failure.</param>
    /// <returns>The original result if successful; otherwise, the fallback result.</returns>
    /// <example>
    /// <code>
    /// var result = Result.Failure("Primary failed")
    ///     .OrElse(() => GetBackupResult());
    /// </code>
    /// </example>
    public Result OrElse(Func<Result> fallback)
    {
        if (this.IsSuccess || fallback is null)
        {
            return this;
        }

        try
        {
            var fallbackResult = fallback();

            return fallbackResult
                .WithMessages(this.Messages);
        }
        catch (Exception ex)
        {
            return Failure()
                .WithError(Settings.ExceptionErrorFactory(ex))
                .WithMessages(this.Messages);
        }
    }

    /// <summary>
    ///     Provides an async fallback result in case of failure.
    /// </summary>
    /// <param name="fallback">The async operation returning a result to use if this result is a failure.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The original result if successful; otherwise, the fallback result.</returns>
    /// <example>
    /// <code>
    /// var result = await Result.Failure("Primary failed")
    ///     .OrElseAsync(
    ///         async ct => await GetBackupResultAsync(ct),
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public async Task<Result> OrElseAsync(
        Func<CancellationToken, Task<Result>> fallback,
        CancellationToken cancellationToken = default)
    {
        if (this.IsSuccess || fallback is null)
        {
            return this;
        }

        try
        {
            var fallbackResult = await fallback(cancellationToken);

            return fallbackResult
                .WithMessages(this.Messages);
        }
        catch (OperationCanceledException)
        {
            return Failure()
                .WithError(new OperationCancelledError())
                .WithMessages(this.Messages);
        }
        catch (Exception ex)
        {
            return Failure()
                .WithError(Settings.ExceptionErrorFactory(ex))
                .WithMessages(this.Messages);
        }
    }

    /// <summary>
    ///     Executes an operation in the chain without transforming the value.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    /// <returns>The original Result.</returns>
    /// <example>
    /// <code>
    /// var result = Result.Success()
    ///     .Do(() => InitializeSystem(user))
    ///     .Then(user => ProcessUser(user));
    /// </code>
    /// </example>
    public Result Do(Action action)
    {
        if (!this.IsSuccess || action is null)
        {
            return this;
        }

        try
        {
            action();

            return this;
        }
        catch (Exception ex)
        {
            return Failure()
                .WithError(Settings.ExceptionErrorFactory(ex))
                .WithMessages(this.Messages);
        }
    }

    /// <summary>
    ///     Executes an async operation in the chain without transforming the value.
    /// </summary>
    /// <param name="action">The async action to execute.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The original Result.</returns>
    /// <example>
    /// <code>
    /// var result = await Result.Success()
    ///     .DoAsync(
    ///         async ct => await InitializeSystemAsync(user, ct),
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public async Task<Result> DoAsync(
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        if (!this.IsSuccess || action is null)
        {
            return this;
        }

        try
        {
            await action(cancellationToken);

            return this;
        }
        catch (OperationCanceledException)
        {
            return Failure()
                .WithError(new OperationCancelledError())
                .WithMessages(this.Messages);
        }
        catch (Exception ex)
        {
            return Failure()
                .WithError(Settings.ExceptionErrorFactory(ex))
                .WithMessages(this.Messages);
        }
    }

    /// <summary>
    ///     Executes an action only if a condition is met, without changing the Result.
    /// </summary>
    /// <param name="condition">The condition to check.</param>
    /// <param name="action">The action to execute if condition is met.</param>
    /// <returns>The original Result.</returns>
    /// <example>
    /// <code>
    /// var result = Result.Success()
    ///     .Switch(
    ///         () => user.IsAdmin,
    ///         () => _logger.LogInfo($"Admin {user.Name} accessed the system")
    ///     );
    /// </code>
    /// </example>
    public Result Switch(Func<bool> condition, Action action)
    {
        if (!this.IsSuccess || condition is null || action is null)
        {
            return this;
        }

        try
        {
            if (condition())
            {
                action();
            }

            return this;
        }
        catch (Exception ex)
        {
            return Failure()
                .WithError(Settings.ExceptionErrorFactory(ex))
                .WithMessages(this.Messages);
        }
    }

    /// <summary>
    ///     Executes an async action only if a condition is met, without changing the Result.
    /// </summary>
    /// <param name="condition">The condition to check.</param>
    /// <param name="action">The async action to execute if condition is met.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The original Result.</returns>
    /// <example>
    /// <code>
    /// var result = await Result.Success()
    ///     .SwitchAsync(
    ///         () => user.IsAdmin,
    ///         async (uct) => await NotifyAdminLoginAsync(user),
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public async Task<Result> SwitchAsync(
        Func<bool> condition,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        if (!this.IsSuccess || condition is null || action is null)
        {
            return this;
        }

        try
        {
            if (condition())
            {
                await action(cancellationToken);
            }

            return this;
        }
        catch (OperationCanceledException)
        {
            return Failure()
                .WithError(new OperationCancelledError())
                .WithMessages(this.Messages);
        }
        catch (Exception ex)
        {
            return Failure()
                .WithError(Settings.ExceptionErrorFactory(ex))
                .WithMessages(this.Messages);
        }
    }
}