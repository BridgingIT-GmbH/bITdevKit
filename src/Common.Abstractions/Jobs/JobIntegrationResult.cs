// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Provides a small result implementation for optional feature integrations that depend only on Common.Abstractions.
/// </summary>
public sealed class JobIntegrationResult : IResult
{
    private JobIntegrationResult(bool isSuccess, IReadOnlyList<string> messages, IReadOnlyList<IResultError> errors)
    {
        this.IsSuccess = isSuccess;
        this.Messages = messages ?? [];
        this.Errors = errors ?? [];
    }

    /// <summary>
    /// Gets the informational messages captured by the integration result.
    /// </summary>
    public IReadOnlyList<string> Messages { get; }

    /// <summary>
    /// Gets the errors captured by the integration result.
    /// </summary>
    public IReadOnlyList<IResultError> Errors { get; }

    /// <summary>
    /// Gets a value indicating whether the integration operation succeeded.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets a value indicating whether the integration operation failed.
    /// </summary>
    public bool IsFailure => !this.IsSuccess;

    /// <summary>
    /// Creates a successful integration result.
    /// </summary>
    /// <param name="messages">Optional informational messages.</param>
    /// <returns>A successful <see cref="JobIntegrationResult"/>.</returns>
    public static JobIntegrationResult Success(params string[] messages)
        => new(true, messages?.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray() ?? [], []);

    /// <summary>
    /// Creates a failed integration result with a single integration error.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <returns>A failed <see cref="JobIntegrationResult"/>.</returns>
    public static JobIntegrationResult Failure(string message)
        => new(false, [], [new JobIntegrationError(message)]);

    /// <summary>
    /// Creates an integration result from another result abstraction.
    /// </summary>
    /// <param name="result">The result to copy.</param>
    /// <returns>A <see cref="JobIntegrationResult"/> containing the source result state.</returns>
    public static JobIntegrationResult From(IResult result)
        => result?.IsSuccess == true
            ? new JobIntegrationResult(true, result.Messages, [])
            : new JobIntegrationResult(false, result?.Messages ?? [], result?.Errors ?? []);

    /// <summary>
    /// Determines whether the result contains any error.
    /// </summary>
    /// <returns><c>true</c> when at least one error is present; otherwise, <c>false</c>.</returns>
    public bool HasError() => this.Errors.Count > 0;

    /// <summary>
    /// Determines whether the result contains an error of the requested type.
    /// </summary>
    /// <typeparam name="TError">The error type to locate.</typeparam>
    /// <returns><c>true</c> when an error of type <typeparamref name="TError"/> is present; otherwise, <c>false</c>.</returns>
    public bool HasError<TError>()
        where TError : class, IResultError
        => this.Errors.OfType<TError>().Any();

    /// <summary>
    /// Tries to get the first error of the requested type.
    /// </summary>
    /// <typeparam name="TError">The error type to locate.</typeparam>
    /// <param name="error">The matching error when one is present; otherwise, <c>null</c>.</param>
    /// <returns><c>true</c> when an error of type <typeparamref name="TError"/> is present; otherwise, <c>false</c>.</returns>
    public bool TryGetError<TError>(out TError error)
        where TError : class, IResultError
    {
        error = this.Errors.OfType<TError>().FirstOrDefault();
        return error is not null;
    }

    /// <summary>
    /// Tries to get all errors of the requested type.
    /// </summary>
    /// <typeparam name="TError">The error type to locate.</typeparam>
    /// <param name="errors">The matching errors. The sequence is empty when no matching errors are present.</param>
    /// <returns><c>true</c> when at least one error of type <typeparamref name="TError"/> is present; otherwise, <c>false</c>.</returns>
    public bool TryGetErrors<TError>(out IEnumerable<TError> errors)
        where TError : class, IResultError
    {
        var matching = this.Errors.OfType<TError>().ToArray();
        errors = matching;
        return matching.Length > 0;
    }
}

/// <summary>
/// Represents an integration-level job error without requiring the Common.Results package.
/// </summary>
public sealed class JobIntegrationError(string message) : IResultError
{
    /// <summary>
    /// Gets additional structured error properties.
    /// </summary>
    public PropertyBag Properties { get; } = new();

    /// <summary>
    /// Gets the integration error message.
    /// </summary>
    public string Message { get; } = message;

    /// <summary>
    /// Throws this integration error as an <see cref="InvalidOperationException"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">Always thrown with the current <see cref="Message"/>.</exception>
    public void Throw() => throw new InvalidOperationException(this.Message);
}
