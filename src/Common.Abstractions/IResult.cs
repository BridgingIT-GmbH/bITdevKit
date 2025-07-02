// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
///     Represents the result of an operation, providing information about messages, errors, and success status.
/// </summary>
public interface IResult
{
    /// <summary>
    ///     Gets a read-only list of messages associated with the result.
    ///     These messages can provide additional context or information about the result.
    /// </summary>
    IReadOnlyList<string> Messages { get; }

    /// <summary>
    ///     Gets a read-only list of errors associated with the result.
    /// </summary>
    /// <remarks>
    ///     Errors provide detailed information about the issues encountered
    ///     during the operation which resulted in this result object.
    /// </remarks>
    IReadOnlyList<IResultError> Errors { get; }

    /// <summary>
    ///     Indicates whether the operation represented by the result was successful.
    ///     If true, the operation completed successfully.
    ///     If false, the operation encountered one or more errors.
    /// </summary>
    bool IsSuccess { get; }

    /// <summary>
    ///     Indicates whether the result of an operation represents a failure.
    /// </summary>
    /// <remarks>
    ///     This property returns the inverse of the <see cref="IsSuccess" /> property.
    ///     If <see cref="IsSuccess" /> is false, this property will return true, indicating a failure.
    /// </remarks>
    bool IsFailure { get; }

    /// <summary>
    ///     Determines whether the result contains any errors.
    /// </summary>
    /// <returns>True if the result contains any errors; otherwise, false.</returns>
    bool HasError();

    /// <summary>
    ///     Determines whether the result contains any specific errors.
    /// </summary>
    /// <returns>
    ///     A boolean value indicating whether any errors are present in the result.
    /// </returns>
    bool HasError<TError>()
        where TError : class, IResultError;

    /// <summary>
    ///     Checks if the result contains any specific error.
    /// </summary>
    /// <param name="error">An output parameter that will contain all the errors found in the result if any exist.</param>
    /// <typeparam name="TError">The type of error to check for, which must implement IResultError.</typeparam>
    /// <returns>True if the result contains one error of the specified type; otherwise, false.</returns>
    bool TryGetError<TError>(out TError error)
        where TError : class, IResultError;

    /// <summary>
    ///     Checks if the result contains any specific errors.
    /// </summary>
    /// <param name="errors">An output parameter that will contain all the errors found in the result if any exist.</param>
    /// <typeparam name="TError">The type of error to check for, which must implement IResultError.</typeparam>
    /// <returns>True if the result contains one or more errors of the specified type; otherwise, false.</returns>
    bool TryGetErrors<TError>(out IEnumerable<TError> errors)
        where TError : class, IResultError;

    //IResult WithMessage(string message);

    //IResult WithMessages(IEnumerable<string> messages);

    //IResult WithError(IResultError error);

    //IResult WithError(Exception ex);

    //IResult WithErrors(IEnumerable<IResultError> errors);

    //IResult WithError<TError>()
    //    where TError : IResultError, new();
}

/// <summary>
/// Represents the result of an operation, encapsulating messages, errors and the success or failure state.
/// </summary>
public interface IResult<out T> : IResult
{
    /// <summary>
    /// Represents a read-only property that retrieves a value of type T. The value cannot be modified.
    /// </summary>
    T Value { get; }

    //IResult Unwrap();

    //IResult<TOutput> ToResult<TOutput>();

    //IResult<TOutput> ToResult<TOutput>(TOutput value);

    //new IResult<T> WithMessage(string message);

    //new IResult<T> WithMessages(IEnumerable<string> messages);

    //new IResult<T> WithError(IResultError error);

    //new IResult<T> WithError(Exception ex);

    //new IResult<T> WithErrors(IEnumerable<IResultError> errors);

    //new IResult<T> WithError<TError>()
    //    where TError : IResultError, new();
}

/// <summary>
/// Represents a paged result that can be enumerated. It extends a result interface to include pagination functionality.
/// </summary>
public interface IResultPaged<out T> : IResult
{
    /// <summary>
    /// Retrieves a collection of values of type T. The collection is enumerable, allowing iteration over its elements.
    /// </summary>
    IEnumerable<T> Value { get; }

    /// <summary>
    /// Gets the current page number (1-based).
    /// </summary>
    int CurrentPage { get; }

    /// <summary>
    /// Gets the total number of pages based on total count and page size.
    /// </summary>
    int TotalPages { get; }

    /// <summary>
    /// Gets the total count of items across all pages.
    /// </summary>
    long TotalCount { get; }

    /// <summary>
    /// Gets the number of items per page.
    /// </summary>
    int PageSize { get; }

    /// <summary>
    /// Gets a value indicating whether there is a previous page available.
    /// </summary>
    bool HasPreviousPage { get; }

    /// <summary>
    /// Gets a value indicating whether there is a next page available.
    /// </summary>
    bool HasNextPage { get; }

    //IResult Unwrap();

    //IResultPaged<TOutput> For<TOutput>();

    //IResultPaged<TOutput> For<TOutput>(IEnumerable<TOutput> values);

    //new IResultPaged<T> WithMessage(string message);

    //new IResultPaged<T> WithMessages(IEnumerable<string> messages);

    //new IResultPaged<T> WithError(IResultError error);

    //new IResultPaged<T> WithError(Exception ex);

    //new IResultPaged<T> WithErrors(IEnumerable<IResultError> errors);

    //new IResultPaged<T> WithError<TError>()
    //    where TError : IResultError, new();
}