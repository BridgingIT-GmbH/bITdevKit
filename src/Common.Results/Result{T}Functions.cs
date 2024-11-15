// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using FluentValidation;
using FluentValidation.Internal;

/// <summary>
/// Represents the result of an operation, which can either be a success or a failure.
/// Contains functional methods to better work with success and failure results and their values, as well as construct results from actions or tasks.
/// </summary>
public readonly partial struct Result<T>
{
    /// <summary>
    ///     Maps a successful Result{T} to a Result{TNew} using the provided mapping function.
    /// </summary>
    /// <typeparam name="TNew">The type to map to.</typeparam>
    /// <param name="mapper">The function to map the value.</param>
    /// <returns>A new Result containing the mapped value or the original errors.</returns>
    /// <example>
    /// <code>
    /// var intResult = Result{int}.Success(42);
    /// var stringResult = intResult.Map(x => x.ToString()); // Result{string}.Success("42")
    /// </code>
    /// </example>
    public Result<TNew> Map<TNew>(Func<T, TNew> mapper)
    {
        if (!this.IsSuccess || mapper is null)
        {
            return Result<TNew>.Failure()
                .WithErrors(this.Errors)
                .WithMessages(this.Messages);
        }

        try
        {
            var newValue = mapper(this.Value);

            return Result<TNew>.Success(newValue)
                .WithMessages(this.Messages);
        }
        catch (Exception ex)
        {
            return Result<TNew>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(this.Messages);
        }
    }

    /// <summary>
    ///     Asynchronously maps a successful Result{T} to a Result{TNew} using the provided mapping function.
    /// </summary>
    /// <typeparam name="TNew">The type to map to.</typeparam>
    /// <param name="mapper">The async function to map the value.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A new Result containing the mapped value or the original errors.</returns>
    /// <example>
    /// <code>
    /// var intResult = Result{int}.Success(42);
    /// var stringResult = await intResult.MapAsync(
    ///     async (x, ct) => await FormatNumberAsync(x),
    ///     cancellationToken
    /// );
    /// </code>
    /// </example>
    public async Task<Result<TNew>> MapAsync<TNew>(
        Func<T, CancellationToken, Task<TNew>> mapper,
        CancellationToken cancellationToken = default)
    {
        if (!this.IsSuccess || mapper is null)
        {
            return Result<TNew>.Failure()
                .WithErrors(this.Errors)
                .WithMessages(this.Messages);
        }

        try
        {
            var newValue = await mapper(this.Value, cancellationToken);

            return Result<TNew>.Success(newValue)
                .WithMessages(this.Messages);
        }
        catch (OperationCanceledException)
        {
            return Result<TNew>.Failure()
                .WithError(new OperationCancelledError())
                .WithMessages(this.Messages);
        }
        catch (Exception ex)
        {
            return Result<TNew>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(this.Messages);
        }
    }

    /// <summary>
    ///     Binds a successful Result{T} to another Result{TNew} using the provided binding function.
    /// </summary>
    /// <typeparam name="TNew">The type of the new Result value.</typeparam>
    /// <param name="binder">The function to bind the value to a new Result.</param>
    /// <returns>The bound Result or a failure containing the original errors.</returns>
    /// <example>
    /// <code>
    /// var result = Result{int}.Success(42)
    ///     .Bind(x => x > 0
    ///         ? Result{string}.Success(x.ToString())
    ///         : Result{string}.Failure("Value must be positive"));
    /// </code>
    /// </example>
    public Result<TNew> Bind<TNew>(Func<T, Result<TNew>> binder)
    {
        if (!this.IsSuccess || binder is null)
        {
            return Result<TNew>.Failure()
                .WithErrors(this.Errors)
                .WithMessages(this.Messages);
        }

        try
        {
            var result = binder(this.Value);

            return result.WithMessages(this.Messages);
        }
        catch (Exception ex)
        {
            return Result<TNew>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(this.Messages);
        }
    }

    /// <summary>
    ///     Asynchronously binds a successful Result{T} to another Result{TNew}.
    /// </summary>
    /// <typeparam name="TNew">The type of the new Result value.</typeparam>
    /// <param name="binder">The async function to bind the value to a new Result.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The bound Result or a failure containing the original errors.</returns>
    /// <example>
    /// <code>
    /// var result = await Result{int}.Success(42)
    ///     .BindAsync(async (x, ct) =>
    ///         await ValidateAndTransformAsync(x),
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public async Task<Result<TNew>> BindAsync<TNew>(
        Func<T, CancellationToken, Task<Result<TNew>>> binder,
        CancellationToken cancellationToken = default)
    {
        if (!this.IsSuccess || binder is null)
        {
            return Result<TNew>.Failure()
                .WithErrors(this.Errors)
                .WithMessages(this.Messages);
        }

        try
        {
            var result = await binder(this.Value, cancellationToken);

            return result.WithMessages(this.Messages);
        }
        catch (OperationCanceledException)
        {
            return Result<TNew>.Failure()
                .WithError(new OperationCancelledError())
                .WithMessages(this.Messages);
        }
        catch (Exception ex)
        {
            return Result<TNew>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(this.Messages);
        }
    }

    /// <summary>
    /// Throws a <see cref="ResultException"/> if the current result is a failure.
    /// </summary>
    /// <returns>The current result if it is successful.</returns>
    /// <exception cref="ResultException">Thrown if the current result is a failure.</exception>
    public Result<T> ThrowIfFailed()
    {
        if (this.IsSuccess)
        {
            return this;
        }

        throw new ResultException(this);
    }

    /// <summary>
    /// Throws an exception of type TException if the result is a failure.
    /// </summary>
    /// <typeparam name="TException">The type of exception to throw.</typeparam>
    /// <returns>The current Result if it indicates success.</returns>
    /// <exception>Thrown if the result indicates a failure.
    ///     <cref>TException</cref>
    /// </exception>
    public Result<T> ThrowIfFailed<TException>()
        where TException : Exception
    {
        if (this.IsSuccess)
        {
            return this;
        }

        throw ((TException)Activator.CreateInstance(typeof(TException), this.Errors.FirstOrDefault()?.Message, this))!;
    }

    /// <summary>
    ///     Ensures that a condition is met for the contained value, converting to a failure if not.
    /// </summary>
    /// <param name="predicate">The condition that must be true for the contained value.</param>
    /// <param name="error">The error to return if the predicate fails.</param>
    /// <returns>The original result if successful and predicate is met; otherwise, a failure with the specified error.</returns>
    /// <example>
    /// <code>
    /// var result = Result{int}.Success(42)
    ///     .Ensure(x => x > 0, new Error("Value must be positive"));
    /// </code>
    /// </example>
    public Result<T> Ensure(
        Func<T, bool> predicate,
        IResultError error)
    {
        if (!this.IsSuccess)
        {
            return this;
        }

        if (predicate is null)
        {
            return Failure(this.Value)
                .WithError(new Error("Predicate cannot be null"))
                .WithMessages(this.Messages);
        }

        try
        {
            return predicate(this.Value)
                ? this
                : Failure(this.Value)
                    .WithError(error ?? new Error("Predicate condition not met"))
                    .WithMessages(this.Messages);
        }
        catch (Exception ex)
        {
            return Failure(this.Value)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(this.Messages);
        }
    }

    /// <summary>
    ///     Asynchronously ensures that a condition is met for the contained value.
    /// </summary>
    /// <param name="predicate">The async condition that must be true for the contained value.</param>
    /// <param name="error">The error to return if the predicate fails.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The original result if successful and predicate is met; otherwise, a failure with the specified error.</returns>
    /// <example>
    /// <code>
    /// var result = await Result{User}.Success(user)
    ///     .EnsureAsync(
    ///         async (u, ct) => await IsValidUserAsync(u),
    ///         new ValidationError("Invalid user"),
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public async Task<Result<T>> EnsureAsync(
        Func<T, CancellationToken, Task<bool>> predicate,
        IResultError error,
        CancellationToken cancellationToken = default)
    {
        if (!this.IsSuccess)
        {
            return this;
        }

        if (predicate is null)
        {
            return Failure(this.Value)
                .WithError(new Error("Predicate cannot be null"))
                .WithMessages(this.Messages);
        }

        try
        {
            return await predicate(this.Value, cancellationToken)
                ? this
                : Failure(this.Value)
                    .WithError(error ?? new Error("Predicate condition not met"))
                    .WithMessages(this.Messages);
        }
        catch (OperationCanceledException)
        {
            return Failure(this.Value)
                .WithError(new OperationCancelledError())
                .WithMessages(this.Messages);
        }
        catch (Exception ex)
        {
            return Failure(this.Value)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(this.Messages);
        }
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
    ///     Executes an action with the current value if the result is successful, without changing the result.
    ///     Useful for performing side effects like logging or monitoring.
    /// </summary>
    /// <param name="operation">The action to execute with the successful value.</param>
    /// <returns>The original Result instance.</returns>
    /// <example>
    /// <code>
    /// var result = Result{User}.Success(user)
    ///     .Tap(user => _logger.LogInformation($"Retrieved user {user.Id}"))
    ///     .Tap(user => _metrics.IncrementUserRetrievals());
    /// </code>
    /// </example>
    public Result<T> Tap(Action<T> operation)
    {
        if (!this.IsSuccess || operation is null)
        {
            return this;
        }

        try
        {
            operation(this.Value);

            return this;
        }
        catch (Exception ex)
        {
            return Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(this.Messages);
        }
    }

    /// <summary>
    ///     Asynchronously executes an action with the current value if the result is successful.
    /// </summary>
    /// <param name="operation">The async action to execute with the successful value.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A Task containing the original Result instance.</returns>
    /// <example>
    /// <code>
    /// var result = await Result{User}.Success(user)
    ///     .TapAsync(
    ///         async (user, ct) => await _cache.StoreUserAsync(user),
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public async Task<Result<T>> TapAsync(
        Func<T, CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        if (!this.IsSuccess || operation is null)
        {
            return this;
        }

        try
        {
            await operation(this.Value, cancellationToken);

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
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(this.Messages);
        }
    }

    /// <summary>
    ///     Maps a success value while performing a side effect, useful for logging or monitoring during transformations.
    ///     Both the mapping and the side effect only occur if the Result is successful.
    /// </summary>
    /// <typeparam name="TNew">The type of the new result value.</typeparam>
    /// <param name="mapper">The function to transform the value.</param>
    /// <param name="operation">The action to perform with the transformed value.</param>
    /// <returns>A new Result containing the transformed value.</returns>
    /// <example>
    /// <code>
    /// var userDto = Result{User}.Success(user)
    ///     .TeeMap(
    ///         user => user.ToDto(),
    ///         dto => _logger.LogInformation($"Mapped user {dto.Id} to DTO")
    ///     );
    /// </code>
    /// </example>
    public Result<TNew> TeeMap<TNew>(
        Func<T, TNew> mapper,
        Action<TNew> operation)
    {
        if (!this.IsSuccess || mapper is null)
        {
            return Result<TNew>.Failure()
                .WithErrors(this.Errors)
                .WithMessages(this.Messages);
        }

        try
        {
            var newValue = mapper(this.Value);
            operation?.Invoke(newValue);

            return Result<TNew>.Success(newValue)
                .WithMessages(this.Messages);
        }
        catch (Exception ex)
        {
            return Result<TNew>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(this.Messages);
        }
    }

    /// <summary>
    ///     Asynchronously maps a success value while performing a side effect.
    ///     Both the mapping and the side effect only occur if the Result is successful.
    /// </summary>
    /// <typeparam name="TNew">The type of the new result value.</typeparam>
    /// <param name="mapper">The function to transform the value.</param>
    /// <param name="operation">The async action to perform with the transformed value.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A new Result containing the transformed value.</returns>
    /// <example>
    /// <code>
    /// var userDto = await Result{User}.Success(user)
    ///     .TeeMapAsync(
    ///         user => user.ToDto(),
    ///         async (dto, ct) => await _cache.StoreDtoAsync(dto),
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public async Task<Result<TNew>> TeeMapAsync<TNew>(
        Func<T, TNew> mapper,
        Func<TNew, CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        if (!this.IsSuccess || mapper is null)
        {
            return Result<TNew>.Failure()
                .WithErrors(this.Errors)
                .WithMessages(this.Messages);
        }

        try
        {
            var newValue = mapper(this.Value);

            if (operation is not null)
            {
                await operation(newValue, cancellationToken);
            }

            return Result<TNew>.Success(newValue)
                .WithMessages(this.Messages);
        }
        catch (OperationCanceledException)
        {
            return Result<TNew>.Failure()
                .WithError(new OperationCancelledError())
                .WithMessages(this.Messages);
        }
        catch (Exception ex)
        {
            return Result<TNew>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(this.Messages);
        }
    }

    /// <summary>
    ///     Filters a successful result based on a predicate condition.
    ///     Converts to failure if the predicate is not met.
    /// </summary>
    /// <param name="operation">The condition that must be true for the result to remain successful.</param>
    /// <param name="error">The error to return if the predicate fails.</param>
    /// <returns>The original result if successful and predicate is met; otherwise, a failure with the specified error.</returns>
    /// <example>
    /// <code>
    /// var result = Result{User}.Success(user)
    ///     .Filter(
    ///         user => user.IsActive,
    ///         new InactiveUserError("Account is not active")
    ///     );
    /// </code>
    /// </example>
    public Result<T> Filter(Func<T, bool> operation, IResultError error)
    {
        if (!this.IsSuccess || operation is null)
        {
            return this;
        }

        try
        {
            if (!operation(this.Value))
            {
                return Failure(this.Value)
                    .WithError(error ?? new Error("Predicate condition not met"))
                    .WithMessages(this.Messages);
            }
        }
        catch (Exception ex)
        {
            return Failure(this.Value)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(this.Messages);
        }

        return this;
    }

    /// <summary>
    ///     Asynchronously filters a successful result based on a predicate condition.
    /// </summary>
    /// <param name="operation">The async condition that must be true for the result to remain successful.</param>
    /// <param name="error">The error to return if the predicate fails.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The original result if successful and predicate is met; otherwise, a failure with the specified error.</returns>
    /// <example>
    /// <code>
    /// var result = await Result{User}.Success(user)
    ///     .FilterAsync(
    ///         async (user, ct) => await HasValidSubscriptionAsync(user),
    ///         new SubscriptionError("No valid subscription found"),
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public async Task<Result<T>> FilterAsync(
        Func<T, CancellationToken, Task<bool>> operation,
        IResultError error,
        CancellationToken cancellationToken = default)
    {
        if (!this.IsSuccess || operation is null)
        {
            return this;
        }

        try
        {
            if (!await operation(this.Value, cancellationToken))
            {
                return Failure(this.Value)
                    .WithError(error ?? new Error("Predicate condition not met"))
                    .WithMessages(this.Messages);
            }
        }
        catch (OperationCanceledException)
        {
            return Failure(this.Value)
                .WithError(new OperationCancelledError())
                .WithMessages(this.Messages);
        }
        catch (Exception ex)
        {
            return Failure(this.Value)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(this.Messages);
        }

        return this;
    }

    /// <summary>
    ///     Converts a successful result to a failure if the specified predicate is met (inverse of Filter).
    /// </summary>
    /// <param name="operation">The condition that must be false for the result to remain successful.</param>
    /// <param name="error">The error to return if the predicate succeeds.</param>
    /// <returns>The original result if successful and predicate is not met; otherwise, a failure with the specified error.</returns>
    /// <example>
    /// <code>
    /// var result = Result{User}.Success(user)
    ///     .Unless(
    ///         user => user.IsBlocked,
    ///         new BlockedUserError("Account is blocked")
    ///     );
    /// </code>
    /// </example>
    public Result<T> Unless(Func<T, bool> operation, IResultError error)
    {
        if (!this.IsSuccess || operation is null)
        {
            return this;
        }

        try
        {
            if (operation(this.Value))
            {
                return Failure(this.Value)
                    .WithError(error ?? new Error("Unless condition met"))
                    .WithMessages(this.Messages);
            }
        }
        catch (Exception ex)
        {
            return Failure(this.Value)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(this.Messages);
        }

        return this;
    }

    /// <summary>
    ///     Asynchronously converts a successful result to a failure if the specified predicate is met.
    /// </summary>
    /// <param name="operation">The async condition that must be false for the result to remain successful.</param>
    /// <param name="error">The error to return if the predicate succeeds.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The original result if successful and predicate is not met; otherwise, a failure with the specified error.</returns>
    /// <example>
    /// <code>
    /// var result = await Result{User}.Success(user)
    ///     .UnlessAsync(
    ///         async (user, ct) => await IsBlacklistedAsync(user),
    ///         new BlacklistError("User is blacklisted"),
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public async Task<Result<T>> UnlessAsync(
        Func<T, CancellationToken, Task<bool>> operation,
        IResultError error,
        CancellationToken cancellationToken = default)
    {
        if (!this.IsSuccess || operation is null)
        {
            return this;
        }

        try
        {
            if (await operation(this.Value, cancellationToken))
            {
                return Failure(this.Value)
                    .WithError(error ?? new Error("Unless condition met"))
                    .WithMessages(this.Messages);
            }
        }
        catch (OperationCanceledException)
        {
            return Failure(this.Value)
                .WithError(new OperationCancelledError())
                .WithMessages(this.Messages);
        }
        catch (Exception ex)
        {
            return Failure(this.Value)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(this.Messages);
        }

        return this;
    }

    /// <summary>
    ///     Chains a new operation if the current Result is successful, providing access to the previous Result's value.
    /// </summary>
    /// <typeparam name="T">The type of the previous result's value.</typeparam>
    /// <param name="operation">The operation to execute if successful, with access to the previous value.</param>
    /// <returns>The original Result if successful.</returns>
    /// <example>
    /// <code>
    /// var result = user.ToResult()
    ///     .AndThen(user => ValidateUser(user))
    ///     .AndThen(user => SaveUser(user));
    /// </code>
    /// </example>
    public Result<T> AndThen(Action<T> operation)
    {
        if (!this.IsSuccess || operation is null)
        {
            return this;
        }

        try
        {
            operation(this.Value);
        }
        catch (Exception ex)
        {
            return Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(this.Messages);
        }

        return this;
    }

    /// <summary>
    ///     Chains a new async operation if the current Result is successful, providing access to the previous Result's value.
    /// </summary>
    /// <typeparam name="T">The type of the previous result's value.</typeparam>
    /// <param name="operation">The async operation to execute if successful, with access to the previous value.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The original Result if successful.</returns>
    /// <example>
    /// <code>
    /// var result = await user.ToResult()
    ///     .AndThenAsync(
    ///         async (user, ct) => await ValidateUserAsync(user),
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public async Task<Result<T>> AndThenAsync(
        Func<T, CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        if (!this.IsSuccess || operation is null)
        {
            return this;
        }

        try
        {
            await operation(this.Value, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Failure()
                .WithError(new OperationCancelledError("Operation was cancelled"))
                .WithMessages(this.Messages);
        }
        catch (Exception ex)
        {
            return Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(this.Messages);
        }

        return this;
    }

    /// <summary>
    ///     Provides a fallback value in case of failure.
    /// </summary>
    /// <param name="operation">Function to provide an alternative value if this result is a failure.</param>
    /// <returns>The original result if successful; otherwise, a success with the fallback value.</returns>
    /// <example>
    /// <code>
    /// var user = Result{User}.Failure("User not found")
    ///     .OrElse(() => User.GetDefaultUser());
    /// </code>
    /// </example>
    public Result<T> OrElse(Func<T> operation)
    {
        if (this.IsSuccess || operation is null)
        {
            return this;
        }

        try
        {
            var fallbackValue = operation();

            return Success(fallbackValue)
                .WithMessages(this.Messages);
        }
        catch (Exception ex)
        {
            return Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(this.Messages);
        }
    }

    /// <summary>
    ///     Provides an async fallback value in case of failure.
    /// </summary>
    /// <param name="operation">Async function to provide an alternative value if this result is a failure.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The original result if successful; otherwise, a success with the fallback value.</returns>
    /// <example>
    /// <code>
    /// var user = await Result{User}.Failure("Not in cache")
    ///     .OrElseAsync(
    ///         async (ct) => await LoadUserFromDatabaseAsync(),
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public async Task<Result<T>> OrElseAsync(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        if (this.IsSuccess || operation is null)
        {
            return this;
        }

        try
        {
            var fallbackValue = await operation(cancellationToken);

            return Success(fallbackValue)
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
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
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
    /// var result = Result{User}.Success(user)
    ///     .Do(() => InitializeSystem())
    ///     .Then(user => ProcessUser(user));
    /// </code>
    /// </example>
    public Result<T> Do(Action action)
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
            return Failure(this.Value)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
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
    /// var result = await Result{User}.Success(user)
    ///     .DoAsync(
    ///         async ct => await InitializeSystemAsync(ct),
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public async Task<Result<T>> DoAsync(
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
            return Failure(this.Value)
                .WithError(new OperationCancelledError())
                .WithMessages(this.Messages);
        }
        catch (Exception ex)
        {
            return Failure(this.Value)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(this.Messages);
        }
    }

    /// <summary>
    ///     Maps both success and failure cases simultaneously.
    /// </summary>
    /// <typeparam name="TNew">The type of the new result value.</typeparam>
    /// <param name="onSuccess">Function to transform the value if successful.</param>
    /// <param name="onFailure">Function to transform the errors if failed.</param>
    /// <returns>A new Result with either the transformed value or transformed errors.</returns>
    /// <example>
    /// <code>
    /// var result = Result{User}.Success(user)
    ///     .BiMap(
    ///         user => new UserDto(user),
    ///         errors => errors.Select(e => new PublicError(e.Message))
    ///     );
    /// </code>
    /// </example>
    public Result<TNew> BiMap<TNew>(
        Func<T, TNew> onSuccess,
        Func<IReadOnlyList<IResultError>, IEnumerable<IResultError>> onFailure)
    {
        if (this.IsSuccess)
        {
            if (onSuccess is null)
            {
                return Result<TNew>.Failure()
                    .WithError(new Error("Success mapper is null"))
                    .WithMessages(this.Messages);
            }

            try
            {
                return Result<TNew>.Success(onSuccess(this.Value))
                    .WithMessages(this.Messages);
            }
            catch (Exception ex)
            {
                return Result<TNew>.Failure()
                    .WithError(Result.Settings.ExceptionErrorFactory(ex))
                    .WithMessages(this.Messages);
            }
        }

        if (onFailure is null)
        {
            return Result<TNew>.Failure()
                .WithErrors(this.Errors)
                .WithMessages(this.Messages);
        }

        try
        {
            return Result<TNew>.Failure()
                .WithErrors(onFailure(this.Errors))
                .WithMessages(this.Messages);
        }
        catch (Exception ex)
        {
            return Result<TNew>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(this.Messages);
        }
    }

    /// <summary>
    ///     Maps a Result's value to a new Result with an optional value, filtering out None cases.
    /// </summary>
    /// <typeparam name="TNew">The type of the resulting value.</typeparam>
    /// <param name="operation">Function that returns Some value for items to keep, None for items to filter out.</param>
    /// <returns>A new Result containing the chosen value if successful, or the original errors if not.</returns>
    /// <example>
    /// <code>
    /// var result = Result{User}.Success(user)
    ///     .Choose(user => user.Age >= 18
    ///         ? Option{UserDto}.Some(new UserDto(user))
    ///         : Option{UserDto}.None());
    /// </code>
    /// </example>
    public Result<TNew> Choose<TNew>(Func<T, ResultChooseOption<TNew>> operation)
    {
        if (!this.IsSuccess || operation is null)
        {
            return Result<TNew>.Failure()
                .WithErrors(this.Errors)
                .WithMessages(this.Messages);
        }

        try
        {
            var option = operation(this.Value);

            return option.TryGetValue(out var value)
                ? Result<TNew>.Success(value).WithMessages(this.Messages)
                : Result<TNew>.Failure()
                    .WithError(new Error("No value was chosen"))
                    .WithMessages(this.Messages);
        }
        catch (Exception ex)
        {
            return Result<TNew>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(this.Messages);
        }
    }

    /// <summary>
    ///     Asynchronously maps a Result's value to a new Result with an optional value, filtering out None cases.
    /// </summary>
    /// <typeparam name="TNew">The type of the resulting value.</typeparam>
    /// <param name="operation">Async function that returns Some value for items to keep, None for items to filter out.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A new Result containing the chosen value if successful, or the original errors if not.</returns>
    /// <example>
    /// <code>
    /// var result = await Result{User}.Success(user)
    ///     .ChooseAsync(async (user, ct) => {
    ///         var permissions = await GetUserPermissionsAsync(user);
    ///         return permissions.HasAccess
    ///             ? Option{UserDto}.Some(new UserDto(user))
    ///             : Option{UserDto}.None();
    ///     }, cancellationToken);
    /// </code>
    /// </example>
    public async Task<Result<TNew>> ChooseAsync<TNew>(
        Func<T, CancellationToken, Task<ResultChooseOption<TNew>>> operation,
        CancellationToken cancellationToken = default)
    {
        if (!this.IsSuccess || operation is null)
        {
            return Result<TNew>.Failure()
                .WithErrors(this.Errors)
                .WithMessages(this.Messages);
        }

        try
        {
            var option = await operation(this.Value, cancellationToken);

            return option.TryGetValue(out var value)
                ? Result<TNew>.Success(value).WithMessages(this.Messages)
                : Result<TNew>.Failure()
                    .WithError(new Error("No value was chosen"))
                    .WithMessages(this.Messages);
        }
        catch (OperationCanceledException)
        {
            return Result<TNew>.Failure()
                .WithError(new OperationCancelledError())
                .WithMessages(this.Messages);
        }
        catch (Exception ex)
        {
            return Result<TNew>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(this.Messages);
        }
    }

    /// <summary>
    ///     For Result{IEnumerable{T}}, applies a transformation to each element while maintaining the Result context.
    /// </summary>
    /// <typeparam name="TOutput">The type of the output elements.</typeparam>
    /// <typeparam name="TInput">The type of the input elements.</typeparam>
    /// <param name="operation">The transformation function to apply to each element.</param>
    /// <returns>A new Result containing the transformed elements or all encountered errors.</returns>
    /// <example>
    /// <code>
    /// var users = Result{IEnumerable{User}}.Success(users)
    ///     .Collect(user => ValidateAndTransformUser(user));
    /// </code>
    /// </example>
    public Result<IEnumerable<TOutput>> Collect<TInput, TOutput>(Func<TInput, Result<TOutput>> operation)
        // where TInput : IEnumerable<TOutput>
    {
        if (!this.IsSuccess || operation is null)
        {
            return Result<IEnumerable<TOutput>>.Failure()
                .WithErrors(this.Errors)
                .WithMessages(this.Messages);
        }

        if (this.Value is null)
        {
            return Result<IEnumerable<TOutput>>.Failure()
                .WithError(new Error("Source sequence is null"))
                .WithMessages(this.Messages);
        }

        var results = new List<TOutput>();
        var errors = new List<IResultError>();
        var messages = new List<string>();

        foreach (var item in (IEnumerable<TInput>)this.Value)
        {
            try
            {
                var result = operation(item);
                if (result.IsSuccess)
                {
                    results.Add(result.Value);
                }
                else
                {
                    errors.AddRange(result.Errors);
                }

                messages.AddRange(result.Messages);
            }
            catch (Exception ex)
            {
                errors.Add(Result.Settings.ExceptionErrorFactory(ex));
            }
        }

        return errors.Any()
            ? Result<IEnumerable<TOutput>>.Failure()
                .WithErrors(errors)
                .WithMessages(messages)
            : Result<IEnumerable<TOutput>>.Success(results)
                .WithMessages(messages);
    }

    /// <summary>
    ///     Asynchronously transforms each element in a collection while maintaining the Result context.
    /// </summary>
    /// <typeparam name="TOutput">The type of the output elements.</typeparam>
    /// <typeparam name="TInput">The type of the input elements.</typeparam>
    /// <param name="operation">The async transformation function to apply to each element.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A new Result containing the transformed elements or all encountered errors.</returns>
    /// <example>
    /// <code>
    /// var users = await Result{IEnumerable{User}}.Success(users)
    ///     .CollectAsync(
    ///         async (user, ct) => await ValidateAndTransformUserAsync(user),
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public async Task<Result<IEnumerable<TOutput>>> CollectAsync<TInput, TOutput>(
            Func<TInput, CancellationToken, Task<Result<TOutput>>> operation,
            CancellationToken cancellationToken = default)
        //where TInput : IEnumerable<TOutput>
    {
        if (!this.IsSuccess || operation is null)
        {
            return Result<IEnumerable<TOutput>>.Failure()
                .WithErrors(this.Errors)
                .WithMessages(this.Messages);
        }

        if (this.Value is null)
        {
            return Result<IEnumerable<TOutput>>.Failure()
                .WithError(new Error("Source sequence is null"))
                .WithMessages(this.Messages);
        }

        var results = new List<TOutput>();
        var errors = new List<IResultError>();
        var messages = new List<string>();

        foreach (var item in (IEnumerable<TInput>)this.Value)
        {
            try
            {
                var result = await operation(item, cancellationToken);
                if (result.IsSuccess)
                {
                    results.Add(result.Value);
                }
                else
                {
                    errors.AddRange(result.Errors);
                }

                messages.AddRange(result.Messages);
            }
            catch (OperationCanceledException)
            {
                errors.Add(new OperationCancelledError());

                break;
            }
            catch (Exception ex)
            {
                errors.Add(Result.Settings.ExceptionErrorFactory(ex));
            }
        }

        return errors.Any()
            ? Result<IEnumerable<TOutput>>.Failure()
                .WithErrors(errors)
                .WithMessages(messages)
            : Result<IEnumerable<TOutput>>.Success(results)
                .WithMessages(messages);
    }

    /// <summary>
    ///     Executes an action only if a condition is met, without changing the Result.
    /// </summary>
    /// <param name="condition">The condition to check.</param>
    /// <param name="action">The action to execute if condition is met.</param>
    /// <returns>The original Result.</returns>
    /// <example>
    /// <code>
    /// var result = Result{User}.Success(user)
    ///     .Switch(
    ///         user => user.IsAdmin,
    ///         user => _logger.LogInfo($"Admin {user.Name} accessed the system")
    ///     );
    /// </code>
    /// </example>
    public Result<T> Switch(Func<T, bool> condition, Action<T> action)
    {
        if (!this.IsSuccess || condition is null || action is null)
        {
            return this;
        }


        try
        {
            if (condition(this.Value))
            {
                action(this.Value);
            }

            return this;
        }
        catch (Exception ex)
        {
            return Failure(this.Value)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
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
    /// var result = await Result{User}.Success(user)
    ///     .SwitchAsync(
    ///         user => user.IsAdmin,
    ///         async (user, ct) => await NotifyAdminLoginAsync(user),
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public async Task<Result<T>> SwitchAsync(
        Func<T, bool> condition,
        Func<T, CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        if (!this.IsSuccess || condition is null || action is null)
        {
            return this;
        }

        try
        {
            if (condition(this.Value))
            {
                await action(this.Value, cancellationToken);
            }

            return this;
        }
        catch (OperationCanceledException)
        {
            return Failure(this.Value)
                .WithError(new OperationCancelledError())
                .WithMessages(this.Messages);
        }
        catch (Exception ex)
        {
            return Failure(this.Value)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(this.Messages);
        }
    }

    /// <summary>
    ///     Validates the successful value of a Result using a specified FluentValidation validator.
    /// </summary>
    /// <param name="validator">The FluentValidation validator to use.</param>
    /// <param name="options">Optional validation strategy options to apply.</param>
    /// <returns>The original Result if validation succeeds; otherwise, a failure Result.</returns>
    /// <example>
    /// <code>
    /// // Basic validation
    /// var result = Result{User}.Success(user)
    ///     .Validate(new UserValidator());
    ///
    /// // Validation with strategy
    /// var result = Result{User}.Success(user)
    ///     .Validate(
    ///         new UserValidator(),
    ///         strategy => strategy.IncludeRuleSets("BasicValidation")
    ///     );
    /// </code>
    /// </example>
    public Result<T> Validate(
        IValidator<T> validator,
        Action<ValidationStrategy<T>> options = null)
    {
        if (!this.IsSuccess)
        {
            return this;
        }

        if (validator is null)
        {
            return Failure(this.Value)
                .WithError(new Error("Validator cannot be null"))
                .WithMessages(this.Messages);
        }

        try
        {
            var validationResult = options is null
                ? validator.Validate(this.Value)
                : validator.Validate(this.Value, options);

            if (validationResult.IsValid)
            {
                return this;
            }

            return Failure(this.Value)
                .WithError(new FluentValidationError(validationResult))
                .WithMessages(this.Messages);
        }
        catch (Exception ex)
        {
            return Failure(this.Value)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessage("Validation failed due to an error")
                .WithMessages(this.Messages);
        }
    }

    /// <summary>
    ///     Asynchronously validates the successful value of a Result using a specified FluentValidation validator.
    /// </summary>
    /// <param name="validator">The FluentValidation validator to use.</param>
    /// <param name="options">Optional validation strategy options to apply.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The original Result if validation succeeds; otherwise, a failure Result.</returns>
    /// <example>
    /// <code>
    /// // Basic async validation
    /// var result = await Result{User}.Success(user)
    ///     .ValidateAsync(new UserValidator(), cancellationToken);
    ///
    /// // Async validation with strategy
    /// var result = await Result{User}.Success(user)
    ///     .ValidateAsync(
    ///         new UserValidator(),
    ///         strategy => strategy
    ///             .IncludeRuleSets("Create")
    ///             .IncludeProperties(x => x.Email),
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public async Task<Result<T>> ValidateAsync(
        IValidator<T> validator,
        Action<ValidationStrategy<T>> options = null,
        CancellationToken cancellationToken = default)
    {
        if (!this.IsSuccess)
        {
            return this;
        }

        if (validator is null)
        {
            return Failure(this.Value)
                .WithError(new Error("Validator cannot be null"))
                .WithMessages(this.Messages);
        }

        try
        {
            var validationResult = options is null
                ? await validator.ValidateAsync(this.Value, cancellationToken)
                : await validator.ValidateAsync(this.Value, options, cancellationToken);

            if (validationResult.IsValid)
            {
                return this;
            }

            return Failure(this.Value)
                .WithError(new FluentValidationError(validationResult))
                .WithMessages(this.Messages);
        }
        catch (OperationCanceledException)
        {
            return Failure(this.Value)
                .WithError(new OperationCancelledError())
                .WithMessages(this.Messages);
        }
        catch (Exception ex)
        {
            return Failure(this.Value)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessage("Validation failed due to an error")
                .WithMessages(this.Messages);
        }
    }
}