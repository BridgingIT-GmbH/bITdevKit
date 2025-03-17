namespace BridgingIT.DevKit.Common;

using FluentValidation;
using FluentValidation.Internal;

public static class ResultExtensions
{
    /// <summary>
    ///     Maps a successful Result{T} to a Result{TNew} using the provided mapping function.
    /// </summary>
    /// <typeparam name="T">The type of the source result value.</typeparam>
    /// <typeparam name="TNew">The type to map to.</typeparam>
    /// <param name="result">The source result to map.</param>
    /// <param name="mapper">The function to map the value.</param>
    /// <returns>A new Result containing the mapped value or the original errors.</returns>
    /// <example>
    /// <code>
    /// var intResult = Result{int}.Success(42);
    /// var stringResult = intResult.Map(x => x.ToString()); // Result{string}.Success("42")
    /// </code>
    /// </example>
    public static Result<TNew> Map<T, TNew>(this Result<T> result, Func<T, TNew> mapper)
    {
        if (!result.IsSuccess || mapper is null)
        {
            return Result<TNew>.Failure()
                .WithErrors(result.Errors)
                .WithMessages(result.Messages);
        }

        try
        {
            var newValue = mapper(result.Value);
            return Result<TNew>.Success(newValue)
                .WithErrors(result.Errors)
                .WithMessages(result.Messages);
        }
        catch (Exception ex)
        {
            return Result<TNew>.Failure()
                .WithErrors(result.Errors)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(result.Messages);
        }
    }

    /// <summary>
    ///     Asynchronously maps a successful Result{T} to a Result{TNew} using the provided mapping function.
    /// </summary>
    /// <typeparam name="T">The type of the source result value.</typeparam>
    /// <typeparam name="TNew">The type to map to.</typeparam>
    /// <param name="result">The source result to map.</param>
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
    public static async Task<Result<TNew>> MapAsync<T, TNew>(
        this Result<T> result,
        Func<T, CancellationToken, Task<TNew>> mapper,
        CancellationToken cancellationToken = default)
    {
        if (!result.IsSuccess || mapper is null)
        {
            return Result<TNew>.Failure()
                .WithErrors(result.Errors)
                .WithMessages(result.Messages);
        }

        try
        {
            var newValue = await mapper(result.Value, cancellationToken);
            return Result<TNew>.Success(newValue)
                .WithErrors(result.Errors)
                .WithMessages(result.Messages);
        }
        catch (OperationCanceledException)
        {
            return Result<TNew>.Failure()
                .WithErrors(result.Errors)
                .WithError(new OperationCancelledError())
                .WithMessages(result.Messages);
        }
        catch (Exception ex)
        {
            return Result<TNew>.Failure()
                .WithErrors(result.Errors)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(result.Messages);
        }
    }

    /// <summary>
    ///     Binds a successful Result{T} to another Result{TNew} using the provided binding function.
    /// </summary>
    /// <typeparam name="T">The type of the source result value.</typeparam>
    /// <typeparam name="TNew">The type of the new Result value.</typeparam>
    /// <param name="result">The source result to bind.</param>
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
    public static Result<TNew> Bind<T, TNew>(this Result<T> result, Func<T, Result<TNew>> binder)
    {
        if (!result.IsSuccess || binder is null)
        {
            return Result<TNew>.Failure()
                .WithErrors(result.Errors)
                .WithMessages(result.Messages);
        }

        try
        {
            var newResult = binder(result.Value);
            return newResult
                .WithErrors(result.Errors)
                .WithMessages(result.Messages);
        }
        catch (Exception ex)
        {
            return Result<TNew>.Failure()
                .WithErrors(result.Errors)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(result.Messages);
        }
    }

    /// <summary>
    ///     Asynchronously binds a successful Result{T} to another Result{TNew}.
    /// </summary>
    /// <typeparam name="T">The type of the source result value.</typeparam>
    /// <typeparam name="TNew">The type of the new Result value.</typeparam>
    /// <param name="result">The source result to bind.</param>
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
    public static async Task<Result<TNew>> BindAsync<T, TNew>(
        this Result<T> result,
        Func<T, CancellationToken, Task<Result<TNew>>> binder,
        CancellationToken cancellationToken = default)
    {
        if (!result.IsSuccess || binder is null)
        {
            return Result<TNew>.Failure()
                .WithErrors(result.Errors)
                .WithMessages(result.Messages);
        }

        try
        {
            var newResult = await binder(result.Value, cancellationToken);
            return newResult
                .WithErrors(result.Errors)
                .WithMessages(result.Messages);
        }
        catch (OperationCanceledException)
        {
            return Result<TNew>.Failure()
                .WithErrors(result.Errors)
                .WithError(new OperationCancelledError())
                .WithMessages(result.Messages);
        }
        catch (Exception ex)
        {
            return Result<TNew>.Failure()
                .WithErrors(result.Errors)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(result.Messages);
        }
    }

    /// <summary>
    /// Throws a <see cref="ResultException"/> if the current result is a failure.
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    /// <param name="result">The result to check.</param>
    /// <returns>The current result if it is successful.</returns>
    /// <exception cref="ResultException">Thrown if the current result is a failure.</exception>
    public static Result<T> ThrowIfFailed<T>(this Result<T> result)
    {
        if (result.IsSuccess)
        {
            return result;
        }

        throw new ResultException(result);
    }

    /// <summary>
    /// Throws an exception of type TException if the result is a failure.
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    /// <typeparam name="TException">The type of exception to throw.</typeparam>
    /// <param name="result">The result to check.</param>
    /// <returns>The current Result if it indicates success.</returns>
    /// <exception>Thrown if the result indicates a failure.
    ///     <cref>TException</cref>
    /// </exception>
    public static Result<T> ThrowIfFailed<T, TException>(this Result<T> result)
        where TException : Exception
    {
        if (result.IsSuccess)
        {
            return result;
        }

        throw ((TException)Activator.CreateInstance(typeof(TException), result.Errors.FirstOrDefault()?.Message, result))!;
    }

    /// <summary>
    ///     Ensures that a condition is met for the contained value, converting to a failure if not.
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    /// <param name="result">The result to check.</param>
    /// <param name="predicate">The condition that must be true for the contained value.</param>
    /// <param name="error">The error to return if the predicate fails.</param>
    /// <returns>The original result if successful and predicate is met; otherwise, a failure with the specified error.</returns>
    /// <example>
    /// <code>
    /// var result = Result{int}.Success(42)
    ///     .Ensure(x => x > 0, new Error("Value must be positive"));
    /// </code>
    /// </example>
    public static Result<T> Ensure<T>(
        this Result<T> result,
        Func<T, bool> predicate,
        IResultError error)
    {
        if (!result.IsSuccess)
        {
            return result;
        }

        if (predicate is null)
        {
            return Result<T>.Failure(result.Value)
                .WithErrors(result.Errors)
                .WithError(new Error("Predicate cannot be null"))
                .WithMessages(result.Messages);
        }

        try
        {
            return predicate(result.Value)
                ? result
                : Result<T>.Failure(result.Value)
                    .WithErrors(result.Errors)
                    .WithError(error ?? new Error("Predicate condition not met"))
                    .WithMessages(result.Messages);
        }
        catch (Exception ex)
        {
            return Result<T>.Failure(result.Value)
                .WithErrors(result.Errors)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(result.Messages);
        }
    }

    /// <summary>
    ///     Asynchronously ensures that a condition is met for the contained value.
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    /// <param name="result">The result to check.</param>
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
    public static async Task<Result<T>> EnsureAsync<T>(
        this Result<T> result,
        Func<T, CancellationToken, Task<bool>> predicate,
        IResultError error,
        CancellationToken cancellationToken = default)
    {
        if (!result.IsSuccess)
        {
            return result;
        }

        if (predicate is null)
        {
            return Result<T>.Failure(result.Value)
                .WithErrors(result.Errors)
                .WithError(new Error("Predicate cannot be null"))
                .WithMessages(result.Messages);
        }

        try
        {
            return await predicate(result.Value, cancellationToken)
                ? result
                : Result<T>.Failure(result.Value)
                    .WithErrors(result.Errors)
                    .WithError(error ?? new Error("Predicate condition not met"))
                    .WithMessages(result.Messages);
        }
        catch (OperationCanceledException)
        {
            return Result<T>.Failure(result.Value)
                .WithErrors(result.Errors)
                .WithError(new OperationCancelledError())
                .WithMessages(result.Messages);
        }
        catch (Exception ex)
        {
            return Result<T>.Failure(result.Value)
                .WithErrors(result.Errors)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(result.Messages);
        }
    }

    /// <summary>
    ///     Executes an action with the current value if the result is successful, without changing the result.
    ///     Useful for performing side effects like logging or monitoring.
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    /// <param name="result">The result to operate on.</param>
    /// <param name="operation">The action to execute with the successful value.</param>
    /// <returns>The original Result instance.</returns>
    /// <example>
    /// <code>
    /// var result = Result{User}.Success(user)
    ///     .Tap(user => _logger.LogInformation($"Retrieved user {user.Id}"))
    ///     .Tap(user => _metrics.IncrementUserRetrievals());
    /// </code>
    /// </example>
    public static Result<T> Tap<T>(this Result<T> result, Action<T> operation)
    {
        if (!result.IsSuccess || operation is null)
        {
            return result;
        }

        try
        {
            operation(result.Value);
            return result;
        }
        catch (Exception ex)
        {
            return Result<T>.Failure()
                .WithErrors(result.Errors)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(result.Messages);
        }
    }

    /// <summary>
    ///     Asynchronously executes an action with the current value if the result is successful.
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    /// <param name="result">The result to operate on.</param>
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
    public static async Task<Result<T>> TapAsync<T>(
        this Result<T> result,
        Func<T, CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        if (!result.IsSuccess || operation is null)
        {
            return result;
        }

        try
        {
            await operation(result.Value, cancellationToken);
            return result;
        }
        catch (OperationCanceledException)
        {
            return Result<T>.Failure()
                .WithErrors(result.Errors)
                .WithError(new OperationCancelledError())
                .WithMessages(result.Messages);
        }
        catch (Exception ex)
        {
            return Result<T>.Failure()
                .WithErrors(result.Errors)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(result.Messages);
        }
    }

    /// <summary>
    ///     Maps a success value while performing a side effect, useful for logging or monitoring during transformations.
    ///     Both the mapping and the side effect only occur if the Result is successful.
    /// </summary>
    /// <typeparam name="T">The type of the source result value.</typeparam>
    /// <typeparam name="TNew">The type of the new result value.</typeparam>
    /// <param name="result">The source result to map.</param>
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
    public static Result<TNew> TeeMap<T, TNew>(
        this Result<T> result,
        Func<T, TNew> mapper,
        Action<TNew> operation)
    {
        if (!result.IsSuccess || mapper is null)
        {
            return Result<TNew>.Failure()
                .WithErrors(result.Errors)
                .WithMessages(result.Messages);
        }

        try
        {
            var newValue = mapper(result.Value);
            operation?.Invoke(newValue);
            return Result<TNew>.Success(newValue)
                .WithErrors(result.Errors)
                .WithMessages(result.Messages);
        }
        catch (Exception ex)
        {
            return Result<TNew>.Failure()
                .WithErrors(result.Errors)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(result.Messages);
        }
    }

    /// <summary>
    ///     Asynchronously maps a success value while performing a side effect.
    ///     Both the mapping and the side effect only occur if the Result is successful.
    /// </summary>
    /// <typeparam name="T">The type of the source result value.</typeparam>
    /// <typeparam name="TNew">The type of the new result value.</typeparam>
    /// <param name="result">The source result to map.</param>
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
    public static async Task<Result<TNew>> TeeMapAsync<T, TNew>(
        this Result<T> result,
        Func<T, TNew> mapper,
        Func<TNew, CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        if (!result.IsSuccess || mapper is null)
        {
            return Result<TNew>.Failure()
                .WithErrors(result.Errors)
                .WithMessages(result.Messages);
        }

        try
        {
            var newValue = mapper(result.Value);
            if (operation is not null)
            {
                await operation(newValue, cancellationToken);
            }
            return Result<TNew>.Success(newValue)
                .WithErrors(result.Errors)
                .WithMessages(result.Messages);
        }
        catch (OperationCanceledException)
        {
            return Result<TNew>.Failure()
                .WithErrors(result.Errors)
                .WithError(new OperationCancelledError())
                .WithMessages(result.Messages);
        }
        catch (Exception ex)
        {
            return Result<TNew>.Failure()
                .WithErrors(result.Errors)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(result.Messages);
        }
    }

    /// <summary>
    ///     Filters a successful result based on a predicate condition.
    ///     Converts to failure if the predicate is not met.
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    /// <param name="result">The result to filter.</param>
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
    public static Result<T> Filter<T>(this Result<T> result, Func<T, bool> operation, IResultError error = null)
    {
        if (!result.IsSuccess || operation is null)
        {
            return result;
        }

        try
        {
            if (!operation(result.Value))
            {
                return Result<T>.Failure(result.Value)
                    .WithErrors(result.Errors)
                    .WithError(error ?? new Error("Predicate condition not met"))
                    .WithMessages(result.Messages);
            }
        }
        catch (Exception ex)
        {
            return Result<T>.Failure(result.Value)
                .WithErrors(result.Errors)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(result.Messages);
        }

        return result;
    }

    /// <summary>
    ///     Asynchronously filters a successful result based on a predicate condition.
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    /// <param name="result">The result to filter.</param>
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
    public static async Task<Result<T>> FilterAsync<T>(
        this Result<T> result,
        Func<T, CancellationToken, Task<bool>> operation,
        IResultError error = null,
        CancellationToken cancellationToken = default)
    {
        if (!result.IsSuccess || operation is null)
        {
            return result;
        }

        try
        {
            if (!await operation(result.Value, cancellationToken))
            {
                return Result<T>.Failure(result.Value)
                    .WithErrors(result.Errors)
                    .WithError(error ?? new Error("Predicate condition not met"))
                    .WithMessages(result.Messages);
            }
        }
        catch (OperationCanceledException)
        {
            return Result<T>.Failure(result.Value)
                .WithErrors(result.Errors)
                .WithError(new OperationCancelledError())
                .WithMessages(result.Messages);
        }
        catch (Exception ex)
        {
            return Result<T>.Failure(result.Value)
                .WithErrors(result.Errors)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(result.Messages);
        }

        return result;
    }

    /// <summary>
    ///     Converts a successful result to a failure if the specified predicate is met (inverse of Filter).
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    /// <param name="result">The result to check.</param>
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
    public static Result<T> Unless<T>(this Result<T> result, Func<T, bool> operation, IResultError error)
    {
        if (!result.IsSuccess || operation is null)
        {
            return result;
        }

        try
        {
            if (operation(result.Value))
            {
                return Result<T>.Failure(result.Value)
                    .WithErrors(result.Errors)
                    .WithError(error ?? new Error("Unless condition met"))
                    .WithMessages(result.Messages);
            }
        }
        catch (Exception ex)
        {
            return Result<T>.Failure(result.Value)
                .WithErrors(result.Errors)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(result.Messages);
        }

        return result;
    }

    /// <summary>
    ///     Asynchronously converts a successful result to a failure if the specified predicate is met.
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    /// <param name="result">The result to check.</param>
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
    public static async Task<Result<T>> UnlessAsync<T>(
        this Result<T> result,
        Func<T, CancellationToken, Task<bool>> operation,
        IResultError error,
        CancellationToken cancellationToken = default)
    {
        if (!result.IsSuccess || operation is null)
        {
            return result;
        }

        try
        {
            if (await operation(result.Value, cancellationToken))
            {
                return Result<T>.Failure(result.Value)
                    .WithErrors(result.Errors)
                    .WithError(error ?? new Error("Unless condition met"))
                    .WithMessages(result.Messages);
            }
        }
        catch (OperationCanceledException)
        {
            return Result<T>.Failure(result.Value)
                .WithErrors(result.Errors)
                .WithError(new OperationCancelledError())
                .WithMessages(result.Messages);
        }
        catch (Exception ex)
        {
            return Result<T>.Failure(result.Value)
                .WithErrors(result.Errors)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(result.Messages);
        }

        return result;
    }

    /// <summary>
    ///     Chains a new operation if the current Result is successful, providing access to the previous Result's value.
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    /// <param name="result">The result to chain.</param>
    /// <param name="operation">The operation to execute if successful, with access to the previous value.</param>
    /// <returns>The original Result if successful.</returns>
    /// <example>
    /// <code>
    /// var result = user.ToResult()
    ///     .AndThen(user => ValidateUser(user))
    ///     .AndThen(user => SaveUser(user));
    /// </code>
    /// </example>
    public static Result<T> AndThen<T>(this Result<T> result, Action<T> operation)
    {
        if (!result.IsSuccess || operation is null)
        {
            return result;
        }

        try
        {
            operation(result.Value);
        }
        catch (Exception ex)
        {
            return Result<T>.Failure()
                .WithErrors(result.Errors)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(result.Messages);
        }

        return result;
    }

    /// <summary>
    ///     Chains a new async operation if the current Result is successful, providing access to the previous Result's value.
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    /// <param name="result">The result to chain.</param>
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
    public static async Task<Result<T>> AndThenAsync<T>(
        this Result<T> result,
        Func<T, CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        if (!result.IsSuccess || operation is null)
        {
            return result;
        }

        try
        {
            await operation(result.Value, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Result<T>.Failure()
                .WithErrors(result.Errors)
                .WithError(new OperationCancelledError("Operation was cancelled"))
                .WithMessages(result.Messages);
        }
        catch (Exception ex)
        {
            return Result<T>.Failure()
                .WithErrors(result.Errors)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(result.Messages);
        }

        return result;
    }

    /// <summary>
    ///     Provides a fallback value in case of failure.
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    /// <param name="result">The result to check.</param>
    /// <param name="operation">Function to provide an alternative value if this result is a failure.</param>
    /// <returns>The original result if successful; otherwise, a success with the fallback value.</returns>
    /// <example>
    /// <code>
    /// var user = Result{User}.Failure("User not found")
    ///     .OrElse(() => User.GetDefaultUser());
    /// </code>
    /// </example>
    public static Result<T> OrElse<T>(this Result<T> result, Func<T> operation)
    {
        if (result.IsSuccess || operation is null)
        {
            return result;
        }

        try
        {
            var fallbackValue = operation();
            return Result<T>.Success(fallbackValue)
                .WithMessages(result.Messages);
        }
        catch (Exception ex)
        {
            return Result<T>.Failure()
                .WithErrors(result.Errors)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(result.Messages);
        }
    }

    /// <summary>
    ///     Provides an async fallback value in case of failure.
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    /// <param name="result">The result to check.</param>
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
    public static async Task<Result<T>> OrElseAsync<T>(
        this Result<T> result,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        if (result.IsSuccess || operation is null)
        {
            return result;
        }

        try
        {
            var fallbackValue = await operation(cancellationToken);
            return Result<T>.Success(fallbackValue)
                .WithMessages(result.Messages);
        }
        catch (OperationCanceledException)
        {
            return Result<T>.Failure()
                .WithErrors(result.Errors)
                .WithError(new OperationCancelledError())
                .WithMessages(result.Messages);
        }
        catch (Exception ex)
        {
            return Result<T>.Failure()
                .WithErrors(result.Errors)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(result.Messages);
        }
    }

    /// <summary>
    ///     Executes an operation in the chain without transforming the value.
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    /// <param name="result">The result to operate on.</param>
    /// <param name="action">The action to execute.</param>
    /// <returns>The original Result.</returns>
    /// <example>
    /// <code>
    /// var result = Result{User}.Success(user)
    ///     .Do(() => InitializeSystem())
    ///     .Then(user => ProcessUser(user));
    /// </code>
    /// </example>
    public static Result<T> Do<T>(this Result<T> result, Action action)
    {
        if (!result.IsSuccess || action is null)
        {
            return result;
        }

        try
        {
            action();
            return result;
        }
        catch (Exception ex)
        {
            return Result<T>.Failure(result.Value)
                .WithErrors(result.Errors)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(result.Messages);
        }
    }

    /// <summary>
    ///     Executes an async operation in the chain without transforming the value.
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    /// <param name="result">The result to operate on.</param>
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
    public static async Task<Result<T>> DoAsync<T>(
        this Result<T> result,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        if (!result.IsSuccess || action is null)
        {
            return result;
        }

        try
        {
            await action(cancellationToken);
            return result;
        }
        catch (OperationCanceledException)
        {
            return Result<T>.Failure(result.Value)
                .WithErrors(result.Errors)
                .WithError(new OperationCancelledError())
                .WithMessages(result.Messages);
        }
        catch (Exception ex)
        {
            return Result<T>.Failure(result.Value)
                .WithErrors(result.Errors)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(result.Messages);
        }
    }

    /// <summary>
    ///     Maps both success and failure cases simultaneously.
    /// </summary>
    /// <typeparam name="T">The type of the source result value.</typeparam>
    /// <typeparam name="TNew">The type of the new result value.</typeparam>
    /// <param name="result">The source result to map.</param>
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
    public static Result<TNew> BiMap<T, TNew>(
        this Result<T> result,
        Func<T, TNew> onSuccess,
        Func<IReadOnlyList<IResultError>, IEnumerable<IResultError>> onFailure)
    {
        if (result.IsSuccess)
        {
            if (onSuccess is null)
            {
                return Result<TNew>.Failure()
                    .WithErrors(result.Errors)
                    .WithError(new Error("Success mapper is null"))
                    .WithMessages(result.Messages);
            }

            try
            {
                return Result<TNew>.Success(onSuccess(result.Value))
                    .WithErrors(result.Errors)
                    .WithMessages(result.Messages);
            }
            catch (Exception ex)
            {
                return Result<TNew>.Failure()
                    .WithErrors(result.Errors)
                    .WithError(Result.Settings.ExceptionErrorFactory(ex))
                    .WithMessages(result.Messages);
            }
        }

        if (onFailure is null)
        {
            return Result<TNew>.Failure()
                .WithErrors(result.Errors)
                .WithMessages(result.Messages);
        }

        try
        {
            return Result<TNew>.Failure()
                .WithErrors(onFailure(result.Errors))
                .WithMessages(result.Messages);
        }
        catch (Exception ex)
        {
            return Result<TNew>.Failure()
                .WithErrors(result.Errors)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(result.Messages);
        }
    }

    /// <summary>
    ///     Maps a Result's value to a new Result with an optional value, filtering out None cases.
    /// </summary>
    /// <typeparam name="T">The type of the source result value.</typeparam>
    /// <typeparam name="TNew">The type of the resulting value.</typeparam>
    /// <param name="result">The source result to choose from.</param>
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
    public static Result<TNew> Choose<T, TNew>(this Result<T> result, Func<T, ResultChooseOption<TNew>> operation)
    {
        if (!result.IsSuccess || operation is null)
        {
            return Result<TNew>.Failure()
                .WithErrors(result.Errors)
                .WithMessages(result.Messages);
        }

        try
        {
            var option = operation(result.Value);
            return option.TryGetValue(out var value)
                ? Result<TNew>.Success(value).WithMessages(result.Messages)
                : Result<TNew>.Failure()
                    .WithErrors(result.Errors)
                    .WithError(new Error("No value was chosen"))
                    .WithMessages(result.Messages);
        }
        catch (Exception ex)
        {
            return Result<TNew>.Failure()
                .WithErrors(result.Errors)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(result.Messages);
        }
    }

    /// <summary>
    ///     Asynchronously maps a Result's value to a new Result with an optional value, filtering out None cases.
    /// </summary>
    /// <typeparam name="T">The type of the source result value.</typeparam>
    /// <typeparam name="TNew">The type of the resulting value.</typeparam>
    /// <param name="result">The source result to choose from.</param>
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
    public static async Task<Result<TNew>> ChooseAsync<T, TNew>(
        this Result<T> result,
        Func<T, CancellationToken, Task<ResultChooseOption<TNew>>> operation,
        CancellationToken cancellationToken = default)
    {
        if (!result.IsSuccess || operation is null)
        {
            return Result<TNew>.Failure()
                .WithErrors(result.Errors)
                .WithMessages(result.Messages);
        }

        try
        {
            var option = await operation(result.Value, cancellationToken);
            return option.TryGetValue(out var value)
                ? Result<TNew>.Success(value).WithMessages(result.Messages)
                : Result<TNew>.Failure()
                    .WithErrors(result.Errors)
                    .WithError(new Error("No value was chosen"))
                    .WithMessages(result.Messages);
        }
        catch (OperationCanceledException)
        {
            return Result<TNew>.Failure()
                .WithErrors(result.Errors)
                .WithError(new OperationCancelledError())
                .WithMessages(result.Messages);
        }
        catch (Exception ex)
        {
            return Result<TNew>.Failure()
                .WithErrors(result.Errors)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(result.Messages);
        }
    }

    /// <summary>
    ///     For Result{IEnumerable{T}}, applies a transformation to each element while maintaining the Result context.
    /// </summary>
    /// <typeparam name="TInput">The type of the input elements.</typeparam>
    /// <typeparam name="TOutput">The type of the output elements.</typeparam>
    /// <param name="result">The source result containing the collection.</param>
    /// <param name="operation">The transformation function to apply to each element.</param>
    /// <returns>A new Result containing the transformed elements or all encountered errors.</returns>
    /// <example>
    /// <code>
    /// var users = Result{IEnumerable{User}}.Success(users)
    ///     .Collect(user => ValidateAndTransformUser(user));
    /// </code>
    /// </example>
    public static Result<IEnumerable<TOutput>> Collect<TInput, TOutput>(
        this Result<IEnumerable<TInput>> result,
        Func<TInput, Result<TOutput>> operation)
    {
        if (!result.IsSuccess || operation is null)
        {
            return Result<IEnumerable<TOutput>>.Failure()
                .WithErrors(result.Errors)
                .WithMessages(result.Messages);
        }

        if (result.Value is null)
        {
            return Result<IEnumerable<TOutput>>.Failure()
                .WithErrors(result.Errors)
                .WithError(new Error("Source sequence is null"))
                .WithMessages(result.Messages);
        }

        var results = new List<TOutput>();
        var errors = new List<IResultError>();
        var messages = new List<string>();

        foreach (var item in result.Value)
        {
            try
            {
                var newResult = operation(item);
                if (newResult.IsSuccess)
                {
                    results.Add(newResult.Value);
                }
                else
                {
                    errors.AddRange(newResult.Errors);
                }
                messages.AddRange(newResult.Messages);
            }
            catch (Exception ex)
            {
                errors.Add(Result.Settings.ExceptionErrorFactory(ex));
            }
        }

        return errors.Any()
            ? Result<IEnumerable<TOutput>>.Failure()
                .WithErrors(result.Errors)
                .WithErrors(errors)
                .WithMessages(messages)
            : Result<IEnumerable<TOutput>>.Success(results)
                .WithErrors(result.Errors)
                .WithMessages(messages);
    }

    /// <summary>
    ///     Asynchronously transforms each element in a collection while maintaining the Result context.
    /// </summary>
    /// <typeparam name="TInput">The type of the input elements.</typeparam>
    /// <typeparam name="TOutput">The type of the output elements.</typeparam>
    /// <param name="result">The source result containing the collection.</param>
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
    public static async Task<Result<IEnumerable<TOutput>>> CollectAsync<TInput, TOutput>(
        this Result<IEnumerable<TInput>> result,
        Func<TInput, CancellationToken, Task<Result<TOutput>>> operation,
        CancellationToken cancellationToken = default)
    {
        if (!result.IsSuccess || operation is null)
        {
            return Result<IEnumerable<TOutput>>.Failure()
                .WithErrors(result.Errors)
                .WithMessages(result.Messages);
        }

        if (result.Value is null)
        {
            return Result<IEnumerable<TOutput>>.Failure()
                .WithErrors(result.Errors)
                .WithError(new Error("Source sequence is null"))
                .WithMessages(result.Messages);
        }

        var results = new List<TOutput>();
        var errors = new List<IResultError>();
        var messages = new List<string>();

        foreach (var item in result.Value)
        {
            try
            {
                var newResult = await operation(item, cancellationToken);
                if (newResult.IsSuccess)
                {
                    results.Add(newResult.Value);
                }
                else
                {
                    errors.AddRange(newResult.Errors);
                }
                messages.AddRange(newResult.Messages);
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
                .WithErrors(result.Errors)
                .WithErrors(errors)
                .WithMessages(messages)
            : Result<IEnumerable<TOutput>>.Success(results)
                .WithErrors(result.Errors)
                .WithMessages(messages);
    }

    /// <summary>
    ///     Executes an action only if a condition is met, without changing the Result.
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    /// <param name="result">The result to operate on.</param>
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
    public static Result<T> Switch<T>(this Result<T> result, Func<T, bool> condition, Action<T> action)
    {
        if (!result.IsSuccess || condition is null || action is null)
        {
            return result;
        }

        try
        {
            if (condition(result.Value))
            {
                action(result.Value);
            }
            return result;
        }
        catch (Exception ex)
        {
            return Result<T>.Failure(result.Value)
                .WithErrors(result.Errors)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(result.Messages);
        }
    }

    /// <summary>
    ///     Executes an async action only if a condition is met, without changing the Result.
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    /// <param name="result">The result to operate on.</param>
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
    public static async Task<Result<T>> SwitchAsync<T>(
        this Result<T> result,
        Func<T, bool> condition,
        Func<T, CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        if (!result.IsSuccess || condition is null || action is null)
        {
            return result;
        }

        try
        {
            if (condition(result.Value))
            {
                await action(result.Value, cancellationToken);
            }
            return result;
        }
        catch (OperationCanceledException)
        {
            return Result<T>.Failure(result.Value)
                .WithErrors(result.Errors)
                .WithError(new OperationCancelledError())
                .WithMessages(result.Messages);
        }
        catch (Exception ex)
        {
            return Result<T>.Failure(result.Value)
                .WithErrors(result.Errors)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(result.Messages);
        }
    }

    /// <summary>
    ///     Validates the successful value of a Result using a specified FluentValidation validator.
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    /// <param name="result">The result to validate.</param>
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
    public static Result<T> Validate<T>(
        this Result<T> result,
        IValidator<T> validator,
        Action<ValidationStrategy<T>> options = null)
    {
        if (!result.IsSuccess)
        {
            return result;
        }

        if (validator is null)
        {
            return Result<T>.Failure(result.Value)
                .WithError(new Error("Validator cannot be null"))
                .WithMessages(result.Messages);
        }

        try
        {
            var validationResult = options is null
                ? validator.Validate(result.Value)
                : validator.Validate(result.Value, options);

            if (validationResult.IsValid)
            {
                return result;
            }

            return Result<T>.Failure(result.Value)
                .WithErrors(result.Errors)
                .WithError(new FluentValidationError(validationResult))
                .WithMessages(result.Messages);
        }
        catch (Exception ex)
        {
            return Result<T>.Failure(result.Value)
                .WithErrors(result.Errors)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessage("Validation failed due to an error")
                .WithMessages(result.Messages);
        }
    }

    /// <summary>
    ///     Asynchronously validates the successful value of a Result using a specified FluentValidation validator.
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    /// <param name="result">The result to validate.</param>
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
    public static async Task<Result<T>> ValidateAsync<T>(
        this Result<T> result,
        IValidator<T> validator,
        Action<ValidationStrategy<T>> options = null,
        CancellationToken cancellationToken = default)
    {
        if (!result.IsSuccess)
        {
            return result;
        }

        if (validator is null)
        {
            return Result<T>.Failure(result.Value)
                .WithErrors(result.Errors)
                .WithError(new Error("Validator cannot be null"))
                .WithMessages(result.Messages);
        }

        try
        {
            var validationResult = options is null
                ? await validator.ValidateAsync(result.Value, cancellationToken)
                : await validator.ValidateAsync(result.Value, options, cancellationToken);

            if (validationResult.IsValid)
            {
                return result;
            }

            return Result<T>.Failure(result.Value)
                .WithErrors(result.Errors)
                .WithError(new FluentValidationError(validationResult))
                .WithMessages(result.Messages);
        }
        catch (OperationCanceledException)
        {
            return Result<T>.Failure(result.Value)
                .WithErrors(result.Errors)
                .WithError(new OperationCancelledError())
                .WithMessages(result.Messages);
        }
        catch (Exception ex)
        {
            return Result<T>.Failure(result.Value)
                .WithErrors(result.Errors)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessage("Validation failed due to an error")
                .WithMessages(result.Messages);
        }
    }

    /// <summary>
    /// Executes different functions based on the Result's success state.
    /// </summary>
    /// <typeparam name="TResult">The type of the return value.</typeparam>
    /// <param name="result">The Result to match.</param>
    /// <param name="onSuccess">Function to execute if the Result is successful, receiving the value.</param>
    /// <param name="onFailure">Function to execute if the Result failed, receiving the errors.</param>
    /// <returns>The result of either the success or failure function.</returns>
    /// <exception cref="ArgumentNullException">Thrown when onSuccess or onFailure is null.</exception>
    /// <example>
    /// <code>
    /// var result = Result{User}.Success(user);
    ///
    /// // Pattern matching with different return types
    /// string message = result.Match(
    ///     onSuccess: user => $"Found user: {user.Name}",
    ///     onFailure: errors => $"Failed with {errors.Count} errors"
    /// );
    ///
    /// // Pattern matching with complex logic
    /// var apiResponse = result.Match(
    ///     onSuccess: user => new ApiResponse
    ///     {
    ///         Data = user,
    ///         Status = 200
    ///     },
    ///     onFailure: errors => new ApiResponse
    ///     {
    ///         Errors = errors.Select(e => e.Message).ToList(),
    ///         Status = 400
    ///     }
    /// );
    /// </code>
    /// </example>
    public static TResult Match<TResult, T>(
        this Result<T> result,
        Func<T, TResult> onSuccess,
        Func<IReadOnlyList<IResultError>, TResult> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        return result.IsSuccess
            ? onSuccess(result.Value)
            : onFailure(result.Errors);
    }

    /// <summary>
    /// Returns different values based on the Result's success state.
    /// </summary>
    /// <typeparam name="TResult">The type of the return value.</typeparam>
    /// <param name="result">The Result to match.</param>
    /// <param name="success">Value to return if successful.</param>
    /// <param name="failure">Value to return if failed.</param>
    /// <returns>Either the success or failure value.</returns>
    /// <example>
    /// <code>
    /// var result = Result{User}.Success(user);
    ///
    /// // Simple value matching
    /// string status = result.Match(
    ///     success: "User is valid",
    ///     failure: "User is invalid"
    /// );
    ///
    /// // Status code matching
    /// int statusCode = result.Match(
    ///     success: 200,
    ///     failure: 400
    /// );
    /// </code>
    /// </example>
    public static TResult Match<TResult, T>(
        this Result<T> result,
        TResult success,
        TResult failure)
    {
        return result.IsSuccess ? success : failure;
    }

    /// <summary>
    /// Asynchronously executes different functions based on the Result's success state.
    /// </summary>
    /// <typeparam name="TResult">The type of the return value.</typeparam>
    /// <param name="result">The Result to match.</param>
    /// <param name="onSuccess">Async function to execute if successful, receiving the value.</param>
    /// <param name="onFailure">Async function to execute if failed, receiving the errors.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A Task containing the result of either the success or failure function.</returns>
    /// <exception cref="ArgumentNullException">Thrown when onSuccess or onFailure is null.</exception>
    /// <example>
    /// <code>
    /// var result = Result{User}.Success(user);
    ///
    /// var response = await result.MatchAsync(
    ///     async (user, ct) =>
    ///     {
    ///         await _userService.LogAccessAsync(user, ct);
    ///         return new SuccessResponse(user);
    ///     },
    ///     async (errors, ct) =>
    ///     {
    ///         await _logger.LogErrorsAsync(errors, ct);
    ///         return new ErrorResponse(errors);
    ///     },
    ///     cancellationToken
    /// );
    /// </code>
    /// </example>
    public static async Task<TResult> MatchAsync<TResult, T>(
        this Result<T> result,
        Func<T, CancellationToken, Task<TResult>> onSuccess,
        Func<IReadOnlyList<IResultError>, CancellationToken, Task<TResult>> onFailure,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        return result.IsSuccess
            ? await onSuccess(result.Value, cancellationToken)
            : await onFailure(result.Errors, cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a success function with a synchronous failure handler.
    /// </summary>
    /// <typeparam name="TResult">The type of the return value.</typeparam>
    /// <param name="result">The Result to match.</param>
    /// <param name="onSuccess">Async function to execute if successful, receiving the value.</param>
    /// <param name="onFailure">Synchronous function to execute if failed, receiving the errors.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A Task containing the result of either the success or failure function.</returns>
    /// <exception cref="ArgumentNullException">Thrown when onSuccess or onFailure is null.</exception>
    /// <example>
    /// <code>
    /// var result = Result{User}.Success(user);
    ///
    /// var message = await result.MatchAsync(
    ///     async (user, ct) =>
    ///     {
    ///         await _userService.UpdateLastLoginAsync(user, ct);
    ///         return $"Updated login time for {user.Name}";
    ///     },
    ///     errors => $"Login failed: {errors.First().Message}",
    ///     cancellationToken
    /// );
    /// </code>
    /// </example>
    public static async Task<TResult> MatchAsync<TResult, T>(
        this Result<T> result,
        Func<T, CancellationToken, Task<TResult>> onSuccess,
        Func<IReadOnlyList<IResultError>, TResult> onFailure,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        return result.IsSuccess
            ? await onSuccess(result.Value, cancellationToken)
            : onFailure(result.Errors);
    }

    /// <summary>
    /// Executes a synchronous success function with an async failure handler.
    /// </summary>
    /// <typeparam name="TResult">The type of the return value.</typeparam>
    /// <param name="result">The Result to match.</param>
    /// <param name="onSuccess">Synchronous function to execute if successful, receiving the value.</param>
    /// <param name="onFailure">Async function to execute if failed, receiving the errors.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A Task containing the result of either the success or failure function.</returns>
    /// <exception cref="ArgumentNullException">Thrown when onSuccess or onFailure is null.</exception>
    /// <example>
    /// <code>
    /// var result = Result{User}.Failure()
    ///     .WithError(new ValidationError("Invalid email"));
    ///
    /// var response = await result.MatchAsync(
    ///     user => new SuccessResponse(user),
    ///     async (errors, ct) =>
    ///     {
    ///         await _errorService.LogValidationErrorsAsync(errors, ct);
    ///         return new ErrorResponse(await FormatErrorsAsync(errors, ct));
    ///     },
    ///     cancellationToken
    /// );
    /// </code>
    /// </example>
    public static async Task<TResult> MatchAsync<TResult, T>(
        this Result<T> result,
        Func<T, TResult> onSuccess,
        Func<IReadOnlyList<IResultError>, CancellationToken, Task<TResult>> onFailure,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        return result.IsSuccess
            ? onSuccess(result.Value)
            : await onFailure(result.Errors, cancellationToken);
    }

    /// <summary>
    /// Executes different actions based on the Result's success state.
    /// </summary>
    /// <param name="result">The Result to handle.</param>
    /// <param name="onSuccess">Action to execute if the Result is successful, receiving the value.</param>
    /// <param name="onFailure">Action to execute if the Result failed, receiving the errors.</param>
    /// <returns>The original Result instance.</returns>
    /// <example>
    /// <code>
    /// var result = Result{User}.Success(user);
    ///
    /// result.Handle(
    ///     onSuccess: user => Console.WriteLine($"User: {user.Name}"),
    ///     onFailure: errors => Console.WriteLine($"Failed with {errors.Count} errors")
    /// );
    /// </code>
    /// </example>
    public static Result<T> Handle<T>(
        this Result<T> result,
        Action<T> onSuccess,
        Action<IReadOnlyList<IResultError>> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        if (result.IsSuccess)
        {
            onSuccess(result.Value);
            return result;
        }
        else
        {
            onFailure(result.Errors);
            return result;
        }
    }

    /// <summary>
    /// Asynchronously executes different actions based on the Result's success state.
    /// </summary>
    /// <param name="result">The Result to handle.</param>
    /// <param name="onSuccess">Async function to execute if the Result is successful, receiving the value.</param>
    /// <param name="onFailure">Async function to execute if the Result failed, receiving the errors.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A Task containing the original Result instance.</returns>
    /// <example>
    /// <code>
    /// var result = Result{User}.Success(user);
    ///
    /// await result.HandleAsync(
    ///     async (user, ct) => await LogUserAccessAsync(user, ct),
    ///     async (errors, ct) => await LogErrorsAsync(errors, ct),
    ///     cancellationToken
    /// );
    /// </code>
    /// </example>
    public static async Task<Result<T>> HandleAsync<T>(
        this Result<T> result,
        Func<T, CancellationToken, Task> onSuccess,
        Func<IReadOnlyList<IResultError>, CancellationToken, Task> onFailure,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        if (result.IsSuccess)
        {
            await onSuccess(result.Value, cancellationToken);
            return result;
        }
        else
        {
            await onFailure(result.Errors, cancellationToken);
            return result;
        }
    }

    /// <summary>
    /// Asynchronously executes a success function with a synchronous failure handler.
    /// </summary>
    /// <param name="result">The Result to handle.</param>
    /// <param name="onSuccess">Async function to execute if the Result is successful, receiving the value.</param>
    /// <param name="onFailure">Synchronous function to execute if the Result failed, receiving the errors.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A Task containing the original Result instance.</returns>
    /// <example>
    /// <code>
    /// var result = Result{User}.Success(user);
    ///
    /// await result.HandleAsync(
    ///     async (user, ct) => await LogUserAccessAsync(user, ct),
    ///     errors => Console.WriteLine($"Failed with {errors.Count} errors"),
    ///     cancellationToken
    /// );
    /// </code>
    /// </example>
    public static async Task<Result<T>> HandleAsync<T>(
        this Result<T> result,
        Func<T, CancellationToken, Task> onSuccess,
        Action<IReadOnlyList<IResultError>> onFailure,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        if (result.IsSuccess)
        {
            await onSuccess(result.Value, cancellationToken);
            return result;
        }
        else
        {
            onFailure(result.Errors);
            return result;
        }
    }

    /// <summary>
    /// Executes a synchronous success function with an async failure handler.
    /// </summary>
    /// <param name="result">The Result to handle.</param>
    /// <param name="onSuccess">Synchronous function to execute if the Result is successful, receiving the value.</param>
    /// <param name="onFailure">Async function to execute if the Result failed, receiving the errors.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A Task containing the original Result instance.</returns>
    /// <example>
    /// <code>
    /// var result = Result{User}.Success(user);
    ///
    /// await result.HandleAsync(
    ///     user => Console.WriteLine($"User: {user.Name}"),
    ///     async (errors, ct) => await LogErrorsAsync(errors, ct),
    ///     cancellationToken
    /// );
    /// </code>
    /// </example>
    public static async Task<Result<T>> HandleAsync<T>(
        this Result<T> result,
        Action<T> onSuccess,
        Func<IReadOnlyList<IResultError>, CancellationToken, Task> onFailure,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        if (result.IsSuccess)
        {
            onSuccess(result.Value);
            return result;
        }
        else
        {
            await onFailure(result.Errors, cancellationToken);
            return result;
        }
    }
}