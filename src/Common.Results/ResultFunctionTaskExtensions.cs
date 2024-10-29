// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

public static partial class ResultFunctionTaskExtensions
{
    /// <summary>
    ///     Ensures that a condition is met for the Result task.
    /// </summary>
    /// <param name="resultTask">The Result task to check.</param>
    /// <param name="predicate">The condition that must be true.</param>
    /// <param name="error">The error to return if the predicate fails.</param>
    /// <returns>The original Result if successful and predicate is met; otherwise, a failure.</returns>
    /// <example>
    /// <code>
    /// var result = await InitializeSystemAsync()
    ///     .Ensure(() => IsSystemReady(), new Error("System not ready"));
    /// </code>
    /// </example>
    public static async Task<Result> Ensure(
        this Task<Result> resultTask,
        Func<bool> predicate,
        IResultError error)
    {
        if (predicate is null)
        {
            return Result.Failure()
                .WithError(new Error("Predicate cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return result.Ensure(predicate, error);
        }
        catch (Exception ex)
        {
            return Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Ensures that an async condition is met for the Result task.
    /// </summary>
    /// <param name="resultTask">The Result task to check.</param>
    /// <param name="predicate">The async condition that must be true.</param>
    /// <param name="error">The error to return if the predicate fails.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The original Result if successful and predicate is met; otherwise, a failure.</returns>
    /// <example>
    /// <code>
    /// var result = await InitializeSystemAsync()
    ///     .EnsureAsync(
    ///         async ct => await IsSystemReadyAsync(ct),
    ///         new Error("System not ready"),
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public static async Task<Result> EnsureAsync(
        this Task<Result> resultTask,
        Func<CancellationToken, Task<bool>> predicate,
        IResultError error,
        CancellationToken cancellationToken = default)
    {
        if (predicate is null)
        {
            return Result.Failure()
                .WithError(new Error("Predicate cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return await result.EnsureAsync(predicate, error, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError());
        }
        catch (Exception ex)
        {
            return Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Wraps a Result task in a Try operation, catching any exceptions.
    /// </summary>
    /// <param name="resultTask">The Result task to wrap.</param>
    /// <returns>A Result containing the value or any caught exceptions.</returns>
    /// <example>
    /// <code>
    /// var result = await InitializeSystemAsync()
    ///     .Try();
    /// </code>
    /// </example>
    public static async Task<Result> Try(this Task<Result> resultTask)
    {
        try
        {
            return await resultTask;
        }
        catch (Exception ex)
        {
            return Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Wraps a Result task in a Try operation with cancellation support.
    /// </summary>
    /// <param name="resultTask">The Result task to wrap.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A Result containing the value or any caught exceptions.</returns>
    /// <example>
    /// <code>
    /// var result = await InitializeSystemAsync()
    ///     .TryAsync(cancellationToken);
    /// </code>
    /// </example>
    public static async Task<Result> TryAsync(
        this Task<Result> resultTask,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            return await resultTask;
        }
        catch (OperationCanceledException)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError());
        }
        catch (Exception ex)
        {
            return Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Executes an action on a successful Result task without changing its state.
    /// </summary>
    /// <param name="resultTask">The Result task to tap into.</param>
    /// <param name="action">The action to execute on success.</param>
    /// <returns>The original Result task unchanged.</returns>
    /// <example>
    /// <code>
    /// var result = await InitializeSystemAsync()
    ///     .Tap(() => _logger.LogInformation("System initialized"));
    /// </code>
    /// </example>
    public static async Task<Result> Tap(
        this Task<Result> resultTask,
        Action action)
    {
        if (action is null)
        {
            return Result.Failure()
                .WithError(new Error("Action cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return result.Tap(action);
        }
        catch (Exception ex)
        {
            return Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Executes an async action on a successful Result task without changing its state.
    /// </summary>
    /// <param name="resultTask">The Result task to tap into.</param>
    /// <param name="action">The async action to execute on success.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The original Result task unchanged.</returns>
    /// <example>
    /// <code>
    /// var result = await InitializeSystemAsync()
    ///     .TapAsync(
    ///         async ct => await _cache.ClearAsync(ct),
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public static async Task<Result> TapAsync(
        this Task<Result> resultTask,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        if (action is null)
        {
            return Result.Failure()
                .WithError(new Error("Action cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return await result.TapAsync(action, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError());
        }
        catch (Exception ex)
        {
            return Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Executes different actions based on the Result task's success or failure.
    /// </summary>
    /// <param name="resultTask">The Result task to process.</param>
    /// <param name="onSuccess">The action to execute on success.</param>
    /// <param name="onFailure">The action to execute on failure.</param>
    /// <returns>The original Result unchanged.</returns>
    /// <example>
    /// <code>
    /// var result = await InitializeSystemAsync()
    ///     .TeeMap(
    ///         () => _logger.LogInformation("Success"),
    ///         errors => _logger.LogError("Failure", errors)
    ///     );
    /// </code>
    /// </example>
    public static async Task<Result> TeeMap(
        this Task<Result> resultTask,
        Action onSuccess,
        Action<IReadOnlyList<IResultError>> onFailure)
    {
        try
        {
            var result = await resultTask;

            return result.TeeMap(onSuccess, onFailure);
        }
        catch (Exception ex)
        {
            return Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Executes different async actions based on the Result task's success or failure.
    /// </summary>
    /// <param name="resultTask">The Result task to process.</param>
    /// <param name="onSuccess">The async action to execute on success.</param>
    /// <param name="onFailure">The async action to execute on failure.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The original Result unchanged.</returns>
    /// <example>
    /// <code>
    /// var result = await InitializeSystemAsync()
    ///     .TeeMapAsync(
    ///         async ct => await _cache.ClearAsync(ct),
    ///         async (errors, ct) => await _logger.LogErrorsAsync(errors, ct),
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public static async Task<Result> TeeMapAsync(
        this Task<Result> resultTask,
        Func<CancellationToken, Task> onSuccess,
        Func<IReadOnlyList<IResultError>, CancellationToken, Task> onFailure,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await resultTask;

            return await result.TeeMapAsync(onSuccess, onFailure, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError());
        }
        catch (Exception ex)
        {
            return Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Filters a Result task based on a predicate.
    /// </summary>
    /// <param name="resultTask">The Result task to filter.</param>
    /// <param name="predicate">The condition that must be true.</param>
    /// <param name="error">The error to return if the predicate fails.</param>
    /// <returns>The original Result if successful and predicate is met; otherwise, a failure.</returns>
    /// <example>
    /// <code>
    /// var result = await InitializeSystemAsync()
    ///     .Filter(() => isSystemReady, new Error("System not ready"));
    /// </code>
    /// </example>
    public static async Task<Result> Filter(
        this Task<Result> resultTask,
        Func<bool> predicate,
        IResultError error)
    {
        if (predicate is null)
        {
            return Result.Failure()
                .WithError(new Error("Predicate cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return result.Filter(predicate, error);
        }
        catch (Exception ex)
        {
            return Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Filters a Result task based on an async predicate.
    /// </summary>
    /// <param name="resultTask">The Result task to filter.</param>
    /// <param name="predicate">The async condition that must be true.</param>
    /// <param name="error">The error to return if the predicate fails.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The original Result if successful and predicate is met; otherwise, a failure.</returns>
    /// <example>
    /// <code>
    /// var result = await InitializeSystemAsync()
    ///     .FilterAsync(
    ///         async ct => await CheckSystemStateAsync(ct),
    ///         new Error("Invalid system state"),
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public static async Task<Result> FilterAsync(
        this Task<Result> resultTask,
        Func<CancellationToken, Task<bool>> predicate,
        IResultError error,
        CancellationToken cancellationToken = default)
    {
        if (predicate is null)
        {
            return Result.Failure()
                .WithError(new Error("Predicate cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return await result.FilterAsync(predicate, error, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError());
        }
        catch (Exception ex)
        {
            return Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Converts a successful Result task to a failure if the specified predicate is met.
    /// </summary>
    /// <param name="resultTask">The Result task to check.</param>
    /// <param name="predicate">The condition that must be false.</param>
    /// <param name="error">The error to return if the predicate succeeds.</param>
    /// <returns>The original Result if condition is false; otherwise, a failure.</returns>
    /// <example>
    /// <code>
    /// var result = await InitializeSystemAsync()
    ///     .Unless(() => isSystemDown, new Error("System is down"));
    /// </code>
    /// </example>
    public static async Task<Result> Unless(
        this Task<Result> resultTask,
        Func<bool> predicate,
        IResultError error)
    {
        if (predicate is null)
        {
            return Result.Failure()
                .WithError(new Error("Predicate cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return result.Unless(predicate, error);
        }
        catch (Exception ex)
        {
            return Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Converts a successful Result task to a failure if the async predicate is met.
    /// </summary>
    /// <param name="resultTask">The Result task to check.</param>
    /// <param name="predicate">The async condition that must be false.</param>
    /// <param name="error">The error to return if the predicate succeeds.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The original Result if condition is false; otherwise, a failure.</returns>
    /// <example>
    /// <code>
    /// var result = await InitializeSystemAsync()
    ///     .UnlessAsync(
    ///         async ct => await IsSystemDownAsync(ct),
    ///         new Error("System is down"),
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public static async Task<Result> UnlessAsync(
        this Task<Result> resultTask,
        Func<CancellationToken, Task<bool>> predicate,
        IResultError error,
        CancellationToken cancellationToken = default)
    {
        if (predicate is null)
        {
            return Result.Failure()
                .WithError(new Error("Predicate cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return await result.UnlessAsync(predicate, error, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError());
        }
        catch (Exception ex)
        {
            return Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Provides a fallback Result if the Result task fails.
    /// </summary>
    /// <param name="resultTask">The Result task.</param>
    /// <param name="fallback">The function providing the fallback Result.</param>
    /// <returns>The original Result if successful; otherwise, the fallback Result.</returns>
    /// <example>
    /// <code>
    /// var result = await GetPrimarySystemAsync()
    ///     .OrElse(() => GetBackupSystemResult());
    /// </code>
    /// </example>
    public static async Task<Result> OrElse(
        this Task<Result> resultTask,
        Func<Result> fallback)
    {
        if (fallback is null)
        {
            return Result.Failure()
                .WithError(new Error("Fallback cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return result.OrElse(fallback);
        }
        catch (Exception ex)
        {
            return Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Provides an async fallback Result if the Result task fails.
    /// </summary>
    /// <param name="resultTask">The Result task.</param>
    /// <param name="fallback">The async function providing the fallback Result.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The original Result if successful; otherwise, the fallback Result.</returns>
    /// <example>
    /// <code>
    /// var result = await GetPrimarySystemAsync()
    ///     .OrElseAsync(
    ///         async ct => await GetBackupSystemAsync(ct),
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public static async Task<Result> OrElseAsync(
        this Task<Result> resultTask,
        Func<CancellationToken, Task<Result>> fallback,
        CancellationToken cancellationToken = default)
    {
        if (fallback is null)
        {
            return Result.Failure()
                .WithError(new Error("Fallback cannot be null"));
        }

        try
        {
            var result = await resultTask;

            return await result.OrElseAsync(_ => fallback(cancellationToken));
        }
        catch (OperationCanceledException)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError());
        }
        catch (Exception ex)
        {
            return Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    ///     Executes different functions based on the Result task's success state.
    /// </summary>
    /// <typeparam name="TResult">The type of the result to return.</typeparam>
    /// <param name="resultTask">The Result task to match on.</param>
    /// <param name="onSuccess">Function to execute if successful.</param>
    /// <param name="onFailure">Function to execute if failed.</param>
    /// <returns>The result of either the success or failure function.</returns>
    /// <example>
    /// <code>
    /// var message = await InitializeSystemAsync()
    ///     .Match(
    ///         onSuccess: () => "System ready",
    ///         onFailure: errors => $"System failed: {errors.First().Message}"
    ///     );
    /// </code>
    /// </example>
    public static async Task<TResult> Match<TResult>(
        this Task<Result> resultTask,
        Func<TResult> onSuccess,
        Func<IReadOnlyList<IResultError>, TResult> onFailure)
    {
        if (onSuccess is null)
        {
            throw new ArgumentNullException(nameof(onSuccess));
        }

        if (onFailure is null)
        {
            throw new ArgumentNullException(nameof(onFailure));
        }

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
    /// <typeparam name="TResult">The type of the result to return.</typeparam>
    /// <param name="resultTask">The Result task to match on.</param>
    /// <param name="success">Value to return if successful.</param>
    /// <param name="failure">Value to return if failed.</param>
    /// <returns>Either the success or failure value.</returns>
    /// <example>
    /// <code>
    /// var status = await InitializeSystemAsync()
    ///     .Match(
    ///         success: "OPERATIONAL",
    ///         failure: "FAILED"
    ///     );
    /// </code>
    /// </example>
    public static async Task<TResult> Match<TResult>(
        this Task<Result> resultTask,
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
    /// <typeparam name="TResult">The type of the result to return.</typeparam>
    /// <param name="resultTask">The Result task to match on.</param>
    /// <param name="onSuccess">Async function to execute if successful.</param>
    /// <param name="onFailure">Async function to execute if failed.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The result of either the success or failure function.</returns>
    /// <example>
    /// <code>
    /// var report = await InitializeSystemAsync()
    ///     .MatchAsync(
    ///         async ct => await GenerateSuccessReportAsync(ct),
    ///         async (errors, ct) => await GenerateErrorReportAsync(errors, ct),
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public static async Task<TResult> MatchAsync<TResult>(
        this Task<Result> resultTask,
        Func<CancellationToken, Task<TResult>> onSuccess,
        Func<IReadOnlyList<IResultError>, CancellationToken, Task<TResult>> onFailure,
        CancellationToken cancellationToken = default)
    {
        if (onSuccess is null)
        {
            throw new ArgumentNullException(nameof(onSuccess));
        }

        if (onFailure is null)
        {
            throw new ArgumentNullException(nameof(onFailure));
        }

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
    /// <typeparam name="TResult">The type of the result to return.</typeparam>
    /// <param name="resultTask">The Result task to match on.</param>
    /// <param name="onSuccess">Async function to execute if successful.</param>
    /// <param name="onFailure">Synchronous function to execute if failed.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The result of either the success or failure function.</returns>
    /// <example>
    /// <code>
    /// var result = await InitializeSystemAsync()
    ///     .MatchAsync(
    ///         async ct => await GenerateSuccessReportAsync(ct),
    ///         errors => "Failed to generate report",
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public static async Task<TResult> MatchAsync<TResult>(
        this Task<Result> resultTask,
        Func<CancellationToken, Task<TResult>> onSuccess,
        Func<IReadOnlyList<IResultError>, TResult> onFailure,
        CancellationToken cancellationToken = default)
    {
        if (onSuccess is null)
        {
            throw new ArgumentNullException(nameof(onSuccess));
        }

        if (onFailure is null)
        {
            throw new ArgumentNullException(nameof(onFailure));
        }

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
    /// <typeparam name="TResult">The type of the result to return.</typeparam>
    /// <param name="resultTask">The Result task to match on.</param>
    /// <param name="onSuccess">Synchronous function to execute if successful.</param>
    /// <param name="onFailure">Async function to execute if failed.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The result of either the success or failure function.</returns>
    /// <example>
    /// <code>
    /// var result = await InitializeSystemAsync()
    ///     .MatchAsync(
    ///         () => "System initialized",
    ///         async (errors, ct) => await FormatErrorsAsync(errors, ct),
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public static async Task<TResult> MatchAsync<TResult>(
        this Task<Result> resultTask,
        Func<TResult> onSuccess,
        Func<IReadOnlyList<IResultError>, CancellationToken, Task<TResult>> onFailure,
        CancellationToken cancellationToken = default)
    {
        if (onSuccess is null)
        {
            throw new ArgumentNullException(nameof(onSuccess));
        }

        if (onFailure is null)
        {
            throw new ArgumentNullException(nameof(onFailure));
        }

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
}