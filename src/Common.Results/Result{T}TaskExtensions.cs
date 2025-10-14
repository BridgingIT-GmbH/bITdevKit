// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using FluentValidation;
using FluentValidation.Internal;
using Microsoft.Extensions.Logging;

/// <summary>
///     Extension methods for Task<Result<T>> to enable proper chaining.
/// </summary>
public static partial class ResultTTaskExtensions
{
    /// <summary>
    /// Throws a <see cref="ResultException"/> if the Result task indicates failure.
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    /// <param name="resultTask">The Result task to check.</param>
    /// <returns>The current Result if it is successful.</returns>
    /// <exception cref="ResultException">Thrown if the current Result is a failure.</exception>
    /// <example>
    /// <code>
    /// await GetUserAsync(userId)
    ///     .ThrowIfFailed();
    /// </code>
    /// </example>
    public static async Task<Result<T>> ThrowIfFailed<T>(this Task<Result<T>> resultTask)
    {
        try
        {
            var result = await resultTask;
            return result.ThrowIfFailed();
        }
        catch (Exception ex)
        {
            throw new ResultException("Error while checking result status", ex);
        }
    }

    /// <summary>
    /// Throws an exception of type TException if the Result task is a failure.
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    /// <typeparam name="TException">The type of exception to throw.</typeparam>
    /// <param name="resultTask">The Result task to check.</param>
    /// <returns>The current Result if it indicates success.</returns>
    /// <exception cref="TException">Thrown if the Result indicates a failure.</exception>
    /// <example>
    /// <code>
    /// await GetUserAsync(userId)
    ///     .ThrowIfFailed{InvalidOperationException}();
    /// </code>
    /// </example>
    public static async Task<Result<T>> ThrowIfFailed<T, TException>(this Task<Result<T>> resultTask)
        where TException : Exception
    {
        try
        {
            var result = await resultTask;
            return result.ThrowIfFailed<T>();
        }
        catch (TException)
        {
            throw; // Propagate the specified exception
        }
        catch (Exception ex)
        {
            throw new ResultException("Error while checking result status", ex);
        }
    }

    /// <summary>
    /// Executes an action only if a condition is met, without changing the Result task.
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    /// <param name="resultTask">The Result task to switch on.</param>
    /// <param name="condition">The condition to check.</param>
    /// <param name="action">The action to execute if condition is met.</param>
    /// <returns>The original Result wrapped in a Task.</returns>
    /// <example>
    /// <code>
    /// await GetUserAsync(userId)
    ///     .Switch(
    ///         user => user.IsAdmin,
    ///         user => _logger.LogInfo($"Admin {user.Name} accessed the system")
    ///     );
    /// </code>
    /// </example>
    public static async Task<Result<T>> Switch<T>(
        this Task<Result<T>> resultTask,
        Func<T, bool> condition,
        Action<T> action)
    {
        if (condition is null || action is null)
        {
            return Result<T>.Failure()
                .WithError(new Error("Condition or action cannot be null"));
        }

        try
        {
            var result = await resultTask;
            return result.Switch(condition, action);
        }
        catch (Exception ex)
        {
            return Result<T>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    /// Executes an async action only if a condition is met, without changing the Result task.
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    /// <param name="resultTask">The Result task to switch on.</param>
    /// <param name="condition">The condition to check.</param>
    /// <param name="action">The async action to execute if condition is met.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The original Result wrapped in a Task.</returns>
    /// <example>
    /// <code>
    /// await GetUserAsync(userId)
    ///     .SwitchAsync(
    ///         user => user.IsAdmin,
    ///         async (user, ct) => await NotifyAdminLoginAsync(user),
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public static async Task<Result<T>> SwitchAsync<T>(
        this Task<Result<T>> resultTask,
        Func<T, bool> condition,
        Func<T, CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        if (condition is null || action is null)
        {
            return Result<T>.Failure()
                .WithError(new Error("Condition or action cannot be null"));
        }

        try
        {
            var result = await resultTask;
            return await result.SwitchAsync(condition, action, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Result<T>.Failure()
                .WithError(new OperationCancelledError());
        }
        catch (Exception ex)
        {
            return Result<T>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Maps the successful value of a Result task using the provided mapping function.
    /// </summary>
    /// <typeparam name="T">The type of the source value.</typeparam>
    /// <typeparam name="TNew">The type to map to.</typeparam>
    /// <param name="resultTask">The Result task to map.</param>
    /// <param name="mapper">The function to map the value.</param>
    /// <returns>A new Result containing the mapped value or the original errors.</returns>
    /// <example>
    /// <code>
    /// var result = await GetUserAsync(userId)
    ///     .Map(user => new UserDto(user));
    /// </code>
    /// </example>
    public static async Task<Result<TNew>> Map<T, TNew>(
        this Task<Result<T>> resultTask,
        Func<T, TNew> mapper)
    {
        if (mapper is null)
        {
            return Result<TNew>.Failure()
                .WithError(new Error("Mapper cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return result.Map(mapper);
        }
        catch (Exception ex)
        {
            return Result<TNew>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Maps the successful value of a Result task using the provided asynchronous mapping function.
    /// </summary>
    /// <typeparam name="T">The type of the source value.</typeparam>
    /// <typeparam name="TNew">The type to map to.</typeparam>
    /// <param name="resultTask">The Result task to map.</param>
    /// <param name="mapper">The async function to map the value.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A new Result containing the mapped value or the original errors.</returns>
    /// <example>
    /// <code>
    /// var result = await GetUserAsync(userId)
    ///     .MapAsync(
    ///         async (user, ct) => await LoadUserPreferencesAsync(user),
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public static async Task<Result<TNew>> MapAsync<T, TNew>(
        this Task<Result<T>> resultTask,
        Func<T, CancellationToken, Task<TNew>> mapper,
        CancellationToken cancellationToken = default)
    {
        if (mapper is null)
        {
            return Result<TNew>.Failure()
                .WithError(new Error("Mapper cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return await result.MapAsync(mapper, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Result<TNew>.Failure()
                .WithError(new OperationCancelledError());
        }
        catch (Exception ex)
        {
            return Result<TNew>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Binds the successful value of a Result task to another Result using the provided binding function.
    /// </summary>
    /// <typeparam name="T">The type of the source value.</typeparam>
    /// <typeparam name="TNew">The type of the new Result value.</typeparam>
    /// <param name="resultTask">The Result task to bind.</param>
    /// <param name="binder">The function to bind the value to a new Result.</param>
    /// <returns>The bound Result or a failure containing the original errors.</returns>
    /// <example>
    /// <code>
    /// var result = await GetUserAsync(userId)
    ///     .Bind(user => ValidateAndTransformUser(user));
    /// </code>
    /// </example>
    public static async Task<Result<TNew>> Bind<T, TNew>(
        this Task<Result<T>> resultTask,
        Func<T, Result<TNew>> binder)
    {
        if (binder is null)
        {
            return Result<TNew>.Failure()
                .WithError(new Error("Binder cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return result.Bind(binder);
        }
        catch (Exception ex)
        {
            return Result<TNew>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Binds the successful value of a Result task to another Result using the provided async binding function.
    /// </summary>
    /// <typeparam name="T">The type of the source value.</typeparam>
    /// <typeparam name="TNew">The type of the new Result value.</typeparam>
    /// <param name="resultTask">The Result task to bind.</param>
    /// <param name="binder">The async function to bind the value to a new Result.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The bound Result or a failure containing the original errors.</returns>
    /// <example>
    /// <code>
    /// var result = await GetUserAsync(userId)
    ///     .BindAsync(
    ///         async (user, ct) => await ValidateAndTransformUserAsync(user),
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public static async Task<Result<TNew>> BindAsync<T, TNew>(
        this Task<Result<T>> resultTask,
        Func<T, CancellationToken, Task<Result<TNew>>> binder,
        CancellationToken cancellationToken = default)
    {
        if (binder is null)
        {
            return Result<TNew>.Failure()
                .WithError(new Error("Binder cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return await result.BindAsync(binder, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Result<TNew>.Failure()
                .WithError(new OperationCancelledError());
        }
        catch (Exception ex)
        {
            return Result<TNew>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Ensures that a condition is met for the contained value of a Result task.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="resultTask">The Result task to check.</param>
    /// <param name="predicate">The condition that must be true.</param>
    /// <param name="error">The error to return if the predicate fails.</param>
    /// <returns>The original Result if successful and predicate is met; otherwise, a failure.</returns>
    /// <example>
    /// <code>
    /// var result = await GetUserAsync(userId)
    ///     .Ensure(
    ///         user => user.IsActive,
    ///         new Error("User is not active")
    ///     );
    /// </code>
    /// </example>
    public static async Task<Result<T>> Ensure<T>(
        this Task<Result<T>> resultTask,
        Func<T, bool> predicate,
        IResultError error)
    {
        if (predicate is null)
        {
            return Result<T>.Failure()
                .WithError(new Error("Predicate cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return result.Ensure(predicate, error);
        }
        catch (Exception ex)
        {
            return Result<T>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Ensures that an async condition is met for the contained value of a Result task.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="resultTask">The Result task to check.</param>
    /// <param name="predicate">The async condition that must be true.</param>
    /// <param name="error">The error to return if the predicate fails.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The original Result if successful and predicate is met; otherwise, a failure.</returns>
    /// <example>
    /// <code>
    /// var result = await GetUserAsync(userId)
    ///     .EnsureAsync(
    ///         async (user, ct) => await CheckUserStatusAsync(user),
    ///         new Error("Invalid user status"),
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public static async Task<Result<T>> EnsureAsync<T>(
        this Task<Result<T>> resultTask,
        Func<T, CancellationToken, Task<bool>> predicate,
        IResultError error,
        CancellationToken cancellationToken = default)
    {
        if (predicate is null)
        {
            return Result<T>.Failure()
                .WithError(new Error("Predicate cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return await result.EnsureAsync(predicate, error, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Result<T>.Failure()
                .WithError(new OperationCancelledError());
        }
        catch (Exception ex)
        {
            return Result<T>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Wraps a Result task in a Try operation, catching any exceptions.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="resultTask">The Result task to wrap.</param>
    /// <returns>A Result containing the value or any caught exceptions.</returns>
    /// <example>
    /// <code>
    /// var result = await GetUserAsync(userId)
    ///     .Try();
    /// </code>
    /// </example>
    public static async Task<Result<T>> Try<T>(
        this Task<Result<T>> resultTask)
    {
        try
        {
            return await resultTask;
        }
        catch (Exception ex)
        {
            return Result<T>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Wraps a Result task in a Try operation with cancellation support.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="resultTask">The Result task to wrap.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A Result containing the value or any caught exceptions.</returns>
    /// <example>
    /// <code>
    /// var result = await GetUserAsync(userId)
    ///     .TryAsync(cancellationToken);
    /// </code>
    /// </example>
    public static async Task<Result<T>> TryAsync<T>(
        this Task<Result<T>> resultTask,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            return await resultTask;
        }
        catch (OperationCanceledException)
        {
            return Result<T>.Failure()
                .WithError(new OperationCancelledError());
        }
        catch (Exception ex)
        {
            return Result<T>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Executes an action with the successful value without changing the Result task.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="resultTask">The Result task to tap into.</param>
    /// <param name="action">The action to execute with the value.</param>
    /// <returns>The original Result task unchanged.</returns>
    /// <example>
    /// <code>
    /// var result = await GetUserAsync(userId)
    ///     .Tap(user => _logger.LogInformation($"Retrieved user {user.Id}"));
    /// </code>
    /// </example>
    public static async Task<Result<T>> Tap<T>(
        this Task<Result<T>> resultTask,
        Action<T> action)
    {
        if (action is null)
        {
            return Result<T>.Failure()
                .WithError(new Error("Action cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return result.Tap(action);
        }
        catch (Exception ex)
        {
            return Result<T>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    /// Logs an awaited Task&lt;Result&lt;T&gt;&gt; using structured logging with default levels
    /// (Debug on success, Warning on failure).
    /// </summary>
    /// <typeparam name="T">The type of the success value contained in the result.</typeparam>
    /// <param name="resultTask">The task returning a result to log.</param>
    /// <param name="logger">
    /// The logger to write to. If null, the method awaits and returns the original result.
    /// </param>
    /// <param name="messageTemplate">
    /// Optional message template for structured logging (e.g., "Pipeline completed for {Id}").
    /// </param>
    /// <param name="args">
    /// Optional structured logging arguments corresponding to <paramref name="messageTemplate"/>.
    /// </param>
    /// <remarks>
    /// This method awaits <paramref name="resultTask"/> and delegates to the synchronous overload.
    /// </remarks>
    /// <returns>
    /// The awaited <see cref="Result{T}"/> from <paramref name="resultTask"/>, unchanged.
    /// </returns>
    /// <example>
    /// <code>
    /// var final = await pipeline.Log(logger, "Handled {Command} for {Email}", "CreateCustomer", email);
    /// </code>
    /// </example>
    public static async Task<Result<T>> Log<T>(
        this Task<Result<T>> resultTask,
        ILogger logger,
        string messageTemplate = null,
        params object[] args)
    {
        var result = await resultTask.ConfigureAwait(false);
        return result.Log(logger, messageTemplate, args);
    }

    /// <summary>
    /// Logs an awaited Task&lt;Result&lt;T&gt;&gt; using structured logging with custom levels.
    /// </summary>
    /// <typeparam name="T">The type of the success value contained in the result.</typeparam>
    /// <param name="resultTask">The task returning a result to log.</param>
    /// <param name="logger">
    /// The logger to write to. If null, the method awaits and returns the original result.
    /// </param>
    /// <param name="messageTemplate">
    /// Optional message template for structured logging (e.g., "Persisted {Entity} with Id {Id}").
    /// </param>
    /// <param name="successLevel">The log level used when the result indicates success.</param>
    /// <param name="failureLevel">The log level used when the result indicates failure.</param>
    /// <param name="args">
    /// Optional structured logging arguments corresponding to <paramref name="messageTemplate"/>.
    /// </param>
    /// <remarks>
    /// This method awaits <paramref name="resultTask"/> and delegates to the synchronous overload.
    /// </remarks>
    /// <returns>
    /// The awaited <see cref="Result{T}"/> from <paramref name="resultTask"/>, unchanged.
    /// </returns>
    /// <example>
    /// <code>
    /// var final = await pipeline.Log(
    ///     logger,
    ///     "Created {Entity} with Id {Id}",
    ///     LogLevel.Information,
    ///     LogLevel.Error,
    ///     "Customer",
    ///     customerId);
    /// </code>
    /// </example>
    public static async Task<Result<T>> Log<T>(
        this Task<Result<T>> resultTask,
        ILogger logger,
        string messageTemplate,
        LogLevel successLevel,
        LogLevel failureLevel,
        params object[] args)
    {
        var result = await resultTask.ConfigureAwait(false);
        return result.Log(logger, messageTemplate, successLevel, failureLevel, args);
    }

    /// <summary>
    ///     Executes an async action with the successful value without changing the Result task.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="resultTask">The Result task to tap into.</param>
    /// <param name="action">The async action to execute with the value.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The original Result task unchanged.</returns>
    /// <example>
    /// <code>
    /// var result = await GetUserAsync(userId)
    ///     .TapAsync(
    ///         async (user, ct) => await _cache.StoreUserAsync(user),
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public static async Task<Result<T>> TapAsync<T>(
        this Task<Result<T>> resultTask,
        Func<T, CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        if (action is null)
        {
            return Result<T>.Failure()
                .WithError(new Error("Action cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return await result.TapAsync(action, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Result<T>.Failure()
                .WithError(new OperationCancelledError());
        }
        catch (Exception ex)
        {
            return Result<T>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Maps a success value while performing a side effect on the transformed value.
    /// </summary>
    /// <typeparam name="T">The type of the source value.</typeparam>
    /// <typeparam name="TNew">The type of the new value.</typeparam>
    /// <param name="resultTask">The Result task to transform.</param>
    /// <param name="mapper">The function to transform the value.</param>
    /// <param name="action">The action to perform on the transformed value.</param>
    /// <returns>A new Result containing the transformed value.</returns>
    /// <example>
    /// <code>
    /// var result = await GetUserAsync(userId)
    ///     .TeeMap(
    ///         user => new UserDto(user),
    ///         dto => _logger.LogInformation($"Created DTO for {dto.Id}")
    ///     );
    /// </code>
    /// </example>
    public static async Task<Result<TNew>> TeeMap<T, TNew>(
        this Task<Result<T>> resultTask,
        Func<T, TNew> mapper,
        Action<TNew> action)
    {
        if (mapper is null)
        {
            return Result<TNew>.Failure()
                .WithError(new Error("Mapper cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return result.TeeMap(mapper, action);
        }
        catch (Exception ex)
        {
            return Result<TNew>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Maps a success value while performing an async side effect on the transformed value.
    /// </summary>
    /// <typeparam name="T">The type of the source value.</typeparam>
    /// <typeparam name="TNew">The type of the new value.</typeparam>
    /// <param name="resultTask">The Result task to transform.</param>
    /// <param name="mapper">The function to transform the value.</param>
    /// <param name="action">The async action to perform on the transformed value.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A new Result containing the transformed value.</returns>
    /// <example>
    /// <code>
    /// var result = await GetUserAsync(userId)
    ///     .TeeMapAsync(
    ///         user => new UserDto(user),
    ///         async (dto, ct) => await _cache.StoreDtoAsync(dto),
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public static async Task<Result<TNew>> TeeMapAsync<T, TNew>(
        this Task<Result<T>> resultTask,
        Func<T, TNew> mapper,
        Func<TNew, CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        if (mapper is null)
        {
            return Result<TNew>.Failure()
                .WithError(new Error("Mapper cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return await result.TeeMapAsync(mapper, action, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Result<TNew>.Failure()
                .WithError(new OperationCancelledError());
        }
        catch (Exception ex)
        {
            return Result<TNew>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Filters a Result task based on a predicate.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="resultTask">The Result task to filter.</param>
    /// <param name="predicate">The condition that must be true.</param>
    /// <param name="error">The error to return if the predicate fails.</param>
    /// <returns>The original Result if successful and predicate is met; otherwise, a failure.</returns>
    /// <example>
    /// <code>
    /// var result = await GetUserAsync(userId)
    ///     .Filter(
    ///         user => user.IsActive,
    ///         new ValidationError("User is not active")
    ///     );
    /// </code>
    /// </example>
    public static async Task<Result<T>> Filter<T>(
        this Task<Result<T>> resultTask,
        Func<T, bool> predicate,
        IResultError error = null)
    {
        if (predicate is null)
        {
            return Result<T>.Failure()
                .WithError(new Error("Predicate cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return result.Filter(predicate, error);
        }
        catch (Exception ex)
        {
            return Result<T>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Filters a Result task based on an async predicate.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="resultTask">The Result task to filter.</param>
    /// <param name="predicate">The async condition that must be true.</param>
    /// <param name="error">The error to return if the predicate fails.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The original Result if successful and predicate is met; otherwise, a failure.</returns>
    /// <example>
    /// <code>
    /// var result = await GetUserAsync(userId)
    ///     .FilterAsync(
    ///         async (user, ct) => await HasValidSubscriptionAsync(user),
    ///         new SubscriptionError("No valid subscription"),
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public static async Task<Result<T>> FilterAsync<T>(
        this Task<Result<T>> resultTask,
        Func<T, CancellationToken, Task<bool>> predicate,
        IResultError error = null,
        CancellationToken cancellationToken = default)
    {
        if (predicate is null)
        {
            return Result<T>.Failure()
                .WithError(new Error("Predicate cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return await result.FilterAsync(predicate, error, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Result<T>.Failure()
                .WithError(new OperationCancelledError());
        }
        catch (Exception ex)
        {
            return Result<T>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Converts a successful Result task to a failure if a condition is met.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="resultTask">The Result task to check.</param>
    /// <param name="predicate">The condition that must be false.</param>
    /// <param name="error">The error to return if the predicate succeeds.</param>
    /// <returns>The original Result if condition is false; otherwise, a failure.</returns>
    /// <example>
    /// <code>
    /// var result = await GetUserAsync(userId)
    ///     .Unless(
    ///         user => user.IsBlocked,
    ///         new ValidationError("User is blocked")
    ///     );
    /// </code>
    /// </example>
    public static async Task<Result<T>> Unless<T>(
        this Task<Result<T>> resultTask,
        Func<T, bool> predicate,
        IResultError error)
    {
        if (predicate is null)
        {
            return Result<T>.Failure()
                .WithError(new Error("Predicate cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return result.Unless(predicate, error);
        }
        catch (Exception ex)
        {
            return Result<T>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    public static async Task<Result<T>> Unless<T>(
    this Task<Result<T>> resultTask,
    Func<T, Result> predicate)
    {
        if (predicate is null)
        {
            return Result<T>.Failure()
                .WithError(new Error("Predicate cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return result.Unless(predicate);
        }
        catch (Exception ex)
        {
            return Result<T>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Converts a successful Result task to a failure if an async condition is met.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="resultTask">The Result task to check.</param>
    /// <param name="predicate">The async condition that must be false.</param>
    /// <param name="error">The error to return if the predicate succeeds.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The original Result if condition is false; otherwise, a failure.</returns>
    /// <example>
    /// <code>
    /// var result = await GetUserAsync(userId)
    ///     .UnlessAsync(
    ///         async (user, ct) => await IsBlacklistedAsync(user),
    ///         new BlacklistError("User is blacklisted"),
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public static async Task<Result<T>> UnlessAsync<T>(
        this Task<Result<T>> resultTask,
        Func<T, CancellationToken, Task<bool>> predicate,
        IResultError error,
        CancellationToken cancellationToken = default)
    {
        if (predicate is null)
        {
            return Result<T>.Failure()
                .WithError(new Error("Predicate cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return await result.UnlessAsync(predicate, error, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Result<T>.Failure()
                .WithError(new OperationCancelledError());
        }
        catch (Exception ex)
        {
            return Result<T>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    public static async Task<Result<T>> UnlessAsync<T>(
    this Task<Result<T>> resultTask,
    Func<T, CancellationToken, Task<Result>> predicate,
    CancellationToken cancellationToken = default)
    {
        if (predicate is null)
        {
            return Result<T>.Failure()
                .WithError(new Error("Predicate cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return await result.UnlessAsync(predicate, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Result<T>.Failure()
                .WithError(new OperationCancelledError());
        }
        catch (Exception ex)
        {
            return Result<T>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Provides a fallback value if the Result task fails.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="resultTask">The Result task.</param>
    /// <param name="fallback">The function providing the fallback value.</param>
    /// <returns>The original Result if successful; otherwise, a success with the fallback value.</returns>
    /// <example>
    /// <code>
    /// var user = await GetUserFromCacheAsync(userId)
    ///     .OrElse(() => GetDefaultUser());
    /// </code>
    /// </example>
    public static async Task<Result<T>> OrElse<T>(
        this Task<Result<T>> resultTask,
        Func<T> fallback)
    {
        if (fallback is null)
        {
            return Result<T>.Failure()
                .WithError(new Error("Fallback cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return result.OrElse(fallback);
        }
        catch (Exception ex)
        {
            return Result<T>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Provides an async fallback value if the Result task fails.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="resultTask">The Result task.</param>
    /// <param name="fallback">The async function providing the fallback value.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The original Result if successful; otherwise, a success with the fallback value.</returns>
    /// <example>
    /// <code>
    /// var user = await GetUserFromCacheAsync(userId)
    ///     .OrElseAsync(
    ///         async ct => await LoadUserFromDatabaseAsync(userId),
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public static async Task<Result<T>> OrElseAsync<T>(
        this Task<Result<T>> resultTask,
        Func<CancellationToken, Task<T>> fallback,
        CancellationToken cancellationToken = default)
    {
        if (fallback is null)
        {
            return Result<T>.Failure()
                .WithError(new Error("Fallback cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return await result.OrElseAsync(async _ => await fallback(cancellationToken), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Result<T>.Failure()
                .WithError(new OperationCancelledError());
        }
        catch (Exception ex)
        {
            return Result<T>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Chains a new operation if the Result task is successful, preserving the original value.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="resultTask">The Result task to chain from.</param>
    /// <param name="operation">The operation to execute if successful.</param>
    /// <returns>The original Result with the same value if successful.</returns>
    /// <example>
    /// <code>
    /// var result = await GetUserAsync()
    ///     .AndThen(user => ValidateUser(user))
    ///     .AndThen(user => SaveUser(user));
    /// </code>
    /// </example>
    public static async Task<Result<T>> AndThen<T>(
        this Task<Result<T>> resultTask,
        Action<T> operation)
    {
        if (operation is null)
        {
            return Result<T>.Failure()
                .WithError(new Error("Operation cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return result.AndThen(operation);
        }
        catch (Exception ex)
        {
            return Result<T>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Chains a new async operation if the Result task is successful, preserving the original value.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="resultTask">The Result task to chain from.</param>
    /// <param name="operation">The async operation to execute if successful.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The original Result with the same value if successful.</returns>
    /// <example>
    /// <code>
    /// var result = await GetUserAsync()
    ///     .AndThenAsync(
    ///         async (user, ct) => await ValidateUserAsync(user),
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public static async Task<Result<T>> AndThenAsync<T>(
        this Task<Result<T>> resultTask,
        Func<T, CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        if (operation is null)
        {
            return Result<T>.Failure()
                .WithError(new Error("Operation cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return await result.AndThenAsync(operation, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Result<T>.Failure()
                .WithError(new OperationCancelledError("Operation was cancelled"));
        }
        catch (Exception ex)
        {
            return Result<T>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Maps both success and failure cases of a Result task simultaneously.
    /// </summary>
    /// <typeparam name="T">The type of the source value.</typeparam>
    /// <typeparam name="TNew">The type of the new value.</typeparam>
    /// <param name="resultTask">The Result task to transform.</param>
    /// <param name="onSuccess">Function to transform the value if successful.</param>
    /// <param name="onFailure">Function to transform the errors if failed.</param>
    /// <returns>A new Result with either the transformed value or transformed errors.</returns>
    /// <example>
    /// <code>
    /// var result = await GetUserAsync(userId)
    ///     .BiMap(
    ///         user => new UserDto(user),
    ///         errors => errors.Select(e => new PublicError(e.Message))
    ///     );
    /// </code>
    /// </example>
    public static async Task<Result<TNew>> BiMap<T, TNew>(
        this Task<Result<T>> resultTask,
        Func<T, TNew> onSuccess,
        Func<IReadOnlyList<IResultError>, IEnumerable<IResultError>> onFailure)
    {
        if (onSuccess is null || onFailure is null)
        {
            return Result<TNew>.Failure()
                .WithError(new Error("Success or failure mapper cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return result.BiMap(onSuccess, onFailure);
        }
        catch (Exception ex)
        {
            return Result<TNew>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Maps a Result's task value to a new Result with an optional value.
    /// </summary>
    /// <typeparam name="T">The type of the source value.</typeparam>
    /// <typeparam name="TNew">The type of the new value.</typeparam>
    /// <param name="resultTask">The Result task to transform.</param>
    /// <param name="chooser">Function that returns Some value for items to keep, None for items to filter out.</param>
    /// <returns>A new Result containing the chosen value if successful, or the original errors if not.</returns>
    /// <example>
    /// <code>
    /// var result = await GetUserAsync(userId)
    ///     .Choose(user => user.Age >= 18
    ///         ? Option{UserDto}.Some(new UserDto(user))
    ///         : Option{UserDto}.None());
    /// </code>
    /// </example>
    public static async Task<Result<TNew>> Choose<T, TNew>(
        this Task<Result<T>> resultTask,
        Func<T, ResultChooseOption<TNew>> chooser)
    {
        if (chooser is null)
        {
            return Result<TNew>.Failure()
                .WithError(new Error("Chooser cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return result.Choose(chooser);
        }
        catch (Exception ex)
        {
            return Result<TNew>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Maps a Result's task value to a new Result with an optional value asynchronously.
    /// </summary>
    /// <typeparam name="T">The type of the source value.</typeparam>
    /// <typeparam name="TNew">The type of the new value.</typeparam>
    /// <param name="resultTask">The Result task to transform.</param>
    /// <param name="chooser">Async function that returns Some value for items to keep, None for items to filter out.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A new Result containing the chosen value if successful, or the original errors if not.</returns>
    /// <example>
    /// <code>
    /// var result = await GetUserAsync(userId)
    ///     .ChooseAsync(async (user, ct) => {
    ///         var permissions = await GetPermissionsAsync(user);
    ///         return permissions.HasAccess
    ///             ? Option{UserDto}.Some(new UserDto(user))
    ///             : Option{UserDto}.None();
    ///     }, cancellationToken);
    /// </code>
    /// </example>
    public static async Task<Result<TNew>> ChooseAsync<T, TNew>(
        this Task<Result<T>> resultTask,
        Func<T, CancellationToken, Task<ResultChooseOption<TNew>>> chooser,
        CancellationToken cancellationToken = default)
    {
        if (chooser is null)
        {
            return Result<TNew>.Failure()
                .WithError(new Error("Chooser cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return await result.ChooseAsync(chooser, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Result<TNew>.Failure()
                .WithError(new OperationCancelledError());
        }
        catch (Exception ex)
        {
            return Result<TNew>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Transforms a sequence Result task by applying an operation to each element.
    /// </summary>
    /// <typeparam name="TInput">The type of the input elements.</typeparam>
    /// <typeparam name="TOutput">The type of the output elements.</typeparam>
    /// <param name="resultTask">The Result task containing the sequence to process.</param>
    /// <param name="operation">The operation to apply to each element.</param>
    /// <returns>A Result containing the transformed sequence or all errors encountered.</returns>
    /// <example>
    /// <code>
    /// var result = await GetUsersAsync()
    ///     .Collect(user => ValidateAndTransformUser(user));
    /// </code>
    /// </example>
    public static async Task<Result<IEnumerable<TOutput>>> Collect<TInput, TOutput>(
        this Task<Result<IEnumerable<TInput>>> resultTask,
        Func<TInput, Result<TOutput>> operation)
    {
        if (operation is null)
        {
            return Result<IEnumerable<TOutput>>.Failure()
                .WithError(new Error("Operation cannot be null"));
        }

        try
        {
            var result = await resultTask;
            return result.Collect(operation);
        }
        catch (Exception ex)
        {
            return Result<IEnumerable<TOutput>>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Transforms a sequence Result task by applying an async operation to each element.
    /// </summary>
    /// <typeparam name="TInput">The type of the input elements.</typeparam>
    /// <typeparam name="TOutput">The type of the output elements.</typeparam>
    /// <param name="resultTask">The Result task containing the sequence to process.</param>
    /// <param name="operation">The async operation to apply to each element.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A Result containing the transformed sequence or all errors encountered.</returns>
    /// <example>
    /// <code>
    /// var result = await GetUsersAsync()
    ///     .CollectAsync(
    ///         async (user, ct) => await ValidateAndTransformUserAsync(user),
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public static async Task<Result<IEnumerable<TOutput>>> CollectAsync<TInput, TOutput>(
        this Task<Result<IEnumerable<TInput>>> resultTask,
        Func<TInput, CancellationToken, Task<Result<TOutput>>> operation,
        CancellationToken cancellationToken = default)
    {
        if (operation is null)
        {
            return Result<IEnumerable<TOutput>>.Failure()
                .WithError(new Error("Operation cannot be null"));
        }

        try
        {
            var result = await resultTask;
            return await result.CollectAsync(operation, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Result<IEnumerable<TOutput>>.Failure()
                .WithError(new OperationCancelledError());
        }
        catch (Exception ex)
        {
            return Result<IEnumerable<TOutput>>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Executes different functions based on the Result task's success state.
    /// </summary>
    /// <typeparam name="T">The type of the source value.</typeparam>
    /// <typeparam name="TResult">The type of the result to return.</typeparam>
    /// <param name="resultTask">The Result task to match on.</param>
    /// <param name="onSuccess">Function to execute if the Result is successful.</param>
    /// <param name="onFailure">Function to execute if the Result failed.</param>
    /// <returns>The result of either the success or failure function.</returns>
    /// <example>
    /// <code>
    /// var message = await GetUserAsync(userId)
    ///     .Match(
    ///         user => $"Found user: {user.Name}",
    ///         errors => $"User lookup failed"
    ///     );
    /// </code>
    /// </example>
    public static async Task<TResult> Match<T, TResult>(
        this Task<Result<T>> resultTask,
        Func<T, TResult> onSuccess,
        Func<IReadOnlyList<IResultError>, TResult> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        try
        {
            var result = await resultTask;

            return result.Match(onSuccess, onFailure);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Error during pattern matching", ex);
        }
    }

    /// <summary>
    ///     Returns different values based on the Result task's success state.
    /// </summary>
    /// <typeparam name="T">The type of the source value.</typeparam>
    /// <typeparam name="TResult">The type of the result to return.</typeparam>
    /// <param name="resultTask">The Result task to match on.</param>
    /// <param name="success">Value to return if successful.</param>
    /// <param name="failure">Value to return if failed.</param>
    /// <returns>Either the success or failure value.</returns>
    /// <example>
    /// <code>
    /// var status = await GetUserAsync(userId)
    ///     .Match(
    ///         success: "User is valid",
    ///         failure: "User is invalid"
    ///     );
    /// </code>
    /// </example>
    public static async Task<TResult> Match<T, TResult>(
        this Task<Result<T>> resultTask,
        TResult success,
        TResult failure)
    {
        try
        {
            var result = await resultTask;

            return result.Match(success, failure);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Error during pattern matching", ex);
        }
    }

    /// <summary>
    ///     Executes different async functions based on the Result task's success state.
    /// </summary>
    /// <typeparam name="T">The type of the source value.</typeparam>
    /// <typeparam name="TResult">The type of the result to return.</typeparam>
    /// <param name="resultTask">The Result task to match on.</param>
    /// <param name="onSuccess">Async function to execute if successful.</param>
    /// <param name="onFailure">Async function to execute if failed.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The result of either the success or failure function.</returns>
    /// <example>
    /// <code>
    /// var result = await GetUserAsync(userId)
    ///     .MatchAsync(
    ///         async (user, ct) => await FormatUserDetailsAsync(user),
    ///         async (errors, ct) => await FormatErrorsAsync(errors),
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public static async Task<TResult> MatchAsync<T, TResult>(
        this Task<Result<T>> resultTask,
        Func<T, CancellationToken, Task<TResult>> onSuccess,
        Func<IReadOnlyList<IResultError>, CancellationToken, Task<TResult>> onFailure,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        try
        {
            var result = await resultTask;

            return await result.MatchAsync(onSuccess, onFailure, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw; // Propagate cancellation
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Error during pattern matching", ex);
        }
    }

    /// <summary>
    ///     Executes an async success function with a synchronous failure handler.
    /// </summary>
    /// <typeparam name="T">The type of the source value.</typeparam>
    /// <typeparam name="TResult">The type of the result to return.</typeparam>
    /// <param name="resultTask">The Result task to match on.</param>
    /// <param name="onSuccess">Async function to execute if successful.</param>
    /// <param name="onFailure">Synchronous function to execute if failed.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The result of either the success or failure function.</returns>
    /// <example>
    /// <code>
    /// var result = await GetUserAsync(userId)
    ///     .MatchAsync(
    ///         async (user, ct) => await ProcessUserAsync(user),
    ///         errors => "Failed to process user",
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public static async Task<TResult> MatchAsync<T, TResult>(
        this Task<Result<T>> resultTask,
        Func<T, CancellationToken, Task<TResult>> onSuccess,
        Func<IReadOnlyList<IResultError>, TResult> onFailure,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        try
        {
            var result = await resultTask;

            return await result.MatchAsync(onSuccess, onFailure, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw; // Propagate cancellation
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Error during pattern matching", ex);
        }
    }

    /// <summary>
    ///     Executes a synchronous success function with an async failure handler.
    /// </summary>
    /// <typeparam name="T">The type of the source value.</typeparam>
    /// <typeparam name="TResult">The type of the result to return.</typeparam>
    /// <param name="resultTask">The Result task to match on.</param>
    /// <param name="onSuccess">Synchronous function to execute if successful.</param>
    /// <param name="onFailure">Async function to execute if failed.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The result of either the success or failure function.</returns>
    /// <example>
    /// <code>
    /// var result = await GetUserAsync(userId)
    ///     .MatchAsync(
    ///         user => $"Processing user: {user.Name}",
    ///         async (errors, ct) => await HandleErrorsAsync(errors),
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public static async Task<TResult> MatchAsync<T, TResult>(
        this Task<Result<T>> resultTask,
        Func<T, TResult> onSuccess,
        Func<IReadOnlyList<IResultError>, CancellationToken, Task<TResult>> onFailure,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        try
        {
            var result = await resultTask;

            return await result.MatchAsync(onSuccess, onFailure, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw; // Propagate cancellation
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Error during pattern matching", ex);
        }
    }

    /// <summary>
    ///     Validates the Result task's value using a specified FluentValidation validator.
    /// </summary>
    /// <typeparam name="T">The type of value to validate.</typeparam>
    /// <param name="resultTask">The Result task containing the value to validate.</param>
    /// <param name="validator">The FluentValidation validator to use.</param>
    /// <param name="options">Optional validation strategy options to apply.</param>
    /// <returns>A Result indicating validation success or failure.</returns>
    /// <example>
    /// <code>
    /// // Basic validation
    /// var result = await GetUserAsync()
    ///     .Validate(new UserValidator());
    ///
    /// // Validation with strategy
    /// var result = await GetUserAsync()
    ///     .Validate(
    ///         new UserValidator(),
    ///         strategy => strategy.IncludeRuleSets("BasicValidation")
    ///     );
    /// </code>
    /// </example>
    public static async Task<Result<T>> Validate<T>(
        this Task<Result<T>> resultTask,
        IValidator<T> validator,
        Action<ValidationStrategy<T>> options = null)
    {
        if (validator is null)
        {
            return Result<T>.Failure()
                .WithError(new Error("Validator cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return result.Validate(validator, options);
        }
        catch (Exception ex)
        {
            return Result<T>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage("Validation failed due to an error");
        }
    }

    /// <summary>
    ///     Validates the Result task's value asynchronously using a specified FluentValidation validator.
    /// </summary>
    /// <typeparam name="T">The type of value to validate.</typeparam>
    /// <param name="resultTask">The Result task containing the value to validate.</param>
    /// <param name="validator">The FluentValidation validator to use.</param>
    /// <param name="options">Optional validation strategy options to apply.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A Result indicating validation success or failure.</returns>
    /// <example>
    /// <code>
    /// // Basic async validation
    /// var result = await GetUserAsync()
    ///     .ValidateAsync(new UserValidator(), cancellationToken: ct);
    ///
    /// // Async validation with strategy
    /// var result = await GetUserAsync()
    ///     .ValidateAsync(
    ///         new UserValidator(),
    ///         strategy => strategy
    ///             .IncludeRuleSets("Create")
    ///             .IncludeProperties(x => x.Email),
    ///         cancellationToken: ct
    ///     );
    /// </code>
    /// </example>
    public static async Task<Result<T>> ValidateAsync<T>(
        this Task<Result<T>> resultTask,
        IValidator<T> validator,
        Action<ValidationStrategy<T>> options = null,
        CancellationToken cancellationToken = default)
    {
        if (validator is null)
        {
            return Result<T>.Failure()
                .WithError(new Error("Validator cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return await result.ValidateAsync(validator, options, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Result<T>.Failure()
                .WithError(new OperationCancelledError());
        }
        catch (Exception ex)
        {
            return Result<T>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage("Validation failed due to an error");
        }
    }

    /// <summary>
    ///     Validates each item in a collection using a specified FluentValidation validator.
    /// </summary>
    /// <typeparam name="TList">The type of the collection.</typeparam>
    /// <typeparam name="TValue">The type of items to validate.</typeparam>
    /// <param name="result">The Result containing the collection.</param>
    /// <param name="validator">The FluentValidation validator to use.</param>
    /// <param name="options">Optional validation strategy options to apply.</param>
    /// <returns>The original Result if all validations succeed; otherwise, a failure with validation errors.</returns>
    /// <example>
    /// <code>
    /// var result = Result{List{User}}.Success(users)
    ///     .ValidateEach(new UserValidator());
    /// </code>
    /// </example>
    public static Result<TList> Validate<TList, TValue>(
        this Result<TList> result,
        IValidator<TValue> validator,
        Action<ValidationStrategy<TValue>> options = null)
        where TList : IEnumerable<TValue>
    {
        if (!result.IsSuccess)
        {
            return result;
        }

        if (validator is null)
        {
            return Result<TList>.Failure()
                .WithError(new Error("Validator cannot be null"));
        }

        try
        {
            var errors = new List<IResultError>();
            foreach (var item in result.Value)
            {
                var validationResult = options is null
                    ? validator.Validate(item)
                    : validator.Validate(item, options);

                if (!validationResult.IsValid)
                {
                    errors.Add(new FluentValidationError(validationResult));
                }
            }

            if (errors.Any())
            {
                return Result<TList>.Failure(result.Value)
                    .WithErrors(errors)
                    .WithMessages(result.Messages);
            }

            return result;
        }
        catch (Exception ex)
        {
            return Result<TList>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Validates each item in a collection asynchronously using a specified FluentValidation validator.
    /// </summary>
    /// <typeparam name="TList">The type of the collection.</typeparam>
    /// <typeparam name="TItem">The type of items to validate.</typeparam>
    /// <param name="result">The Result containing the collection.</param>
    /// <param name="validator">The FluentValidation validator to use.</param>
    /// <param name="options">Optional validation strategy options to apply.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The original Result if all validations succeed; otherwise, a failure with validation errors.</returns>
    /// <example>
    /// <code>
    /// var result = await Result{List{User}}.Success(users)
    ///     .ValidateEachAsync(new UserValidator(), cancellationToken: ct);
    ///
    /// // With validation strategy
    /// var result = await Result{List{User}}.Success(users)
    ///     .ValidateEachAsync(
    ///         new UserValidator(),
    ///         strategy => strategy.IncludeRuleSets("Create"),
    ///         cancellationToken: ct
    ///     );
    /// </code>
    /// </example>
    public static async Task<Result<TList>> ValidateAsync<TList, TItem>(
        this Result<TList> result,
        IValidator<TItem> validator,
        Action<ValidationStrategy<TItem>> options = null,
        CancellationToken cancellationToken = default)
        where TList : IEnumerable<TItem>
    {
        if (!result.IsSuccess)
        {
            return result;
        }

        if (validator is null)
        {
            return Result<TList>.Failure()
                .WithError(new Error("Validator cannot be null"));
        }

        try
        {
            var errors = new List<IResultError>();
            foreach (var item in result.Value)
            {
                var validationResult = options is null
                    ? await validator.ValidateAsync(item, cancellationToken)
                    : await validator.ValidateAsync(item, opt => options(opt), cancellationToken);

                if (!validationResult.IsValid)
                {
                    errors.Add(new FluentValidationError(validationResult));
                }
            }

            if (errors.Any())
            {
                return Result<TList>.Failure(result.Value)
                    .WithErrors(errors)
                    .WithMessages(result.Messages);
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            return Result<TList>.Failure()
                .WithError(new OperationCancelledError("Operation was cancelled"))
                .WithMessages(result.Messages);
        }
        catch (Exception ex)
        {
            return Result<TList>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Executes an operation in the chain without transforming the value.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="resultTask">The Result task to execute the action on.</param>
    /// <param name="operation">The action to execute.</param>
    /// <returns>The original Result.</returns>
    /// <example>
    /// <code>
    /// var result = await GetUserAsync()
    ///     .Do(() => InitializeSystem())
    ///     .Then(user => ProcessUser(user));
    /// </code>
    /// </example>
    public static async Task<Result<T>> Do<T>(
        this Task<Result<T>> resultTask,
        Action operation)
    {
        if (operation is null)
        {
            return Result<T>.Failure()
                .WithError(new Error("Action cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return result.Do(operation);
        }
        catch (Exception ex)
        {
            return Result<T>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Executes an async operation in the chain without transforming the value.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="resultTask">The Result task to execute the action on.</param>
    /// <param name="operation">The async action to execute.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The original Result.</returns>
    /// <example>
    /// <code>
    /// var result = await GetUserAsync()
    ///     .DoAsync(
    ///         async ct => await InitializeSystemAsync(ct),
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public static async Task<Result<T>> DoAsync<T>(
        this Task<Result<T>> resultTask,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        if (operation is null)
        {
            return Result<T>.Failure()
                .WithError(new Error("Action cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return await result.DoAsync(operation, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Result<T>.Failure()
                .WithError(new OperationCancelledError("Operation was cancelled"));
        }
        catch (Exception ex)
        {
            return Result<T>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    public static async Task<Result<TOutput>> Wrap<TOutput>(this Task<Result<TOutput>> resultTask)
    {
        try
        {
            var result = await resultTask;

            return result.Wrap<TOutput>();
        }
        catch (OperationCanceledException)
        {
            return Result<TOutput>.Failure()
                .WithError(new OperationCancelledError("Operation was cancelled"));
        }
        catch (Exception ex)
        {
            return Result<TOutput>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    public static async Task<Result> Unwrap<T>(this Task<Result<T>> resultTask)
    {
        var result = await resultTask;

        return result.Unwrap();
    }
}