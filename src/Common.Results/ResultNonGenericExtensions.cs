namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.Logging;

/// <summary>
/// Provides extension methods to enhance the functionality of the Result struct.
/// </summary>
public static class ResultNonGenericExtensions
{
    /// <summary>
    /// Throws an exception if the result indicates failure.
    /// </summary>
    /// <returns>The original result if it represents a success; otherwise, throws an exception.</returns>
    public static Result ThrowIfFailed(this Result result)
    {
        if (result.IsSuccess)
        {
            return result;
        }

        if (result.HasError())
        {
            result.Errors.FirstOrDefault()?.Throw();
        }

        throw new ResultException(result);
    }

    /// <summary>
    /// Throws a specified exception if the result indicates a failure.
    /// </summary>
    /// <typeparam name="TException">The type of exception to throw.</typeparam>
    /// <returns>The current Result if it indicates success.</returns>
    /// <exception>Thrown if the result indicates a failure.
    ///     <cref>TException</cref>
    /// </exception>
    public static Result ThrowIfFailed<TException>(this Result result)
        where TException : Exception
    {
        if (result.IsSuccess)
        {
            return result;
        }

        throw ((TException)Activator.CreateInstance(typeof(TException), result.Errors.FirstOrDefault()?.Message, result))!;
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
    public static Result<T> Map<T>(this Result result, T value)
    {
        if (!result.IsSuccess)
        {
            return Result<T>.Failure()
                .WithErrors(result.Errors)
                .WithMessages(result.Messages);
        }

        try
        {
            return Result<T>.Success(value)
                .WithMessages(result.Messages);
        }
        catch (Exception ex)
        {
            return Result<T>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(result.Messages);
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
    public static Result<T> Map<T>(this Result result, Func<T> mapper)
    {
        if (!result.IsSuccess || mapper is null)
        {
            return Result<T>.Failure()
                .WithErrors(result.Errors)
                .WithMessages(result.Messages);
        }

        try
        {
            var value = mapper();

            return Result<T>.Success(value)
                .WithMessages(result.Messages);
        }
        catch (Exception ex)
        {
            return Result<T>.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(result.Messages);
        }
    }

    /// <summary>
    /// Executes different functions based on the Result's success state.
    /// </summary>
    /// <typeparam name="TResult">The type of the return value.</typeparam>
    /// <param name="result">The Result to match on.</param>
    /// <param name="onSuccess">Function to execute if the Result is successful.</param>
    /// <param name="onFailure">Function to execute if the Result failed, receiving the errors.</param>
    /// <returns>The result of either the success or failure function.</returns>
    /// <exception cref="ArgumentNullException">Thrown when onSuccess or onFailure is null.</exception>
    /// <example>
    /// <code>
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
    /// </code>
    /// </example>
    public static TResult Match<TResult>(
        this Result result,
        Func<TResult> onSuccess,
        Func<IReadOnlyList<IResultError>, TResult> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        return result.IsSuccess ? onSuccess() : onFailure(result.Errors);
    }

    /// <summary>
    /// Returns different values based on the Result's success state.
    /// </summary>
    /// <typeparam name="TResult">The type of the return value.</typeparam>
    /// <param name="result">The Result to match on.</param>
    /// <param name="success">Value to return if successful.</param>
    /// <param name="failure">Value to return if failed.</param>
    /// <returns>Either the success or failure value.</returns>
    /// <example>
    /// <code>
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
    /// </code>
    /// </example>
    public static TResult Match<TResult>(
        this Result result,
        TResult success,
        TResult failure)
    {
        return result.IsSuccess ? success : failure;
    }

    /// <summary>
    /// Asynchronously executes different functions based on the Result's success state.
    /// </summary>
    /// <typeparam name="TResult">The type of the return value.</typeparam>
    /// <param name="result">The Result to match on.</param>
    /// <param name="onSuccess">Async function to execute if successful.</param>
    /// <param name="onFailure">Async function to execute if failed, receiving the errors.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The result of either the success or failure function.</returns>
    /// <exception cref="ArgumentNullException">Thrown when onSuccess or onFailure is null.</exception>
    /// <example>
    /// <code>
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
    /// </code>
    /// </example>
    public static Task<TResult> MatchAsync<TResult>(
        this Result result,
        Func<CancellationToken, Task<TResult>> onSuccess,
        Func<IReadOnlyList<IResultError>, CancellationToken, Task<TResult>> onFailure,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        return result.IsSuccess
            ? onSuccess(cancellationToken)
            : onFailure(result.Errors, cancellationToken);
    }

    /// <summary>
    /// Executes an async success function with a synchronous failure handler.
    /// </summary>
    /// <typeparam name="TResult">The type of the return value.</typeparam>
    /// <param name="result">The Result to match on.</param>
    /// <param name="onSuccess">Async function to execute if successful.</param>
    /// <param name="onFailure">Synchronous function to execute if failed, receiving the errors.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The result of either the success or failure function.</returns>
    /// <exception cref="ArgumentNullException">Thrown when onSuccess or onFailure is null.</exception>
    /// <example>
    /// <code>
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
    /// </code>
    /// </example>
    public static Task<TResult> MatchAsync<TResult>(
        this Result result,
        Func<CancellationToken, Task<TResult>> onSuccess,
        Func<IReadOnlyList<IResultError>, TResult> onFailure,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        return result.IsSuccess
            ? onSuccess(cancellationToken)
            : Task.FromResult(onFailure(result.Errors));
    }

    /// <summary>
    /// Executes a synchronous success function with an async failure handler.
    /// </summary>
    /// <typeparam name="TResult">The type of the return value.</typeparam>
    /// <param name="result">The Result to match on.</param>
    /// <param name="onSuccess">Synchronous function to execute if successful.</param>
    /// <param name="onFailure">Async function to execute if failed, receiving the errors.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The result of either the success or failure function.</returns>
    /// <exception cref="ArgumentNullException">Thrown when onSuccess or onFailure is null.</exception>
    /// <example>
    /// <code>
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
    /// </code>
    /// </example>
    public static Task<TResult> MatchAsync<TResult>(
        this Result result,
        Func<TResult> onSuccess,
        Func<IReadOnlyList<IResultError>, CancellationToken, Task<TResult>> onFailure,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        return result.IsSuccess
            ? Task.FromResult(onSuccess())
            : onFailure(result.Errors, cancellationToken);
    }

    /// <summary>
    /// Executes different actions based on the Result's success state.
    /// </summary>
    /// <param name="result">The Result to handle.</param>
    /// <param name="onSuccess">Action to execute if the Result is successful.</param>
    /// <param name="onFailure">Action to execute if the Result failed, receiving the errors.</param>
    /// <returns>The original Result instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when onSuccess or onFailure is null.</exception>
    /// <example>
    /// <code>
    /// var result = Result.Success();
    ///
    /// result.Handle(
    ///     onSuccess: () => Console.WriteLine("Operation succeeded"),
    ///     onFailure: errors => Console.WriteLine($"Failed with {errors.Count} errors")
    /// );
    /// </code>
    /// </example>
    public static Result Handle(
        this Result result,
        Action onSuccess,
        Action<IReadOnlyList<IResultError>> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        if (result.IsSuccess)
        {
            onSuccess();
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
    /// <param name="onSuccess">Async function to execute if the Result is successful.</param>
    /// <param name="onFailure">Async function to execute if the Result failed, receiving the errors.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A Task containing the original Result instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when onSuccess or onFailure is null.</exception>
    /// <example>
    /// <code>
    /// var result = Result.Success();
    ///
    /// await result.HandleAsync(
    ///     async ct => await LogSuccessAsync(ct),
    ///     async (errors, ct) => await LogErrorsAsync(errors, ct),
    ///     cancellationToken
    /// );
    /// </code>
    /// </example>
    public static async Task<Result> HandleAsync(
        this Result result,
        Func<CancellationToken, Task> onSuccess,
        Func<IReadOnlyList<IResultError>, CancellationToken, Task> onFailure,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        if (result.IsSuccess)
        {
            await onSuccess(cancellationToken);
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
    /// <param name="onSuccess">Async function to execute if the Result is successful.</param>
    /// <param name="onFailure">Synchronous function to execute if the Result failed, receiving the errors.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A Task containing the original Result instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when onSuccess or onFailure is null.</exception>
    /// <example>
    /// <code>
    /// var result = Result.Success();
    ///
    /// await result.HandleAsync(
    ///     async ct => await LogSuccessAsync(ct),
    ///     errors => Console.WriteLine($"Failed with {errors.Count} errors"),
    ///     cancellationToken
    /// );
    /// </code>
    /// </example>
    public static async Task<Result> HandleAsync(
        this Result result,
        Func<CancellationToken, Task> onSuccess,
        Action<IReadOnlyList<IResultError>> onFailure,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        if (result.IsSuccess)
        {
            await onSuccess(cancellationToken);
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
    /// <param name="onSuccess">Synchronous function to execute if the Result is successful.</param>
    /// <param name="onFailure">Async function to execute if the Result failed, receiving the errors.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A Task containing the original Result instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when onSuccess or onFailure is null.</exception>
    /// <example>
    /// <code>
    /// var result = Result.Success();
    ///
    /// await result.HandleAsync(
    ///     () => Console.WriteLine("Operation succeeded"),
    ///     async (errors, ct) => await LogErrorsAsync(errors, ct),
    ///     cancellationToken
    /// );
    /// </code>
    /// </example>
    public static async Task<Result> HandleAsync(
        this Result result,
        Action onSuccess,
        Func<IReadOnlyList<IResultError>, CancellationToken, Task> onFailure,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        if (result.IsSuccess)
        {
            onSuccess();
            return result;
        }
        else
        {
            await onFailure(result.Errors, cancellationToken);
            return result;
        }
    }

    /// <summary>
    /// Ensures that a condition is met, converting to a failure if not.
    /// </summary>
    /// <param name="result">The Result to check.</param>
    /// <param name="predicate">The condition that must be true.</param>
    /// <param name="error">The error to return if the predicate fails.</param>
    /// <returns>The original result if successful and predicate is met; otherwise, a failure with the specified error.</returns>
    /// <example>
    /// <code>
    /// var result = Result.Success()
    ///     .Ensure(() => userCount > 0, new Error("No users found"));
    /// </code>
    /// </example>
    public static Result Ensure(this Result result, Func<bool> predicate, IResultError error)
    {
        if (!result.IsSuccess)
        {
            return result;
        }

        if (predicate is null)
        {
            return Result.Failure()
                .WithError(new Error("Predicate cannot be null"))
                .WithMessages(result.Messages);
        }

        try
        {
            return predicate()
                ? result
                : Result.Failure()
                    .WithError(error ?? new Error("Predicate condition not met"))
                    .WithMessages(result.Messages);
        }
        catch (Exception ex)
        {
            return Result.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(result.Messages);
        }
    }

    /// <summary>
    /// Ensures that an async condition is met, converting to a failure if not.
    /// </summary>
    /// <param name="result">The Result to check.</param>
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
    public static async Task<Result> EnsureAsync(
        this Result result,
        Func<CancellationToken, Task<bool>> predicate,
        IResultError error,
        CancellationToken cancellationToken = default)
    {
        if (!result.IsSuccess)
        {
            return result;
        }

        if (predicate is null)
        {
            return Result.Failure()
                .WithError(new Error("Predicate cannot be null"))
                .WithMessages(result.Messages);
        }

        try
        {
            return await predicate(cancellationToken)
                ? result
                : Result.Failure()
                    .WithError(error ?? new Error("Predicate condition not met"))
                    .WithMessages(result.Messages);
        }
        catch (OperationCanceledException)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError())
                .WithMessages(result.Messages);
        }
        catch (Exception ex)
        {
            return Result.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(result.Messages);
        }
    }

    /// <summary>
    /// Executes an action on a successful result without changing its value or state.
    /// Useful for performing side effects like logging or monitoring.
    /// </summary>
    /// <param name="result">The Result to tap into.</param>
    /// <param name="operation">The action to execute on success.</param>
    /// <returns>The original Result instance.</returns>
    /// <example>
    /// <code>
    /// var result = Result.Success()
    ///     .Tap(() => _logger.LogInformation("Operation successful"));
    /// </code>
    /// </example>
    public static Result Tap(this Result result, Action operation)
    {
        if (!result.IsSuccess || operation is null)
        {
            return result;
        }

        try
        {
            operation();
            return result;
        }
        catch (Exception ex)
        {
            return Result.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(result.Messages);
        }
    }

    /// <summary>
    /// Asynchronously executes an action on a successful result without changing its value or state.
    /// </summary>
    /// <param name="result">The Result to tap into.</param>
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
    public static async Task<Result> TapAsync(
        this Result result,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        if (!result.IsSuccess || operation is null)
        {
            return result;
        }

        try
        {
            await operation(cancellationToken);
            return result;
        }
        catch (OperationCanceledException)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError())
                .WithMessages(result.Messages);
        }
        catch (Exception ex)
        {
            return Result.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(result.Messages);
        }
    }

    public static Result Log(this Result result, ILogger logger, string message = null, LogLevel logLevel = LogLevel.Trace)
    {
        if (logger is null)
        {
            return Result.Failure()
                .WithError(new Error("Logger cannot be null"));
        }

        try
        {
            if (result.IsSuccess)
            {
                logger.Log(logLevel, $"{{LogKey}} {result.ToString(message)}", "RES");
            }
            else
            {
                logger.LogError($"{{LogKey}} {result.ToString(message)}", "RES");
            }

            return result;
        }
        catch (Exception ex)
        {
            return Result.Failure()
                .WithErrors(result.Errors)
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(result.Messages);
        }
    }

    /// <summary>
    /// Maps a success value while performing a side effect.
    /// </summary>
    /// <param name="result">The Result to map.</param>
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
    public static Result TeeMap(
        this Result result,
        Action onSuccess,
        Action<IReadOnlyList<IResultError>> onFailure)
    {
        try
        {
            if (result.IsSuccess)
            {
                onSuccess?.Invoke();
            }
            else
            {
                onFailure?.Invoke(result.Errors);
            }
            return result;
        }
        catch (Exception ex)
        {
            return Result.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(result.Messages);
        }
    }

    /// <summary>
    /// Asynchronously maps a success value while performing a side effect.
    /// </summary>
    /// <param name="result">The Result to map.</param>
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
    public static async Task<Result> TeeMapAsync(
        this Result result,
        Func<CancellationToken, Task> onSuccess,
        Func<IReadOnlyList<IResultError>, CancellationToken, Task> onFailure,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (result.IsSuccess)
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
                    await onFailure(result.Errors, cancellationToken);
                }
            }
            return result;
        }
        catch (OperationCanceledException)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError())
                .WithMessages(result.Messages);
        }
        catch (Exception ex)
        {
            return Result.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(result.Messages);
        }
    }

    /// <summary>
    /// Converts a successful result to a failure if the specified predicate is not met.
    /// </summary>
    /// <param name="result">The Result to filter.</param>
    /// <param name="predicate">The condition that must be true for the result to remain successful.</param>
    /// <param name="error">The error to return if the predicate fails.</param>
    /// <returns>The original result if successful and predicate is met; otherwise, a failure with the specified error.</returns>
    /// <example>
    /// <code>
    /// var result = Result.Success()
    ///     .Filter(() => isSystemReady, new Error("System not ready"));
    /// </code>
    /// </example>
    public static Result Filter(this Result result, Func<bool> predicate, IResultError error)
    {
        if (!result.IsSuccess || predicate is null)
        {
            return result;
        }

        try
        {
            return predicate()
                ? result
                : Result.Failure()
                    .WithError(error ?? new Error("Predicate condition not met"))
                    .WithMessages(result.Messages);
        }
        catch (Exception ex)
        {
            return Result.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(result.Messages);
        }
    }

    /// <summary>
    /// Asynchronously tests a condition and converts a successful result to a failure if the predicate is not met.
    /// </summary>
    /// <param name="result">The Result to filter.</param>
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
    public static async Task<Result> FilterAsync(
        this Result result,
        Func<CancellationToken, Task<bool>> predicate,
        IResultError error,
        CancellationToken cancellationToken = default)
    {
        if (!result.IsSuccess || predicate is null)
        {
            return result;
        }

        try
        {
            return await predicate(cancellationToken)
                ? result
                : Result.Failure()
                    .WithError(error ?? new Error("Predicate condition not met"))
                    .WithMessages(result.Messages);
        }
        catch (OperationCanceledException)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError())
                .WithMessages(result.Messages);
        }
        catch (Exception ex)
        {
            return Result.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(result.Messages);
        }
    }

    /// <summary>
    /// Converts a successful result to a failure if the specified predicate is met.
    /// </summary>
    /// <param name="result">The Result to check.</param>
    /// <param name="predicate">The condition that must be false for the result to remain successful.</param>
    /// <param name="error">The error to return if the predicate succeeds.</param>
    /// <returns>The original result if successful and predicate is not met; otherwise, a failure with the specified error.</returns>
    /// <example>
    /// <code>
    /// var result = Result.Success()
    ///     .Unless(() => systemIsDown, new Error("System is down"));
    /// </code>
    /// </example>
    public static Result Unless(this Result result, Func<bool> predicate, IResultError error)
    {
        if (!result.IsSuccess || predicate is null)
        {
            return result;
        }

        try
        {
            return predicate()
                ? Result.Failure()
                    .WithError(error ?? new Error("Unless condition met"))
                    .WithMessages(result.Messages)
                : result;
        }
        catch (Exception ex)
        {
            return Result.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(result.Messages);
        }
    }

    /// <summary>
    /// Asynchronously tests a condition and converts a successful result to a failure if the predicate is met.
    /// </summary>
    /// <param name="result">The Result to check.</param>
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
    public static async Task<Result> UnlessAsync(
        this Result result,
        Func<CancellationToken, Task<bool>> predicate,
        IResultError error,
        CancellationToken cancellationToken = default)
    {
        if (!result.IsSuccess || predicate is null)
        {
            return result;
        }

        try
        {
            return await predicate(cancellationToken)
                ? Result.Failure()
                    .WithError(error ?? new Error("Unless condition met"))
                    .WithMessages(result.Messages)
                : result;
        }
        catch (OperationCanceledException)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError())
                .WithMessages(result.Messages);
        }
        catch (Exception ex)
        {
            return Result.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(result.Messages);
        }
    }

    /// <summary>
    /// Chains a new operation if the current Result is successful.
    /// </summary>
    /// <param name="result">The Result to chain from.</param>
    /// <param name="operation">The operation to execute if successful.</param>
    /// <returns>The result of the next operation if successful; otherwise, a failure.</returns>
    /// <example>
    /// <code>
    /// var result = Result.Success()
    ///     .AndThen(() => ValidateUser(user))
    ///     .AndThen(() => SaveUser(user));
    /// </code>
    /// </example>
    public static Result AndThen(this Result result, Func<Result> operation)
    {
        if (!result.IsSuccess || operation is null)
        {
            return result;
        }

        try
        {
            return operation();
        }
        catch (Exception ex)
        {
            return Result.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(result.Messages);
        }
    }

    /// <summary>
    /// Chains a new async operation if the current Result is successful.
    /// </summary>
    /// <param name="result">The Result to chain from.</param>
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
    public static async Task<Result> AndThenAsync(
        this Result result,
        Func<CancellationToken, Task<Result>> operation,
        CancellationToken cancellationToken = default)
    {
        if (!result.IsSuccess || operation is null)
        {
            return result;
        }

        try
        {
            return await operation(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError())
                .WithMessages(result.Messages);
        }
        catch (Exception ex)
        {
            return Result.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(result.Messages);
        }
    }

    /// <summary>
    /// Provides a fallback result in case of failure.
    /// </summary>
    /// <param name="result">The Result to check.</param>
    /// <param name="fallback">The result to return if this result is a failure.</param>
    /// <returns>The original result if successful; otherwise, the fallback result.</returns>
    /// <example>
    /// <code>
    /// var result = Result.Failure("Primary failed")
    ///     .OrElse(() => GetBackupResult());
    /// </code>
    /// </example>
    public static Result OrElse(this Result result, Func<Result> fallback)
    {
        if (result.IsSuccess || fallback is null)
        {
            return result;
        }

        try
        {
            var fallbackResult = fallback();
            return fallbackResult
                .WithMessages(result.Messages);
        }
        catch (Exception ex)
        {
            return Result.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(result.Messages);
        }
    }

    /// <summary>
    /// Provides an async fallback result in case of failure.
    /// </summary>
    /// <param name="result">The Result to check.</param>
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
    public static async Task<Result> OrElseAsync(
        this Result result,
        Func<CancellationToken, Task<Result>> fallback,
        CancellationToken cancellationToken = default)
    {
        if (result.IsSuccess || fallback is null)
        {
            return result;
        }

        try
        {
            var fallbackResult = await fallback(cancellationToken);
            return fallbackResult
                .WithMessages(result.Messages);
        }
        catch (OperationCanceledException)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError())
                .WithMessages(result.Messages);
        }
        catch (Exception ex)
        {
            return Result.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(result.Messages);
        }
    }

    /// <summary>
    /// Executes an operation in the chain without transforming the value.
    /// </summary>
    /// <param name="result">The Result to execute the action on.</param>
    /// <param name="action">The action to execute.</param>
    /// <returns>The original Result.</returns>
    /// <example>
    /// <code>
    /// var result = Result.Success()
    ///     .Do(() => InitializeSystem(user))
    ///     .Then(user => ProcessUser(user));
    /// </code>
    /// </example>
    public static Result Do(this Result result, Action action)
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
            return Result.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(result.Messages);
        }
    }

    /// <summary>
    /// Executes an async operation in the chain without transforming the value.
    /// </summary>
    /// <param name="result">The Result to execute the action on.</param>
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
    public static async Task<Result> DoAsync(
        this Result result,
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
            return Result.Failure()
                .WithError(new OperationCancelledError())
                .WithMessages(result.Messages);
        }
        catch (Exception ex)
        {
            return Result.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(result.Messages);
        }
    }

    /// <summary>
    /// Executes an action only if a condition is met, without changing the Result.
    /// </summary>
    /// <param name="result">The Result to switch on.</param>
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
    public static Result Switch(this Result result, Func<bool> condition, Action action)
    {
        if (!result.IsSuccess || condition is null || action is null)
        {
            return result;
        }

        try
        {
            if (condition())
            {
                action();
            }
            return result;
        }
        catch (Exception ex)
        {
            return Result.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(result.Messages);
        }
    }

    /// <summary>
    /// Executes an async action only if a condition is met, without changing the Result.
    /// </summary>
    /// <param name="result">The Result to switch on.</param>
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
    public static async Task<Result> SwitchAsync(
        this Result result,
        Func<bool> condition,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        if (!result.IsSuccess || condition is null || action is null)
        {
            return result;
        }

        try
        {
            if (condition())
            {
                await action(cancellationToken);
            }
            return result;
        }
        catch (OperationCanceledException)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError())
                .WithMessages(result.Messages);
        }
        catch (Exception ex)
        {
            return Result.Failure()
                .WithError(Result.Settings.ExceptionErrorFactory(ex))
                .WithMessages(result.Messages);
        }
    }
}