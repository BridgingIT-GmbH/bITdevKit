// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.Logging;

public static partial class ResultNonGenericTaskExtensions
{
    /// <summary>
    /// Throws an exception if the Result task indicates failure.
    /// </summary>
    /// <param name="resultTask">The Result task to check.</param>
    /// <returns>The original Result if it represents a success; otherwise, throws an exception.</returns>
    /// <exception cref="ResultException">Thrown if the Result indicates failure and no specific error is present to throw.</exception>
    /// <example>
    /// <code>
    /// await GetSystemStatusAsync()
    ///     .ThrowIfFailed();
    /// </code>
    /// </example>
    public static async Task<Result> ThrowIfFailed(this Task<Result> resultTask)
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
    /// Throws a specified exception if the Result task indicates a failure.
    /// </summary>
    /// <typeparam name="TException">The type of exception to throw.</typeparam>
    /// <param name="resultTask">The Result task to check.</param>
    /// <returns>The current Result if it indicates success.</returns>
    /// <exception cref="TException">Thrown if the Result indicates a failure.</exception>
    /// <example>
    /// <code>
    /// await GetSystemStatusAsync()
    ///     .ThrowIfFailed{InvalidOperationException}();
    /// </code>
    /// </example>
    public static async Task<Result> ThrowIfFailed<TException>(this Task<Result> resultTask)
        where TException : Exception
    {
        try
        {
            var result = await resultTask;
            return result.ThrowIfFailed<TException>();
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
    /// Maps a successful Result task to a Result{T} using the provided value.
    /// </summary>
    /// <typeparam name="T">The type to map to.</typeparam>
    /// <param name="resultTask">The Result task to map.</param>
    /// <param name="value">The value to map to.</param>
    /// <returns>A new Result containing the mapped value or the original errors.</returns>
    /// <example>
    /// <code>
    /// var stringResult = await GetSystemStatusAsync()
    ///     .Map("System ready"); // Result{string}.Success("System ready")
    /// </code>
    /// </example>
    public static async Task<Result<T>> Map<T>(this Task<Result> resultTask, T value)
    {
        try
        {
            var result = await resultTask;
            return result.Map(value);
        }
        catch (Exception ex)
        {
            return Result<T>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    /// Maps a successful Result task to a Result{T} using the provided mapping function.
    /// </summary>
    /// <typeparam name="T">The type to map to.</typeparam>
    /// <param name="resultTask">The Result task to map.</param>
    /// <param name="mapper">The function to map the value.</param>
    /// <returns>A new Result containing the mapped value or the original errors.</returns>
    /// <example>
    /// <code>
    /// var stringResult = await GetSystemStatusAsync()
    ///     .Map(() => "System ready"); // Result{string}.Success("System ready")
    /// </code>
    /// </example>
    public static async Task<Result<T>> Map<T>(this Task<Result> resultTask, Func<T> mapper)
    {
        if (mapper is null)
        {
            return Result<T>.Failure()
                .WithError(new Error("Mapper cannot be null"));
        }

        try
        {
            var result = await resultTask;
            return result.Map(mapper);
        }
        catch (Exception ex)
        {
            return Result<T>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    /// Executes different actions based on the Result task's success state.
    /// </summary>
    /// <param name="resultTask">The Result task to handle.</param>
    /// <param name="onSuccess">Action to execute if the Result is successful.</param>
    /// <param name="onFailure">Action to execute if the Result failed, receiving the errors.</param>
    /// <returns>The original Result wrapped in a Task.</returns>
    /// <exception cref="ArgumentNullException">Thrown when onSuccess or onFailure is null.</exception>
    /// <example>
    /// <code>
    /// await GetSystemStatusAsync()
    ///     .Handle(
    ///         onSuccess: () => Console.WriteLine("System operational"),
    ///         onFailure: errors => Console.WriteLine($"System failed: {errors.Count} errors")
    ///     );
    /// </code>
    /// </example>
    public static async Task<Result> Handle(
        this Task<Result> resultTask,
        Action onSuccess,
        Action<IReadOnlyList<IResultError>> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        try
        {
            var result = await resultTask;
            return result.Handle(onSuccess, onFailure);
        }
        catch (Exception ex)
        {
            onFailure(new[] { Result.Settings.ExceptionErrorFactory(ex) }.ToList().AsReadOnly());
            return await resultTask; // Return the original result despite the exception
        }
    }

    /// <summary>
    /// Asynchronously executes different actions based on the Result task's success state.
    /// </summary>
    /// <param name="resultTask">The Result task to handle.</param>
    /// <param name="onSuccess">Async function to execute if the Result is successful.</param>
    /// <param name="onFailure">Async function to execute if the Result failed, receiving the errors.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A Task containing the original Result instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when onSuccess or onFailure is null.</exception>
    /// <example>
    /// <code>
    /// await GetSystemStatusAsync()
    ///     .HandleAsync(
    ///         async (ct) => await LogSuccessAsync(ct),
    ///         async (errors, ct) => await LogErrorsAsync(errors, ct),
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public static async Task<Result> HandleAsync(
        this Task<Result> resultTask,
        Func<CancellationToken, Task> onSuccess,
        Func<IReadOnlyList<IResultError>, CancellationToken, Task> onFailure,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        try
        {
            var result = await resultTask;
            return await result.HandleAsync(onSuccess, onFailure, cancellationToken);
        }
        catch (Exception ex)
        {
            await onFailure(new[] { Result.Settings.ExceptionErrorFactory(ex) }.ToList().AsReadOnly(), cancellationToken);
            return await resultTask; // Return the original result despite the exception
        }
    }

    /// <summary>
    /// Asynchronously executes a success function with a synchronous failure handler from a Task{Result}.
    /// </summary>
    /// <param name="resultTask">The Result task to handle.</param>
    /// <param name="onSuccess">Async function to execute if the Result is successful.</param>
    /// <param name="onFailure">Synchronous function to execute if the Result failed, receiving the errors.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A Task containing the original Result instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when onSuccess or onFailure is null.</exception>
    /// <example>
    /// <code>
    /// await GetSystemStatusAsync()
    ///     .HandleAsync(
    ///         async (ct) => await LogSuccessAsync(ct),
    ///         errors => Console.WriteLine($"Failed with {errors.Count} errors"),
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public static async Task<Result> HandleAsync(
        this Task<Result> resultTask,
        Func<CancellationToken, Task> onSuccess,
        Action<IReadOnlyList<IResultError>> onFailure,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        try
        {
            var result = await resultTask;
            return await result.HandleAsync(onSuccess, onFailure, cancellationToken);
        }
        catch (Exception ex)
        {
            onFailure(new[] { Result.Settings.ExceptionErrorFactory(ex) }.ToList().AsReadOnly());
            return await resultTask; // Return the original result despite the exception
        }
    }

    /// <summary>
    /// Executes a synchronous success function with an async failure handler from a Task{Result}.
    /// </summary>
    /// <param name="resultTask">The Result task to handle.</param>
    /// <param name="onSuccess">Synchronous function to execute if the Result is successful.</param>
    /// <param name="onFailure">Async function to execute if the Result failed, receiving the errors.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A Task containing the original Result instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when onSuccess or onFailure is null.</exception>
    /// <example>
    /// <code>
    /// await GetSystemStatusAsync()
    ///     .HandleAsync(
    ///         () => Console.WriteLine("Operation succeeded"),
    ///         async (errors, ct) => await LogErrorsAsync(errors, ct),
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public static async Task<Result> HandleAsync(
        this Task<Result> resultTask,
        Action onSuccess,
        Func<IReadOnlyList<IResultError>, CancellationToken, Task> onFailure,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        try
        {
            var result = await resultTask;
            return await result.HandleAsync(onSuccess, onFailure, cancellationToken);
        }
        catch (Exception ex)
        {
            await onFailure(new[] { Result.Settings.ExceptionErrorFactory(ex) }.ToList().AsReadOnly(), cancellationToken);
            return await resultTask; // Return the original result despite the exception
        }
    }

    /// <summary>
    /// Chains a new operation on a successful Result task.
    /// </summary>
    /// <param name="resultTask">The Result task to chain from.</param>
    /// <param name="operation">The operation to execute if successful.</param>
    /// <returns>A new Result with the operation result or the original errors.</returns>
    /// <example>
    /// <code>
    /// var result = await GetSystemStatusAsync()
    ///     .AndThen(() => ValidateSystem());
    /// </code>
    /// </example>
    public static async Task<Result> AndThen(
        this Task<Result> resultTask,
        Func<Result> operation)
    {
        if (operation is null)
        {
            return Result.Failure()
                .WithError(new Error("Operation cannot be null"));
        }

        try
        {
            var result = await resultTask;
            return result.AndThen(operation);
        }
        catch (Exception ex)
        {
            return Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    /// Chains a new async operation on a successful Result task.
    /// </summary>
    /// <param name="resultTask">The Result task to chain from.</param>
    /// <param name="operation">The async operation to execute if successful.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A new Result with the operation result or the original errors.</returns>
    /// <example>
    /// <code>
    /// var result = await GetSystemStatusAsync()
    ///     .AndThenAsync(
    ///         async (ct) => await ValidateSystemAsync(ct),
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public static async Task<Result> AndThenAsync(
        this Task<Result> resultTask,
        Func<CancellationToken, Task<Result>> operation,
        CancellationToken cancellationToken = default)
    {
        if (operation is null)
        {
            return Result.Failure()
                .WithError(new Error("Operation cannot be null"));
        }

        try
        {
            var result = await resultTask;
            return await result.AndThenAsync(operation, cancellationToken);
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
    /// Executes an operation in the chain without transforming the value of a Result task.
    /// </summary>
    /// <param name="resultTask">The Result task to execute the action on.</param>
    /// <param name="action">The action to execute.</param>
    /// <returns>The original Result wrapped in a Task.</returns>
    /// <example>
    /// <code>
    /// await GetSystemStatusAsync()
    ///     .Do(() => InitializeSystem());
    /// </code>
    /// </example>
    public static async Task<Result> Do(this Task<Result> resultTask, Action action)
    {
        if (action is null)
        {
            return Result.Failure()
                .WithError(new Error("Action cannot be null"));
        }

        try
        {
            var result = await resultTask;
            return result.Do(action);
        }
        catch (Exception ex)
        {
            return Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    /// Executes an async operation in the chain without transforming the value of a Result task.
    /// </summary>
    /// <param name="resultTask">The Result task to execute the action on.</param>
    /// <param name="action">The async action to execute.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The original Result wrapped in a Task.</returns>
    /// <example>
    /// <code>
    /// await GetSystemStatusAsync()
    ///     .DoAsync(
    ///         async ct => await InitializeSystemAsync(ct),
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public static async Task<Result> DoAsync(
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
            return await result.DoAsync(action, cancellationToken);
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
    /// Executes an action only if a condition is met, without changing the Result task.
    /// </summary>
    /// <param name="resultTask">The Result task to switch on.</param>
    /// <param name="condition">The condition to check.</param>
    /// <param name="action">The action to execute if condition is met.</param>
    /// <returns>The original Result wrapped in a Task.</returns>
    /// <example>
    /// <code>
    /// await GetSystemStatusAsync()
    ///     .Switch(
    ///         () => isAdmin,
    ///         () => _logger.LogInfo("Admin access")
    ///     );
    /// </code>
    /// </example>
    public static async Task<Result> Switch(
        this Task<Result> resultTask,
        Func<bool> condition,
        Action action)
    {
        if (condition is null || action is null)
        {
            return Result.Failure()
                .WithError(new Error("Condition or action cannot be null"));
        }

        try
        {
            var result = await resultTask;
            return result.Switch(condition, action);
        }
        catch (Exception ex)
        {
            return Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(ex.Message);
        }
    }

    /// <summary>
    /// Executes an async action only if a condition is met, without changing the Result task.
    /// </summary>
    /// <param name="resultTask">The Result task to switch on.</param>
    /// <param name="condition">The condition to check.</param>
    /// <param name="action">The async action to execute if condition is met.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The original Result wrapped in a Task.</returns>
    /// <example>
    /// <code>
    /// await GetSystemStatusAsync()
    ///     .SwitchAsync(
    ///         () => isAdmin,
    ///         async (ct) => await NotifyAdminAsync(ct),
    ///         cancellationToken
    ///     );
    /// </code>
    /// </example>
    public static async Task<Result> SwitchAsync(
        this Task<Result> resultTask,
        Func<bool> condition,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        if (condition is null || action is null)
        {
            return Result.Failure()
                .WithError(new Error("Condition or action cannot be null"));
        }

        try
        {
            var result = await resultTask;
            return await result.SwitchAsync(condition, action, cancellationToken);
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
    /// Logs an awaited <see cref="Task{Result}"/> using structured logging with default levels
    /// (Debug on success, Warning on failure).
    /// </summary>
    /// <param name="resultTask">The task returning a result to log.</param>
    /// <param name="logger">
    /// The logger to write to. If null, the method awaits and returns the original result.
    /// </param>
    /// <param name="messageTemplate">
    /// Optional message template for structured logging (e.g., "Completed {Operation}").
    /// </param>
    /// <param name="args">
    /// Optional structured logging arguments corresponding to <paramref name="messageTemplate"/>.
    /// </param>
    /// <remarks>
    /// This method awaits <paramref name="resultTask"/> and delegates to the synchronous overload.
    /// </remarks>
    /// <returns>The awaited <see cref="Result"/> from <paramref name="resultTask"/>, unchanged.</returns>
    /// <example>
    /// <code>
    /// var final = await service.ExecuteAsync().Log(logger, "Executed {Operation}", "Cleanup");
    /// </code>
    /// </example>
    public static async Task<Result> Log(
        this Task<Result> resultTask,
        ILogger logger,
        string messageTemplate = null,
        params object[] args)
    {
        var result = await resultTask.ConfigureAwait(false);
        return result.Log(logger, messageTemplate, args);
    }

    /// <summary>
    /// Logs an awaited <see cref="Task{Result}"/> using structured logging with custom levels.
    /// </summary>
    /// <param name="resultTask">The task returning a result to log.</param>
    /// <param name="logger">
    /// The logger to write to. If null, the method awaits and returns the original result.
    /// </param>
    /// <param name="messageTemplate">
    /// Optional message template for structured logging (e.g., "Handled {Operation}").
    /// </param>
    /// <param name="successLevel">The log level used when the result indicates success.</param>
    /// <param name="failureLevel">The log level used when the result indicates failure.</param>
    /// <param name="args">
    /// Optional structured logging arguments corresponding to <paramref name="messageTemplate"/>.
    /// </param>
    /// <remarks>
    /// This method awaits <paramref name="resultTask"/> and delegates to the synchronous overload.
    /// </remarks>
    /// <returns>The awaited <see cref="Result"/> from <paramref name="resultTask"/>, unchanged.</returns>
    /// <example>
    /// <code>
    /// var final = await op.RunAsync().Log(
    ///     logger,
    ///     "Operation {Name} finished",
    ///     LogLevel.Information,
    ///     LogLevel.Error,
    ///     opName);
    /// </code>
    /// </example>
    public static async Task<Result> Log(
        this Task<Result> resultTask,
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
    /// Logs an awaited Task&lt;Result&gt; using structured logging with default levels
    /// (Debug/Warning). The arguments for the message template are produced from the
    /// awaited result via <paramref name="argsFactory"/>.
    /// </summary>
    /// <param name="resultTask">The task returning a result to log.</param>
    /// <param name="logger">The logger to write to. If null, the method is a no-op.</param>
    /// <param name="messageTemplate">The message template to log.</param>
    /// <param name="argsFactory">Factory building arguments from the awaited result.</param>
    /// <returns>The awaited result, unchanged.</returns>
    public static async Task<Result> Log(
        this Task<Result> resultTask,
        ILogger logger,
        string messageTemplate,
        Func<Result, object[]> argsFactory)
    {
        var result = await resultTask.ConfigureAwait(false);
        return result.Log(logger, messageTemplate, argsFactory);
    }

    /// <summary>
    /// Logs an awaited Task&lt;Result&gt; using structured logging with custom levels.
    /// The arguments for the message template are produced from the awaited result via
    /// <paramref name="argsFactory"/>.
    /// </summary>
    /// <param name="resultTask">The task returning a result to log.</param>
    /// <param name="logger">The logger to write to. If null, the method is a no-op.</param>
    /// <param name="messageTemplate">The message template to log.</param>
    /// <param name="argsFactory">Factory building arguments from the awaited result.</param>
    /// <param name="successLevel">Log level on success.</param>
    /// <param name="failureLevel">Log level on failure.</param>
    /// <returns>The awaited result, unchanged.</returns>
    public static async Task<Result> Log(
        this Task<Result> resultTask,
        ILogger logger,
        string messageTemplate,
        Func<Result, object[]> argsFactory,
        LogLevel successLevel,
        LogLevel failureLevel)
    {
        var result = await resultTask.ConfigureAwait(false);
        return result.Log(logger, messageTemplate, argsFactory, successLevel, failureLevel);
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

            return await result.OrElseAsync(_ => fallback(cancellationToken), cancellationToken: cancellationToken);
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