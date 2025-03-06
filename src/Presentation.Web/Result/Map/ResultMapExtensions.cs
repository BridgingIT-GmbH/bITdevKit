// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using IResult = Microsoft.AspNetCore.Http.IResult;

/// <summary>
/// Extension methods for mapping Result objects to HTTP responses.
/// </summary>
public static class ResultMapExtensions
{
    /// <summary>
    /// Maps a non-generic <see cref="Result"/> to HTTP results for operations returning no content.
    /// </summary>
    /// <param name="result">The <see cref="Result"/> to map.</param>
    /// <param name="logger">An optional logger for logging failure cases. If null, no logging occurs.</param>
    /// <returns>A <see cref="Results{NoContent, NotFound, UnauthorizedHttpResult, BadRequest, ProblemHttpResult}"/> representing the HTTP response.</returns>
    public static Results<NoContent, NotFound, UnauthorizedHttpResult, BadRequest, ProblemHttpResult> MapHttpNoContent(
        this Result result,
        ILogger logger = null)
    {
        return ResultMapHttpExtensions.MapNoContent(result, logger);
    }

    /// <summary>
    /// Maps a generic <see cref="Result{T}"/> to HTTP results for operations returning data.
    /// </summary>
    /// <typeparam name="T">The type of the result value, constrained to reference types (class).</typeparam>
    /// <param name="result">The <see cref="Result{T}"/> to map.</param>
    /// <param name="logger">An optional logger for logging failure cases. If null, no logging occurs.</param>
    /// <returns>A <see cref="Results{Ok{T}, NotFound, UnauthorizedHttpResult, BadRequest, ProblemHttpResult}"/> representing the HTTP response.</returns>
    public static Results<Ok<T>, NotFound, UnauthorizedHttpResult, BadRequest, ProblemHttpResult> MapHttpOk<T>(
        this Result<T> result,
        ILogger logger = null)
        where T : class
    {
        return ResultMapHttpExtensions.MapOk(result, logger);
    }

    /// <summary>
    /// Maps a generic <see cref="Result{T}"/> to HTTP results for operations returning data.
    /// </summary>
    /// <typeparam name="T">The type of the result value, constrained to reference types (class).</typeparam>
    /// <param name="result">The <see cref="Result{T}"/> to map.</param>
    /// <param name="logger">An optional logger for logging failure cases. If null, no logging occurs.</param>
    /// <returns>A <see cref="Results{Ok{T}, UnauthorizedHttpResult, BadRequest, ProblemHttpResult}"/> representing the HTTP response.</returns>
    public static Results<Ok<T>, UnauthorizedHttpResult, BadRequest, ProblemHttpResult> MapHttpOkAll<T>(
        this Result<T> result,
        ILogger logger = null)
        where T : class
    {
        return ResultMapHttpExtensions.MapOkAll(result, logger);
    }

    /// <summary>
    /// Maps a non-generic <see cref="Result"/> to HTTP results for operations returning data.
    /// </summary>
    /// <param name="result">The <see cref="Result"/> to map.</param>
    /// <param name="logger">An optional logger for logging failure cases. If null, no logging occurs.</param>
    /// <returns>A <see cref="Results{Ok, NotFound, UnauthorizedHttpResult, BadRequest, ProblemHttpResult}"/> representing the HTTP response.</returns>
    public static Results<Ok, NotFound, UnauthorizedHttpResult, BadRequest, ProblemHttpResult> MapHttpOk(
        this Result result,
        ILogger logger = null)
    {
        return ResultMapHttpExtensions.MapOk(result, logger);
    }

    /// <summary>
    /// Maps a generic <see cref="Result{T}"/> to HTTP results for create operations.
    /// </summary>
    /// <typeparam name="T">The type of the result value, constrained to reference types (class).</typeparam>
    /// <param name="result">The <see cref="Result{T}"/> to map.</param>
    /// <param name="uri">The URI of the newly created resource, used in the Created response.</param>
    /// <param name="logger">An optional logger for logging failure cases. If null, no logging occurs.</param>
    /// <returns>A <see cref="Results{Created{T}, UnauthorizedHttpResult, BadRequest, ProblemHttpResult}"/> representing the HTTP response.</returns>
    public static Results<Created<T>, UnauthorizedHttpResult, BadRequest, ProblemHttpResult> MapHttpCreated<T>(
        this Result<T> result,
        string uri,
        ILogger logger = null)
        where T : class
    {
        return ResultMapHttpExtensions.MapCreated(result, uri, logger);
    }

    /// <summary>
    /// Maps a generic <see cref="Result{T}"/> to HTTP results for create operations.
    /// </summary>
    /// <typeparam name="T">The type of the result value, constrained to reference types (class).</typeparam>
    /// <param name="result">The <see cref="Result{T}"/> to map.</param>
    /// <param name="uriFactory">A function that generates the URI based on the result value.</param>
    /// <param name="logger">An optional logger for logging failure cases. If null, no logging occurs.</param>
    /// <returns>A <see cref="Results{Created{T}, UnauthorizedHttpResult, BadRequest, ProblemHttpResult}"/> representing the HTTP response.</returns>
    public static Results<Created<T>, UnauthorizedHttpResult, BadRequest, ProblemHttpResult> MapHttpCreated<T>(
        this Result<T> result,
        Func<T, string> uriFactory,
        ILogger logger = null)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(uriFactory, nameof(uriFactory));

        if (result.IsSuccess)
        {
            var uri = uriFactory(result.Value);
            return ResultMapHttpExtensions.MapCreated(result, uri, logger);
        }

        // For failure cases, we don't need the URI, so we can use a dummy value
        // The switch expression in MapCreated will never use it for failure cases
        return ResultMapHttpExtensions.MapCreated(result, "/dummy-uri-never-used", logger);
    }

    /// <summary>
    /// Maps a <see cref="Result"/> to HTTP 202 Accepted response for long-running operations.
    /// </summary>
    /// <param name="result">The <see cref="Result"/> to map.</param>
    /// <param name="location">The location URI where the status of the operation can be monitored.</param>
    /// <param name="logger">An optional logger for logging failure cases. If null, no logging occurs.</param>
    /// <returns>A <see cref="Results{Accepted, UnauthorizedHttpResult, BadRequest, ProblemHttpResult}"/> representing the HTTP response.</returns>
    public static Results<Accepted, UnauthorizedHttpResult, BadRequest, ProblemHttpResult> MapHttpAccepted(
        this Result result,
        string location,
        ILogger logger = null)
    {
        return ResultMapHttpExtensions.MapAccepted(result, location, logger);
    }

    /// <summary>
    /// Maps a <see cref="Result{T}"/> to HTTP 202 Accepted response with a body value for long-running operations.
    /// </summary>
    /// <typeparam name="T">The type of the result value, constrained to reference types (class).</typeparam>
    /// <param name="result">The <see cref="Result{T}"/> to map.</param>
    /// <param name="location">The location URI where the status of the operation can be monitored.</param>
    /// <param name="logger">An optional logger for logging failure cases. If null, no logging occurs.</param>
    /// <returns>A <see cref="Results{Accepted{T}, UnauthorizedHttpResult, BadRequest, ProblemHttpResult}"/> representing the HTTP response.</returns>
    public static Results<Accepted<T>, UnauthorizedHttpResult, BadRequest, ProblemHttpResult> MapHttpAccepted<T>(
        this Result<T> result,
        string location,
        ILogger logger = null)
        where T : class
    {
        return ResultMapHttpExtensions.MapAccepted(result, location, logger);
    }

    /// <summary>
    /// Maps a <see cref="Result{T}"/> to HTTP 202 Accepted response with a body value for long-running operations,
    /// using a function to generate the location URI.
    /// </summary>
    /// <typeparam name="T">The type of the result value, constrained to reference types (class).</typeparam>
    /// <param name="result">The <see cref="Result{T}"/> to map.</param>
    /// <param name="locationFactory">A function that generates the location URI based on the result value.</param>
    /// <param name="logger">An optional logger for logging failure cases. If null, no logging occurs.</param>
    /// <returns>A <see cref="Results{Accepted{T}, UnauthorizedHttpResult, BadRequest, ProblemHttpResult}"/> representing the HTTP response.</returns>
    public static Results<Accepted<T>, UnauthorizedHttpResult, BadRequest, ProblemHttpResult> MapHttpAccepted<T>(
        this Result<T> result,
        Func<T, string> locationFactory,
        ILogger logger = null)
        where T : class
    {
        return ResultMapHttpExtensions.MapAccepted(result, locationFactory, logger);
    }

    /// <summary>
    /// Maps a <see cref="ResultPaged{T}"/> to HTTP results for operations returning paginated data.
    /// </summary>
    /// <typeparam name="T">The type of items in the paged collection.</typeparam>
    /// <param name="result">The <see cref="ResultPaged{T}"/> to map.</param>
    /// <param name="logger">An optional logger for logging failure cases. If null, no logging occurs.</param>
    /// <returns>A <see cref="Results{Ok{PagedResponse{T}}, UnauthorizedHttpResult, BadRequest, ProblemHttpResult}"/> representing the HTTP response.</returns>
    public static Results<Ok<PagedResponse<T>>, UnauthorizedHttpResult, BadRequest, ProblemHttpResult> MapHttpOkPaged<T>(
        this ResultPaged<T> result,
        ILogger logger = null)
        where T : class
    {
        return ResultMapHttpExtensions.MapOkPaged(result, logger);
    }

    /// <summary>
    /// Maps a <see cref="Result{FileContent}"/> to HTTP results for file download operations.
    /// </summary>
    /// <param name="result">The <see cref="Result{FileContent}"/> to map.</param>
    /// <param name="logger">An optional logger for logging failure cases. If null, no logging occurs.</param>
    /// <returns>A <see cref="Results{FileContentHttpResult, NotFound, UnauthorizedHttpResult, BadRequest, ProblemHttpResult}"/> representing the HTTP response.</returns>
    public static Results<FileContentHttpResult, NotFound, UnauthorizedHttpResult, BadRequest, ProblemHttpResult> MapHttpFile(
        this Result<FileContent> result,
        ILogger logger = null)
    {
        return ResultMapHttpExtensions.MapFile(result, logger);
    }

    /// <summary>
    /// Extension method to generate a file download result with a function that creates the file name.
    /// </summary>
    /// <param name="result">The Result containing file content information.</param>
    /// <param name="fileNameFactory">A function that generates a filename based on the file content.</param>
    /// <param name="logger">Optional logger for error cases.</param>
    /// <returns>HTTP result for file download.</returns>
    public static Results<FileContentHttpResult, NotFound, UnauthorizedHttpResult, BadRequest, ProblemHttpResult> MapHttpFile(
        this Result<FileContent> result,
        Func<FileContent, string> fileNameFactory,
        ILogger logger = null)
    {
        return ResultMapHttpExtensions.MapFile(result, fileNameFactory, logger);
    }

    /// <summary>
    /// Maps a generic <see cref="Result{T}"/> to HTTP results for operations.
    /// </summary>
    /// <typeparam name="TSuccess">The type of the successful HTTP result.</typeparam>
    /// <typeparam name="TNotFound">The type of the not found HTTP result.</typeparam>
    /// <typeparam name="TUnauthorized">The type of the unauthorized HTTP result.</typeparam>
    /// <typeparam name="TBadRequest">The type of the bad request HTTP result.</typeparam>
    /// <typeparam name="TProblem">The type of the problem HTTP result.</typeparam>
    /// <typeparam name="TValue">The type of the result value.</typeparam>
    /// <param name="result">The <see cref="Result{T}"/> to map.</param>
    /// <param name="successFunc">A function to generate the success HTTP result when the operation succeeds.</param>
    /// <param name="logger">An optional logger for logging failure cases. If null, no logging occurs.</param>
    /// <returns>A <see cref="Results{TSuccess, TNotFound, TUnauthorized, TBadRequest, TProblem}"/> representing the HTTP response.</returns>
    public static Results<TSuccess, TNotFound, TUnauthorized, TBadRequest, TProblem> MapHttp<TSuccess, TNotFound, TUnauthorized, TBadRequest, TProblem, TValue>(
        this Result<TValue> result,
        Func<TValue, TSuccess> successFunc,
        ILogger logger = null)
        where TSuccess : IResult
        where TNotFound : IResult
        where TUnauthorized : IResult
        where TBadRequest : IResult
        where TProblem : IResult
        where TValue : class
    {
        return ResultMapHttpExtensions.Map<TSuccess, TNotFound, TUnauthorized, TBadRequest, TProblem, TValue>(
            result, successFunc, logger);
    }

    /// <summary>
    /// Maps a non-generic <see cref="Result"/> to HTTP results for operations.
    /// </summary>
    /// <typeparam name="TSuccess">The type of the successful HTTP result.</typeparam>
    /// <typeparam name="TNotFound">The type of the not found HTTP result.</typeparam>
    /// <typeparam name="TUnauthorized">The type of the unauthorized HTTP result.</typeparam>
    /// <typeparam name="TBadRequest">The type of the bad request HTTP result.</typeparam>
    /// <typeparam name="TProblem">The type of the problem HTTP result.</typeparam>
    /// <param name="result">The <see cref="Result"/> to map.</param>
    /// <param name="successFunc">A function to generate the success HTTP result when the operation succeeds.</param>
    /// <param name="logger">An optional logger for logging failure cases. If null, no logging occurs.</param>
    /// <returns>A <see cref="Results{TSuccess, TNotFound, TUnauthorized, TBadRequest, TProblem}"/> representing the HTTP response.</returns>
    public static Results<TSuccess, TNotFound, TUnauthorized, TBadRequest, TProblem> MapHttp<TSuccess, TNotFound, TUnauthorized, TBadRequest, TProblem>(
        this Result result,
        Func<TSuccess> successFunc,
        ILogger logger = null)
        where TSuccess : IResult
        where TNotFound : IResult
        where TUnauthorized : IResult
        where TBadRequest : IResult
        where TProblem : IResult
    {
        return ResultMapHttpExtensions.Map<TSuccess, TNotFound, TUnauthorized, TBadRequest, TProblem>(
            result, successFunc, logger);
    }
}

/// <summary>
/// Represents a standardized response format for paginated data.
/// </summary>
/// <typeparam name="T">The type of items in the collection.</typeparam>
public class PagedResponse<T>
{
    /// <summary>
    /// Gets or sets the collection of items for the current page.
    /// </summary>
    public IEnumerable<T> Items { get; set; }

    /// <summary>
    /// Gets or sets the current page number (1-based).
    /// </summary>
    public int Page { get; set; }

    /// <summary>
    /// Gets or sets the number of items per page.
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// Gets or sets the total number of items across all pages.
    /// </summary>
    public long TotalCount { get; set; }

    /// <summary>
    /// Gets or sets the total number of pages.
    /// </summary>
    public int TotalPages { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether there is a previous page available.
    /// </summary>
    public bool HasPreviousPage { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether there is a next page available.
    /// </summary>
    public bool HasNextPage { get; set; }
}

/// <summary>
/// Represents file content information for download responses.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="FileContent"/> class.
/// </remarks>
/// <param name="content">The file content as a byte array.</param>
/// <param name="fileName">The suggested file name for download.</param>
/// <param name="contentType">The MIME type of the file content.</param>
/// <param name="enableRangeProcessing">Whether to enable range processing for partial downloads.</param>
/// <param name="lastModified">The last modified date of the file for caching.</param>
/// <param name="entityTag">The entity tag for caching.</param>
public class FileContent(
    byte[] content,
    string fileName,
    string contentType,
    bool enableRangeProcessing = false,
    DateTimeOffset? lastModified = null,
    EntityTagHeaderValue entityTag = null)
{
    /// <summary>
    /// Gets the file content as a byte array.
    /// </summary>
    public byte[] Content { get; } = content ?? throw new ArgumentNullException(nameof(content));

    /// <summary>
    /// Gets the suggested file name for download.
    /// </summary>
    public string FileName { get; } = fileName ?? throw new ArgumentNullException(nameof(fileName));

    /// <summary>
    /// Gets the MIME type of the file content.
    /// </summary>
    public string ContentType { get; } = contentType ?? "application/octet-stream";

    /// <summary>
    /// Gets a value indicating whether to enable range processing for partial downloads.
    /// </summary>
    public bool EnableRangeProcessing { get; } = enableRangeProcessing;

    /// <summary>
    /// Gets the last modified date of the file for caching.
    /// </summary>
    public DateTimeOffset? LastModified { get; } = lastModified;

    /// <summary>
    /// Gets the entity tag for caching.
    /// </summary>
    public EntityTagHeaderValue EntityTag { get; } = entityTag;
}