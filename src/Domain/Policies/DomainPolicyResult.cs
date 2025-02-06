// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

/// <summary>
/// Represents the result of a domain policy execution, providing information on whether the policy was successful or not,
/// along with any associated messages and errors.
/// </summary>
/// <typeparam name="T">The type of the value contained in the result.</typeparam>
[DebuggerDisplay("{IsSuccess ? \"✓\" : \"✗\"} {messages.Count}Msg {errors.Count}Err {FirstMessageOrError}")]
public struct DomainPolicyResult<T> : IResult<T>
{
    /// <summary>
    /// Holds the value associated with the result of the domain policy execution.
    /// </summary>
    /// <remarks>
    /// The <c>value</c> field contains the data that represents the outcome of a successful policy execution. It is
    /// only accessible if the policy result indicates success.
    /// </remarks>
    private readonly T value;

    /// <summary>
    /// Indicates whether the policy execution was successful or not.
    /// </summary>
    /// <remarks>
    /// The <c>success</c> field is a boolean that determines the overall outcome of the policy operation.
    /// It is used internally to decide if the policy execution ended with a successful state.
    /// </remarks>
    private readonly bool success;

    /// <summary>
    /// Holds the messages associated with the result of a domain policy execution.
    /// </summary>
    /// <remarks>
    /// The <c>Messages</c> property contains a list of messages that provide additional context or
    /// information about the outcome of the policy execution. This might include warning messages, informational
    /// messages, or any relevant details that accompany the result.
    /// </remarks>
    private readonly ValueList<string> messages;

    /// <summary>
    /// Holds the errors encountered during the execution of a domain policy.
    /// </summary>
    /// <remarks>
    /// The <c>errors</c> variable is a collection of <c>IResultError</c> instances, representing the errors generated
    /// during the execution of the domain policy. It is used internally to store and retrieve error information.
    /// </remarks>
    private readonly ValueList<IResultError> errors;

    /// <summary>
    /// Represents the outcome of a domain policy execution, including success status, result value,
    /// messages, and errors.
    /// </summary>
    private DomainPolicyResult(bool isSuccess, T value = default, ValueList<string> messages = default, ValueList<IResultError> errors = default)
    {
        this.success = isSuccess;
        this.value = value;
        this.messages = messages;
        this.errors = errors;
    }

    /// <summary>
    /// Gets the first message or the first error associated with the domain policy result.
    /// </summary>
    /// <remarks>
    /// The <c>FirstMessageOrError</c> property provides a quick summary of the first encountered message or error
    /// related to the domain policy execution. If no messages or errors are present, it returns an empty string.
    /// </remarks>
    private string FirstMessageOrError =>
        !this.messages.IsEmpty ? $" | {this.messages.AsEnumerable().First()}" :
        !this.errors.IsEmpty ? $" | {this.errors.AsEnumerable().First().GetType().Name}" :
        string.Empty;

    /// <summary>
    /// Indicates whether the domain policy execution was successful.
    /// </summary>
    /// <remarks>
    /// The <c>IsSuccess</c> property returns <c>true</c> if all the applied policies executed without
    /// errors; otherwise, it returns <c>false</c>. This property is useful to determine the overall
    /// success of the policy execution process.
    /// </remarks>
    public bool IsSuccess => this.success;

    /// <summary>
    /// Indicates whether the policy result is a failure.
    /// </summary>
    /// <remarks>
    /// The <c>IsFailure</c> property returns <c>true</c> if the policy execution did not succeed.
    /// This can be utilized to check the overall status of the applied policies, particularly
    /// when determining whether subsequent policy executions or actions should proceed.
    /// </remarks>
    public bool IsFailure => !this.success;

    /// <summary>
    /// Gets or sets the outcomes from the execution of various domain policies.
    /// </summary>
    /// <remarks>
    /// The <c>PolicyResults</c> property aggregates the results from applying multiple policies to a domain context,
    /// providing a means to access and analyze each policy's specific outcome.
    /// </remarks>
    public DomainPolicyResults<T> PolicyResults { get; set; } = new();

    /// <summary>
    /// Associates the given policy results with the current DomainPolicyResult instance.
    /// </summary>
    /// <param name="results">The DomainPolicyResults to be associated.</param>
    /// <returns>The current DomainPolicyResult instance with the specified policy results.</returns>
    public DomainPolicyResult<T> WithPolicyResults(DomainPolicyResults<T> results)
    {
        this.PolicyResults = results;

        return this;
    }

    /// <summary>
    /// Gets the value of the result if the operation was successful.
    /// </summary>
    /// <remarks>
    /// The <c>Value</c> property holds the result of an operation when it is successful. Attempts to access this
    /// property when the operation has failed will result in an <see cref="InvalidOperationException"/>.
    /// </remarks>
    public T Value => this.success
        ? this.value
        : default; //throw new InvalidOperationException("Cannot access Value of failed result");

    /// <summary>
    /// Gets a read-only list of messages generated during the execution of policies.
    /// </summary>
    /// <remarks>
    /// The <c>Messages</c> property holds any informational messages that were produced
    /// as a result of policy execution. This allows for inspection of what occurred
    /// during the process.
    /// </remarks>
    public IReadOnlyList<string> Messages =>
        this.messages.AsEnumerable().ToList().AsReadOnly();

    /// <summary>
    /// Gets a read-only list of errors that were encountered during the execution of the policy.
    /// </summary>
    /// <remarks>
    /// The <c>Errors</c> property provides access to the collection of errors that occurred while applying one or more
    /// policies. This list can be used to understand what went wrong and to inspect individual <c>IResultError</c>
    /// instances for more details.
    /// </remarks>
    public IReadOnlyList<IResultError> Errors =>
        this.errors.AsEnumerable().ToList().AsReadOnly();

    /// <summary>
    /// Determines if the current DomainPolicyResult instance contains an error of the specified type.
    /// </summary>
    /// <typeparam name="TError">The type of error to check for, which must implement IResultError.</typeparam>
    /// <returns>True if an error of the specified type is present; otherwise, false.</returns>
    public bool HasError<TError>()
        where TError : class, IResultError
    {
        var errorType = typeof(TError);

        return this.errors.AsEnumerable().Any(e => e.GetType() == errorType);
    }

    /// <summary>
    /// Checks if there are any errors present in the current result.
    /// </summary>
    /// <returns>True if there are errors, otherwise false.</returns>
    public bool HasError()
    {
        return !this.errors.IsEmpty;
    }

    /// <summary>
    /// Attempts to retrieve errors of a specified type from the current DomainPolicyResult.
    /// </summary>
    /// <typeparam name="TError">The type of errors to retrieve.</typeparam>
    /// <param name="errors">An output parameter that will contain the errors of the specified type, if any are found.</param>
    /// <returns>True if errors of the specified type are found; otherwise, false.</returns>
    public bool TryGetErrors<TError>(out IEnumerable<TError> errors)
        where TError : class, IResultError
    {
        var errorType = typeof(TError);
        errors = this.errors.AsEnumerable().Where(e => e.GetType() == errorType).Cast<TError>();

        return errors.Any();
    }

    /// <summary>
    /// Retrieves the first error of the specified type from the current instance.
    /// </summary>
    /// <typeparam name="TError">The type of the error to retrieve.</typeparam>
    /// <returns>The first error of the specified type if found; otherwise, null.</returns>
    public TError GetError<TError>()
        where TError : class, IResultError
    {
        var errorType = typeof(TError);

        return this.errors.AsEnumerable().FirstOrDefault(e => e.GetType() == errorType) as TError;
    }

    /// <summary>
    /// Attempts to retrieve the first error of the specified type from the current DomainPolicyResult instance.
    /// </summary>
    /// <typeparam name="TError">The type of error to retrieve.</typeparam>
    /// <param name="error">When this method returns, contains the error of the specified type if found; otherwise, the default value of the type.</param>
    /// <returns>
    /// <c>true</c> if an error of the specified type is found; otherwise, <c>false</c>.
    /// </returns>
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

    /// <summary>
    /// Retrieves all errors of a specified type contained in the DomainPolicyResult instance.
    /// </summary>
    /// <typeparam name="TError">The type of errors to be retrieved.</typeparam>
    /// <returns>An IEnumerable of errors that match the specified type.</returns>
    public IEnumerable<TError> GetErrors<TError>()
        where TError : class, IResultError
    {
        var errorType = typeof(TError);

        return this.errors.AsEnumerable().Where(e => e.GetType() == errorType).Cast<TError>();
    }

    /// <summary>
    /// Creates a successful DomainPolicyResult instance with a default value.
    /// </summary>
    /// <returns>A successful DomainPolicyResult instance.</returns>
    public static DomainPolicyResult<T> Success()
    {
        return new DomainPolicyResult<T>(true);
    }

    /// <summary>
    /// Creates a successful DomainPolicyResult instance with the specified value.
    /// </summary>
    /// <param name="value">The value to be associated with the successful result.</param>
    /// <returns>A DomainPolicyResult instance indicating success.</returns>
    public static DomainPolicyResult<T> Success(T value)
    {
        return new DomainPolicyResult<T>(true, value);
    }

    /// <summary>
    /// Creates a failed DomainPolicyResult for a policy execution.
    /// </summary>
    /// <returns>A failed instance of DomainPolicyResult.</returns>
    public static DomainPolicyResult<T> Failure()
    {
        return new DomainPolicyResult<T>(false);
    }

    /// <summary>
    /// Adds a message to the current DomainPolicyResult instance.
    /// </summary>
    /// <param name="message">The message to be added to the result.</param>
    /// <returns>The current DomainPolicyResult instance with the specified message added.</returns>
    public DomainPolicyResult<T> WithMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return this;
        }

        return new DomainPolicyResult<T>(this.success, this.value, this.messages.Add(message), this.errors);
    }

    /// <summary>
    /// Adds the specified messages to the current DomainPolicyResult instance.
    /// </summary>
    /// <param name="messages">The messages to be added.</param>
    /// <returns>The current DomainPolicyResult instance with the specified messages added.</returns>
    public DomainPolicyResult<T> WithMessages(IEnumerable<string> messages)
    {
        if (messages is null)
        {
            return this;
        }

        return new DomainPolicyResult<T>(this.success, this.value, this.messages.AddRange(messages), this.errors);
    }

    /// <summary>
    /// Adds the specified error to the current DomainPolicyResult instance and marks the result as unsuccessful.
    /// </summary>
    /// <param name="error">The IResultError to be added.</param>
    /// <returns>The updated DomainPolicyResult instance with the added error.</returns>
    public DomainPolicyResult<T> WithError(IResultError error)
    {
        if (error is null)
        {
            return this;
        }

        return new DomainPolicyResult<T>(false, this.value, this.messages, this.errors.Add(error));
    }

    /// <summary>
    /// Adds the provided errors to the current DomainPolicyResult instance.
    /// </summary>
    /// <param name="errors">The list of errors to be added.</param>
    /// <returns>The updated DomainPolicyResult instance with the added errors.</returns>
    public DomainPolicyResult<T> WithErrors(IEnumerable<IResultError> errors)
    {
        if (errors is null || !errors.Any())
        {
            return this;
        }

        return new DomainPolicyResult<T>(false, this.value, this.messages, this.errors.AddRange(errors));
    }
}