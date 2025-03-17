namespace BridgingIT.DevKit.Common;

/// <summary>
/// Provides extension methods to enhance the functionality of the Result struct.
/// </summary>
public static class ResultExtensions
{
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
}