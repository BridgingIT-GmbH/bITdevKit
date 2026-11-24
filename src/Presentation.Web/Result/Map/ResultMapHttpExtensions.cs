// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using IResult = Microsoft.AspNetCore.Http.IResult;

/// <summary>
/// Represents result map http extensions.
/// </summary>
public static class ResultMapHttpExtensions
{
    /// <summary>
    /// Registers a custom error handler for a specific error type.
    /// </summary>
    /// <typeparam name="TError">The type of error to register a handler for.</typeparam>
    /// <param name="handler">A function that takes a logger and result and returns an IResult.</param>
    /// <remarks>
    /// If a handler is already registered for the error type, it will be replaced (last registration wins).
    /// This handler will be invoked before the default error handling logic when the Result contains an error of type TError.
    /// </remarks>
    /// <example>
    /// <code>
    /// ResultMapHttpExtensions.RegisterErrorHandler&lt;CustomBusinessError&gt;((logger, result) => {
    ///     logger?.LogWarning("Business rule violation: {Error}", result.ToString());
    ///     return TypedResults.Problem(
    ///         detail: result.ToString(),
    ///         statusCode: 422,
    ///         title: "Business Rule Violation",
    ///         type: "https://example.com/errors/business-rule");
    /// });
    /// </code>
    /// </example>
    public static void RegisterErrorHandler<TError>(Func<ILogger, Result, IResult> handler)
        where TError : IResultError
    {
        ResultMapErrorHandlerRegistry.RegisterHandler<TError>(handler);
    }

    /// <summary>
    /// Removes a custom error handler for a specific error type.
    /// </summary>
    /// <typeparam name="TError">The type of error to remove the handler for.</typeparam>
    /// <returns>true if the handler was removed; otherwise, false.</returns>
    public static bool RemoveErrorHandler<TError>()
        where TError : IResultError
    {
        return ResultMapErrorHandlerRegistry.RemoveHandler<TError>();
    }

    /// <summary>
    /// Clears all registered custom error handlers.
    /// </summary>
    public static void ClearErrorHandlers()
    {
        ResultMapErrorHandlerRegistry.ClearHandlers();
    }

    /// <summary>
    /// Checks if a custom handler is registered for the specified error type.
    /// </summary>
    /// <typeparam name="TError">The type of error to check.</typeparam>
    /// <returns>true if a handler is registered for the specified error type; otherwise, false.</returns>
    public static bool HasErrorHandlerFor<TError>()
        where TError : IResultError
    {
        return ResultMapErrorHandlerRegistry.HasHandlerFor<TError>();
    }

    /// <summary>
    /// Gets a list of all error types with registered handlers.
    /// </summary>
    /// <returns>An enumerable of all registered error types.</returns>
    public static IEnumerable<Type> GetRegisteredErrorTypes()
    {
        return ResultMapErrorHandlerRegistry.GetRegisteredErrorTypes();
    }

    /// <summary>
    /// Maps a generic <see cref="Result{T}"/> struct to HTTP results for operations,
    /// handling success, unauthorized, not found, bad request, validation, conflict, rule violations, and other error cases in a flexible manner.
    /// </summary>
    /// <typeparam name="TSuccess">The type of the successful HTTP result (e.g., <see cref="Ok{T}"/>, <see cref="NoContent"/>, <see cref="Created{T}"/>).</typeparam>
    /// <typeparam name="TNotFound">The type of the not found HTTP result (e.g., <see cref="NotFound"/>).</typeparam>
    /// <typeparam name="TUnauthorized">The type of the unauthorized HTTP result (e.g., <see cref="UnauthorizedHttpResult"/>).</typeparam>
    /// <typeparam name="TBadRequest">The HTTP 400 type, such as <see cref="BadRequest"/>, <c>BadRequest&lt;string&gt;</c>, or <c>BadRequest&lt;ProblemDetails&gt;</c>.</typeparam>
    /// <typeparam name="TProblem">The problem response type. Use <see cref="ProblemHttpResult"/> for error-specific statuses or <see cref="InternalServerError"/> for a native HTTP 500. Supply a mapper logger to log failures mapped to the native arm.</typeparam>
    /// <typeparam name="TValue">The type of the result value, constrained to reference types (class) if used. Defaults to <see cref="object"/> for non-generic cases.</typeparam>
    /// <param name="result">The <see cref="Result{T}"/> struct containing the operation outcome.</param>
    /// <param name="successFunc">A function to generate the success HTTP result when the operation succeeds. Must return <typeparamref name="TSuccess"/>.</param>
    /// <param name="logger">An optional logger that logs failures during mapping and takes precedence over shared HTTP Result logging.</param>
    /// <returns>A <see cref="Results{TSuccess, TNotFound, TUnauthorized, TBadRequest, TProblem}"/> representing the HTTP response.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="result"/> or <paramref name="successFunc"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the result value is null for a reference type in <see cref="Result{T}"/>.</exception>
    /// <example>
    /// Usage for a generic result (e.g., GET operation):
    /// <code>
    /// var response = await mediator.Send(new AssetFindOneQuery(id), cancellationToken);
    /// return MapResult{Ok{Asset}, NotFound, UnauthorizedHttpResult, BadRequest, ProblemHttpResult, Asset}(
    ///     response.Result,
    ///     value => TypedResults.Ok(value),
    ///     logger,
    ///     cancellationToken);
    /// </code>
    /// Usage for a non-generic result (e.g., DELETE operation):
    /// <code>
    /// var response = await mediator.Send(new AssetDeleteCommand(id), cancellationToken);
    /// return MapResult{NoContent, NotFound, UnauthorizedHttpResult, BadRequest, ProblemHttpResult, object}(
    ///     response.Result,
    ///     _ => TypedResults.NoContent(),
    ///     logger,
    ///     cancellationToken);
    /// </code>
    /// Usage for a create operation:
    /// <code>
    /// var response = await mediator.Send(new AssetCreateCommand(asset), cancellationToken);
    /// return MapResult{Created{Asset}, NotFound, UnauthorizedHttpResult, BadRequest, ProblemHttpResult, Asset}(
    ///     response.Result,
    ///     value => TypedResults.Created($"/api/core/assets/{value.Id}", value),
    ///     logger,
    ///     cancellationToken);
    /// </code>
    /// Example response for success: HTTP 200 OK, 204 No Content, or 201 Created (depending on <typeparamref name="TSuccess"/>).
    /// Example response for unauthorized: HTTP 401 Unauthorized.
    /// Example response for not found: HTTP 404 Not Found.
    /// Example response for bad request: HTTP 400 Bad Request.
    /// Example response for other errors: HTTP 400 Problem with details.
    /// </example>
    public static Results<TSuccess, TNotFound, TUnauthorized, TBadRequest, TProblem> Map<TSuccess, TNotFound, TUnauthorized, TBadRequest, TProblem, TValue>(
        Result<TValue> result,
        Func<TValue, TSuccess> successFunc,
        ILogger logger = null)
        where TSuccess : IResult
        where TNotFound : IResult
        where TUnauthorized : IResult
        where TBadRequest : IResult
        where TProblem : IResult
        where TValue : class
    {
        ArgumentNullException.ThrowIfNull(successFunc);

        // Check for custom handlers first
        if (TryMapCustomError(result, logger, out var customResult))
        {
            switch (customResult)
            {
                case TSuccess successResult:
                    return successResult;
                case TNotFound notFoundResult:
                    return notFoundResult;
                case TUnauthorized unauthorizedResult:
                    return unauthorizedResult;
                case TBadRequest badRequestResult:
                    return badRequestResult;
                case TProblem problemResult:
                    return problemResult;
                default:
                    // Fallback to MapError<TProblem> for unrecognized custom results
                    logger?.LogInformation("Custom error handler returned an unrecognized type '{CustomResultType}'. Mapping to TProblem.", customResult.GetType().Name);
                    return MapError<TProblem>(logger, result);
            }
        }

        return result switch
        {
            { IsSuccess: true } => successFunc(result.Value),
            { IsFailure: true, Errors: var errors } when errors.Has<ForbiddenError>() => MapStatusProblem<TProblem>(logger, result, StatusCodes.Status403Forbidden),
            { IsFailure: true, Errors: var errors } when errors.Has<UnauthorizedError>() => MapUnauthorizedError<TUnauthorized>(logger, result),
            { IsFailure: true, Errors: var errors } when errors.Has<EntityNotFoundError>() || errors.Has<NotFoundError>() => MapNotFoundError<TNotFound>(logger, result),
            { IsFailure: true, Errors: var errors } when errors.Has<ValidationError>() || errors.Has<FluentValidationError>() || errors.Has<CollectionValidationError>() =>
                typeof(TProblem).IsAssignableFrom(typeof(ProblemHttpResult))
                    ? MapBadRequestError<TProblem>(logger, result)
                    : MapBadRequestError<TBadRequest>(logger, result),
            _ => MapFailureProblem<TProblem>(logger, result)
        };
    }

    /// <summary>
    /// Maps a non-generic <see cref="Result"/> struct to HTTP results for operations,
    /// handling success, unauthorized, not found, bad request, validation, conflict, rule violations, and other error cases in a flexible manner.
    /// </summary>
    /// <typeparam name="TSuccess">The type of the successful HTTP result (e.g., <see cref="Ok{T}"/>, <see cref="NoContent"/>, <see cref="Created{T}"/>).</typeparam>
    /// <typeparam name="TNotFound">The type of the not found HTTP result (e.g., <see cref="NotFound"/>).</typeparam>
    /// <typeparam name="TUnauthorized">The type of the unauthorized HTTP result (e.g., <see cref="UnauthorizedHttpResult"/>).</typeparam>
    /// <typeparam name="TBadRequest">The type of the bad request HTTP result (e.g., <see cref="BadRequest"/>).</typeparam>
    /// <typeparam name="TProblem">The problem response type. Use <see cref="ProblemHttpResult"/> for error-specific statuses or <see cref="InternalServerError"/> for a native HTTP 500. Supply a mapper logger to log failures mapped to the native arm.</typeparam>
    /// <param name="result">The <see cref="Result"/> struct containing the operation outcome.</param>
    /// <param name="successFunc">A function to generate the success HTTP result when the operation succeeds. Must return <typeparamref name="TSuccess"/>.</param>
    /// <param name="logger">An optional logger that logs failures during mapping and takes precedence over shared HTTP Result logging.</param>
    /// <returns>A <see cref="Results{TSuccess, TNotFound, TUnauthorized, TBadRequest, TProblem}"/> representing the HTTP response.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="result"/> or <paramref name="successFunc"/> is null.</exception>
    /// <example>
    /// Usage for a generic result (e.g., GET operation):
    /// <code>
    /// var response = await mediator.Send(new AssetFindOneQuery(id), cancellationToken);
    /// return MapResult{Ok, NotFound, UnauthorizedHttpResult, BadRequest, ProblemHttpResult}(
    ///     response.Result,
    ///     logger,
    ///     cancellationToken);
    /// </code>
    /// Usage for a non-generic result (e.g., DELETE operation):
    /// <code>
    /// var response = await mediator.Send(new AssetDeleteCommand(id), cancellationToken);
    /// return MapResult{NoContent, NotFound, UnauthorizedHttpResult, BadRequest, ProblemHttpResult}(
    ///     response.Result,
    ///     _ => TypedResults.NoContent(),
    ///     logger,
    ///     cancellationToken);
    /// </code>
    /// Usage for a create operation:
    /// <code>
    /// var response = await mediator.Send(new AssetCreateCommand(asset), cancellationToken);
    /// return MapResult{Created{Asset}, NotFound, UnauthorizedHttpResult, BadRequest, ProblemHttpResult}(
    ///     response.Result,
    ///     value => TypedResults.Created($"/api/core/assets/{value.Id}", value),
    ///     logger,
    ///     cancellationToken);
    /// </code>
    /// Example response for success: HTTP 200 OK, 204 No Content, or 201 Created (depending on <typeparamref name="TSuccess"/>).
    /// Example response for unauthorized: HTTP 401 Unauthorized.
    /// Example response for not found: HTTP 404 Not Found.
    /// Example response for bad request: HTTP 400 Bad Request.
    /// Example response for other errors: HTTP 400 Problem with details.
    /// </example>
    public static Results<TSuccess, TNotFound, TUnauthorized, TBadRequest, TProblem> Map<TSuccess, TNotFound, TUnauthorized, TBadRequest, TProblem>(
        Result result,
        Func<TSuccess> successFunc,
        ILogger logger = null)
        where TSuccess : IResult
        where TNotFound : IResult
        where TUnauthorized : IResult
        where TBadRequest : IResult
        where TProblem : IResult
    {
        ArgumentNullException.ThrowIfNull(successFunc);

        // Check for custom handlers first
        if (TryMapCustomError(result, logger, out var customResult))
        {
            switch (customResult)
            {
                case TSuccess successResult:
                    return successResult;
                case TNotFound notFoundResult:
                    return notFoundResult;
                case TUnauthorized unauthorizedResult:
                    return unauthorizedResult;
                case TBadRequest badRequestResult:
                    return badRequestResult;
                case TProblem problemResult:
                    return problemResult;
                default:
                    // Fallback to MapError<TProblem> for unrecognized custom results
                    logger?.LogInformation("Custom error handler returned an unrecognized type '{CustomResultType}'. Mapping to TProblem.", customResult.GetType().Name);
                    return MapError<TProblem>(logger, result);
            }
        }

        return result switch
        {
            { IsSuccess: true } => successFunc(),
            { IsFailure: true, Errors: var errors } when errors.Has<ForbiddenError>() => MapStatusProblem<TProblem>(logger, result, StatusCodes.Status403Forbidden),
            { IsFailure: true, Errors: var errors } when errors.Has<UnauthorizedError>() => MapUnauthorizedError<TUnauthorized>(logger, result),
            { IsFailure: true, Errors: var errors } when errors.Has<EntityNotFoundError>() || errors.Has<NotFoundError>() => MapNotFoundError<TNotFound>(logger, result),
            { IsFailure: true, Errors: var errors } when errors.Has<ValidationError>() || errors.Has<FluentValidationError>() || errors.Has<CollectionValidationError>() =>
                typeof(TProblem).IsAssignableFrom(typeof(ProblemHttpResult))
                    ? MapBadRequestError<TProblem>(logger, result)
                    : MapBadRequestError<TBadRequest>(logger, result),
            _ => MapFailureProblem<TProblem>(logger, result)
        };
    }

    /// <summary>
    /// Maps a non-generic <see cref="Result"/> struct to HTTP results for operations,
    /// handling success, unauthorized, not found, bad request, validation, conflict, rule violations, and other error cases in a flexible manner.
    /// </summary>
    /// <typeparam name="TSuccess">The type of the successful HTTP result (e.g., <see cref="Ok{T}"/>, <see cref="NoContent"/>, <see cref="Created{T}"/>).</typeparam>
    /// <typeparam name="TUnauthorized">The type of the unauthorized HTTP result (e.g., <see cref="UnauthorizedHttpResult"/>).</typeparam>
    /// <typeparam name="TBadRequest">The type of the bad request HTTP result (e.g., <see cref="BadRequest"/>).</typeparam>
    /// <typeparam name="TProblem">The problem response type. Use <see cref="ProblemHttpResult"/> for error-specific statuses or <see cref="InternalServerError"/> for a native HTTP 500. Supply a mapper logger to log failures mapped to the native arm.</typeparam>
    /// <param name="result">The <see cref="Result"/> struct containing the operation outcome.</param>
    /// <param name="successFunc">A function to generate the success HTTP result when the operation succeeds. Must return <typeparamref name="TSuccess"/>.</param>
    /// <param name="logger">An optional logger that logs failures during mapping and takes precedence over shared HTTP Result logging.</param>
    /// <returns>A <see cref="Results{TSuccess, TNotFound, TUnauthorized, TBadRequest, TProblem}"/> representing the HTTP response.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="result"/> or <paramref name="successFunc"/> is null.</exception>
    /// <example>
    /// Usage for a generic result (e.g., GET operation):
    /// <code>
    /// var response = await mediator.Send(new AssetFindOneQuery(id), cancellationToken);
    /// return MapResult{Ok, NotFound, UnauthorizedHttpResult, BadRequest, ProblemHttpResult}(
    ///     response.Result,
    ///     logger,
    ///     cancellationToken);
    /// </code>
    /// Usage for a non-generic result (e.g., DELETE operation):
    /// <code>
    /// var response = await mediator.Send(new AssetDeleteCommand(id), cancellationToken);
    /// return MapResult{NoContent, NotFound, UnauthorizedHttpResult, BadRequest, ProblemHttpResult}(
    ///     response.Result,
    ///     _ => TypedResults.NoContent(),
    ///     logger,
    ///     cancellationToken);
    /// </code>
    /// Usage for a create operation:
    /// <code>
    /// var response = await mediator.Send(new AssetCreateCommand(asset), cancellationToken);
    /// return MapResult{Created{Asset}, NotFound, UnauthorizedHttpResult, BadRequest, ProblemHttpResult}(
    ///     response.Result,
    ///     value => TypedResults.Created($"/api/core/assets/{value.Id}", value),
    ///     logger,
    ///     cancellationToken);
    /// </code>
    /// Example response for success: HTTP 200 OK, 204 No Content, or 201 Created (depending on <typeparamref name="TSuccess"/>).
    /// Example response for unauthorized: HTTP 401 Unauthorized.
    /// Example response for not found: HTTP 404 Not Found.
    /// Example response for bad request: HTTP 400 Bad Request.
    /// Example response for other errors: HTTP 400 Problem with details.
    /// </example>
    public static Results<TSuccess, TUnauthorized, TBadRequest, TProblem> Map<TSuccess, TUnauthorized, TBadRequest, TProblem>(
        Result result,
        Func<TSuccess> successFunc,
        ILogger logger = null)
        where TSuccess : IResult
        where TUnauthorized : IResult
        where TBadRequest : IResult
        where TProblem : IResult
    {
        ArgumentNullException.ThrowIfNull(successFunc);

        // Check for custom handlers first
        if (TryMapCustomError(result, logger, out var customResult))
        {
            switch (customResult)
            {
                case TSuccess successResult:
                    return successResult;
                case TUnauthorized unauthorizedResult:
                    return unauthorizedResult;
                case TBadRequest badRequestResult:
                    return badRequestResult;
                case TProblem problemResult:
                    return problemResult;
                default:
                    // Fallback to MapError<TProblem> for unrecognized custom results
                    logger?.LogInformation("Custom error handler returned an unrecognized type '{CustomResultType}'. Mapping to TProblem.", customResult.GetType().Name);
                    return MapError<TProblem>(logger, result);
            }
        }

        return result switch
        {
            { IsSuccess: true } => successFunc(),
            { IsFailure: true, Errors: var errors } when errors.Has<ForbiddenError>() => MapStatusProblem<TProblem>(logger, result, StatusCodes.Status403Forbidden),
            { IsFailure: true, Errors: var errors } when errors.Has<UnauthorizedError>() => MapUnauthorizedError<TUnauthorized>(logger, result),
            { IsFailure: true, Errors: var errors } when errors.Has<EntityNotFoundError>() || errors.Has<NotFoundError>() => MapStatusProblem<TProblem>(logger, result, StatusCodes.Status404NotFound),
            { IsFailure: true, Errors: var errors } when errors.Has<ValidationError>() || errors.Has<FluentValidationError>() || errors.Has<CollectionValidationError>() =>
                typeof(TProblem).IsAssignableFrom(typeof(ProblemHttpResult))
                    ? MapBadRequestError<TProblem>(logger, result)
                    : MapBadRequestError<TBadRequest>(logger, result),
            _ => MapFailureProblem<TProblem>(logger, result)
        };
    }

    /// <summary>
    /// Maps a non-generic <see cref="Result"/> struct to HTTP results for operations returning no content,
    /// such as DELETE operations. Handles success, unauthorized, not found, bad request, and other error cases.
    /// </summary>
    /// <param name="result">The <see cref="Result"/> struct containing the operation outcome.</param>
    /// <param name="logger">An optional logger that logs failures during mapping and takes precedence over shared HTTP Result logging.</param>
    /// <returns>A <see cref="Results{NoContent, NotFound, UnauthorizedHttpResult, BadRequest, ProblemHttpResult}"/> representing the HTTP response.</returns>
    /// <example>
    /// Usage in an endpoint:
    /// <code>
    /// var response = await mediator.Send(new AssetDeleteCommand(id), cancellationToken);
    /// return MapNoContent(response.Result, logger, cancellationToken);
    /// </code>
    /// Example response for success: HTTP 204 No Content.
    /// Example response for unauthorized: HTTP 401 Unauthorized.
    /// Example response for not found: HTTP 404 Not Found.
    /// Example response for bad request: HTTP 400 Bad Request.
    /// Example response for other errors: HTTP 400 Problem with details.
    /// </example>
    public static Results<NoContent, NotFound, UnauthorizedHttpResult, BadRequest, ProblemHttpResult> MapNoContent(
        Result result,
        ILogger logger = null)
    {
        // Check for custom handlers first
        if (TryMapCustomError(result, logger, out var customResult))
        {
            switch (customResult)
            {
                case NoContent noContentResult:
                    return noContentResult;
                case NotFound notFoundResult:
                    return notFoundResult;
                case UnauthorizedHttpResult unauthorizedResult:
                    return unauthorizedResult;
                case BadRequest badRequestResult:
                    return badRequestResult;
                case ProblemHttpResult problemResult:
                    return problemResult;
                default:
                    // Wrap unrecognized custom results directly in a ProblemHttpResult
                    logger?.LogInformation("Custom error handler returned an unrecognized type. Wrapping in ProblemHttpResult.");
                    return WrapCustomResult(customResult, result, logger);
            }
        }

        return result switch
        {
            { IsSuccess: true } => TypedResults.NoContent(),
            _ => MapFailureProblem<ProblemHttpResult>(logger, result)
        };
    }

    /// <summary>
    /// Maps a generic <see cref="Result{T}"/> struct to HTTP results for operations returning data,
    /// such as GET or PUT operations. Handles success, unauthorized, not found, bad request, and other error cases.
    /// </summary>
    /// <typeparam name="T">The type of the result value, constrained to reference types (class).</typeparam>
    /// <param name="result">The <see cref="Result{T}"/> struct containing the operation outcome.</param>
    /// <param name="logger">An optional logger that logs failures during mapping and takes precedence over shared HTTP Result logging.</param>
    /// <returns>A typed HTTP result containing the value on success or the mapped failure response.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the result value is null for a reference type.</exception>
    /// <example>
    /// Usage in an endpoint:
    /// <code>
    /// var response = await mediator.Send(new AssetFindOneQuery(id), cancellationToken);
    /// return MapOk(response.Result, logger, cancellationToken);
    /// </code>
    /// Example response for success: HTTP 200 OK with the asset data.
    /// Example response for unauthorized: HTTP 401 Unauthorized.
    /// Example response for not found: HTTP 404 Not Found.
    /// Example response for bad request: HTTP 400 Bad Request.
    /// Example response for other errors: HTTP 400 Problem with details.
    /// </example>
    public static Results<Ok<T>, NotFound, UnauthorizedHttpResult, BadRequest, ProblemHttpResult> MapOk<T>(
        Result<T> result,
        ILogger logger = null)
        where T : class
    {
        //if (result.Value == null)
        //{
        //    throw new InvalidOperationException($"Result.Value is null for type {typeof(T).Name}");
        //}

        // Check for custom handlers first
        if (TryMapCustomError(result, logger, out var customResult))
        {
            switch (customResult)
            {
                case Ok<T> okResult:
                    return okResult;
                case NotFound notFoundResult:
                    return notFoundResult;
                case UnauthorizedHttpResult unauthorizedResult:
                    return unauthorizedResult;
                case BadRequest badRequestResult:
                    return badRequestResult;
                case ProblemHttpResult problemResult:
                    return problemResult;
                default:
                    // Wrap unrecognized custom results directly in a ProblemHttpResult
                    logger?.LogInformation("Custom error handler returned an unrecognized type. Wrapping in ProblemHttpResult.");
                    return WrapCustomResult(customResult, result, logger);
            }
        }

        return result switch
        {
            { IsSuccess: true } => TypedResults.Ok(result.Value),
            _ => MapFailureProblem<ProblemHttpResult>(logger, result)
        };
    }

    /// <summary>
    /// Maps a non generic <see cref="Result"/> struct to HTTP results for operations returning data,
    /// such as GET or PUT operations. Handles success, unauthorized, not found, bad request, and other error cases.
    /// </summary>
    /// <param name="result">The <see cref="Result"/> struct containing the operation outcome.</param>
    /// <param name="logger">An optional logger that logs failures during mapping and takes precedence over shared HTTP Result logging.</param>
    /// <returns>A <see cref="Results{Ok, NotFound, UnauthorizedHttpResult, BadRequest, ProblemHttpResult}"/> representing the HTTP response.</returns>
    /// <example>
    /// Usage in an endpoint:
    /// <code>
    /// var response = await mediator.Send(new AssetFindOneQuery(id), cancellationToken);
    /// return MapOk(response.Result, logger, cancellationToken);
    /// </code>
    /// Example response for success: HTTP 200 OK.
    /// Example response for unauthorized: HTTP 401 Unauthorized.
    /// Example response for not found: HTTP 404 Not Found.
    /// Example response for bad request: HTTP 400 Bad Request.
    /// Example response for other errors: HTTP 400 Problem with details.
    /// </example>
    public static Results<Ok, NotFound, UnauthorizedHttpResult, BadRequest, ProblemHttpResult> MapOk(
        Result result,
        ILogger logger = null)
    {
        // Check for custom handlers first
        if (TryMapCustomError(result, logger, out var customResult))
        {
            switch (customResult)
            {
                case Ok okResult:
                    return okResult;
                case NotFound notFoundResult:
                    return notFoundResult;
                case UnauthorizedHttpResult unauthorizedResult:
                    return unauthorizedResult;
                case BadRequest badRequestResult:
                    return badRequestResult;
                case ProblemHttpResult problemResult:
                    return problemResult;
                default:
                    // Wrap unrecognized custom results directly in a ProblemHttpResult
                    logger?.LogInformation("Custom error handler returned an unrecognized type. Wrapping in ProblemHttpResult.");
                    return WrapCustomResult(customResult, result, logger);
            }
        }

        return result switch
        {
            { IsSuccess: true } => TypedResults.Ok(),
            _ => MapFailureProblem<ProblemHttpResult>(logger, result)
        };
    }

    /// <summary>
    /// Maps a generic <see cref="Result{T}"/> struct to HTTP results for operations returning data,
    /// such as GET or PUT operations. Handles success, unauthorized, not found, bad request, and other error cases.
    /// </summary>
    /// <typeparam name="T">The type of the result value, constrained to reference types (class).</typeparam>
    /// <param name="result">The <see cref="Result{T}"/> struct containing the operation outcome.</param>
    /// <param name="logger">An optional logger that logs failures during mapping and takes precedence over shared HTTP Result logging.</param>
    /// <returns>A typed HTTP result containing the value on success or the mapped failure response.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the result value is null for a reference type.</exception>
    /// <example>
    /// Usage in an endpoint:
    /// <code>
    /// var response = await mediator.Send(new AssetFindOneQuery(id), cancellationToken);
    /// return MapOk(response.Result, logger, cancellationToken);
    /// </code>
    /// Example response for success: HTTP 200 OK with the asset data.
    /// Example response for unauthorized: HTTP 401 Unauthorized.
    /// Example response for not found: HTTP 404 Not Found.
    /// Example response for bad request: HTTP 400 Bad Request.
    /// Example response for other errors: HTTP 400 Problem with details.
    /// </example>
    public static Results<Ok<T>, UnauthorizedHttpResult, BadRequest, ProblemHttpResult> MapOkAll<T>(
        Result<T> result,
        ILogger logger = null)
        where T : class
    {
        //if (result.Value == null)
        //{
        //    throw new InvalidOperationException($"Result.Value is null for type {typeof(T).Name}");
        //}

        // Check for custom handlers first
        if (TryMapCustomError(result, logger, out var customResult))
        {
            switch (customResult)
            {
                case Ok<T> okResult:
                    return okResult;
                case UnauthorizedHttpResult unauthorizedResult:
                    return unauthorizedResult;
                case BadRequest badRequestResult:
                    return badRequestResult;
                case ProblemHttpResult problemResult:
                    return problemResult;
                default:
                    // Wrap unrecognized custom results directly in a ProblemHttpResult
                    logger?.LogInformation("Custom error handler returned an unrecognized type. Wrapping in ProblemHttpResult.");
                    return WrapCustomResult(customResult, result, logger);
            }
        }

        return result switch
        {
            { IsSuccess: true } => TypedResults.Ok(result.Value),
            _ => MapFailureProblem<ProblemHttpResult>(logger, result)
        };
    }

    /// <summary>
    /// Maps a generic <see cref="Result{T}"/> struct to HTTP results for create operations,
    /// such as POST operations. Handles success, unauthorized, not found, bad request, and other error cases.
    /// </summary>
    /// <typeparam name="T">The type of the result value, constrained to reference types (class).</typeparam>
    /// <param name="result">The <see cref="Result{T}"/> struct containing the operation outcome.</param>
    /// <param name="uri">The URI of the newly created resource, used in the Created response.</param>
    /// <param name="logger">An optional logger that logs failures during mapping and takes precedence over shared HTTP Result logging.</param>
    /// <returns>A typed HTTP result containing the created value on success or the mapped failure response.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="uri"/> is null or empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the result value is null for a reference type.</exception>
    /// <example>
    /// Usage in an endpoint:
    /// <code>
    /// var response = await mediator.Send(new AssetCreateCommand(asset), cancellationToken);
    /// return MapCreated(response.Result, $"/api/core/assets/{response.Result.Value.Id}", logger, cancellationToken);
    /// </code>
    /// Example response for success: HTTP 201 Created with the asset data and URI.
    /// Example response for unauthorized: HTTP 401 Unauthorized.
    /// Example response for not found: HTTP 404 Not Found.
    /// Example response for bad request: HTTP 400 Bad Request.
    /// Example response for other errors: HTTP 400 Problem with details.
    /// </example>
    public static Results<Created<T>, UnauthorizedHttpResult, BadRequest, ProblemHttpResult> MapCreated<T>(
        Result<T> result,
        string uri,
        ILogger logger = null)
        where T : class
    {
        if (string.IsNullOrWhiteSpace(uri))
        {
            throw new ArgumentException("URI cannot be null or empty", nameof(uri));
        }

        if (TryMapCustomError(result, logger, out var customResult))
        {
            switch (customResult)
            {
                case Created<T> createdResult:
                    return createdResult;
                case UnauthorizedHttpResult unauthorizedResult:
                    return unauthorizedResult;
                case BadRequest badRequestResult:
                    return badRequestResult;
                case ProblemHttpResult problemResult:
                    return problemResult;
                default:
                    // Wrap unrecognized custom results directly in a ProblemHttpResult
                    logger?.LogInformation("Custom error handler returned an unrecognized type. Wrapping in ProblemHttpResult.");
                    return WrapCustomResult(customResult, result, logger);
            }
        }

        return result switch
        {
            { IsSuccess: true } => TypedResults.Created(uri, result.Value),
            _ => MapFailureProblem<ProblemHttpResult>(logger, result)
        };
    }

    /// <summary>
    /// Maps a <see cref="Result"/> to HTTP 202 Accepted response for long-running operations.
    /// </summary>
    /// <param name="result">The <see cref="Result"/> to map.</param>
    /// <param name="location">The location URI where the status of the operation can be monitored.</param>
    /// <param name="logger">An optional logger that logs failures during mapping and takes precedence over shared HTTP Result logging.</param>
    /// <returns>A <see cref="Results{Accepted, UnauthorizedHttpResult, BadRequest, ProblemHttpResult}"/> representing the HTTP response.</returns>
    public static Results<Accepted, UnauthorizedHttpResult, BadRequest, ProblemHttpResult> MapAccepted(
        Result result,
        string location,
        ILogger logger = null)
    {
        ArgumentNullException.ThrowIfNull(location);

        // Check for custom handlers first
        if (TryMapCustomError(result, logger, out var customResult))
        {
            switch (customResult)
            {
                case Accepted accResult:
                    return accResult;
                case UnauthorizedHttpResult unauthorizedResult:
                    return unauthorizedResult;
                case BadRequest badRequestResult:
                    return badRequestResult;
                case ProblemHttpResult problemResult:
                    return problemResult;
                default:
                    // Wrap unrecognized custom results directly in a ProblemHttpResult
                    logger?.LogInformation("Custom error handler returned an unrecognized type. Wrapping in ProblemHttpResult.");
                    return WrapCustomResult(customResult, result, logger);
            }
        }

        return result switch
        {
            { IsSuccess: true } => TypedResults.Accepted(location),
            _ => MapFailureProblem<ProblemHttpResult>(logger, result)
        };
    }

    /// <summary>
    /// Maps a <see cref="Result{T}"/> to HTTP 202 Accepted response with a body value for long-running operations.
    /// </summary>
    /// <typeparam name="T">The type of the result value, constrained to reference types (class).</typeparam>
    /// <param name="result">The <see cref="Result{T}"/> to map.</param>
    /// <param name="location">The location URI where the status of the operation can be monitored.</param>
    /// <param name="logger">An optional logger that logs failures during mapping and takes precedence over shared HTTP Result logging.</param>
    /// <returns>A typed HTTP result containing the accepted value on success or the mapped failure response.</returns>
    public static Results<Accepted<T>, UnauthorizedHttpResult, BadRequest, ProblemHttpResult> MapAccepted<T>(
        this Result<T> result,
        string location,
        ILogger logger = null)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(location);

        if (result.Value == null)
        {
            throw new InvalidOperationException($"Result.Value is null for type {typeof(T).Name}");
        }

        // Check for custom handlers first
        if (TryMapCustomError(result, logger, out var customResult))
        {
            switch (customResult)
            {
                case Accepted<T> accResult:
                    return accResult;
                case UnauthorizedHttpResult unauthorizedResult:
                    return unauthorizedResult;
                case BadRequest badRequestResult:
                    return badRequestResult;
                case ProblemHttpResult problemResult:
                    return problemResult;
                default:
                    // Wrap unrecognized custom results directly in a ProblemHttpResult
                    logger?.LogInformation("Custom error handler returned an unrecognized type. Wrapping in ProblemHttpResult.");
                    return WrapCustomResult(customResult, result, logger);
            }
        }

        return result switch
        {
            { IsSuccess: true } => TypedResults.Accepted(location, result.Value),
            _ => MapFailureProblem<ProblemHttpResult>(logger, result)
        };
    }

    /// <summary>
    /// Maps a <see cref="Result{T}"/> to HTTP 202 Accepted response with a body value for long-running operations,
    /// using a function to generate the location URI.
    /// </summary>
    /// <typeparam name="T">The type of the result value, constrained to reference types (class).</typeparam>
    /// <param name="result">The <see cref="Result{T}"/> to map.</param>
    /// <param name="locationFactory">A function that generates the location URI based on the result value.</param>
    /// <param name="logger">An optional logger that logs failures during mapping and takes precedence over shared HTTP Result logging.</param>
    /// <returns>A typed HTTP result containing the accepted value on success or the mapped failure response.</returns>
    public static Results<Accepted<T>, UnauthorizedHttpResult, BadRequest, ProblemHttpResult> MapAccepted<T>(
        this Result<T> result,
        Func<T, string> locationFactory,
        ILogger logger = null)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(locationFactory);

        if (result.IsSuccess)
        {
            if (result.Value == null)
            {
                throw new InvalidOperationException($"Result.Value is null for type {typeof(T).Name}");
            }

            var location = locationFactory(result.Value);
            return MapAccepted(result, location, logger);
        }

        // For failure cases, we don't need the location, so we can use a dummy value
        // The switch expression in MapAccepted will never use it for failure cases
        return MapAccepted(result, "/dummy-location-never-used", logger);
    }

    /// <summary>
    /// Maps a <see cref="ResultPaged{T}"/> to HTTP results for operations returning paginated data.
    /// </summary>
    /// <typeparam name="T">The type of items in the paged collection.</typeparam>
    /// <param name="result">The <see cref="ResultPaged{T}"/> to map.</param>
    /// <param name="logger">An optional logger that logs failures during mapping and takes precedence over shared HTTP Result logging.</param>
    /// <returns>A typed HTTP result containing the paged response on success or the mapped failure response.</returns>
    public static Results<Ok<PagedResponse<T>>, UnauthorizedHttpResult, BadRequest, ProblemHttpResult> MapOkPaged<T>(
        ResultPaged<T> result,
        ILogger logger = null)
        where T : class
    {
        // Check for custom handlers first
        if (TryMapCustomError(result, logger, out var customResult))
        {
            switch (customResult)
            {
                case Ok<PagedResponse<T>> okResult:
                    return okResult;
                case UnauthorizedHttpResult unauthorizedResult:
                    return unauthorizedResult;
                case BadRequest badRequestResult:
                    return badRequestResult;
                case ProblemHttpResult problemResult:
                    return problemResult;
                default:
                    // Wrap unrecognized custom results directly in a ProblemHttpResult
                    logger?.LogInformation("Custom error handler returned an unrecognized type. Wrapping in ProblemHttpResult.");
                    return WrapCustomResult(customResult, result, logger);
            }
        }

        return result switch
        {
            { IsSuccess: true } => TypedResults.Ok(ToPagedResponse(result)),
            _ => MapFailureProblem<ProblemHttpResult>(logger, result)
        };

        // Creates a standardized paged response from a ResultPaged.
        static PagedResponse<T> ToPagedResponse(ResultPaged<T> result)
        {
            return new PagedResponse<T>
            {
                Items = result.Value,
                Page = result.CurrentPage,
                PageSize = result.PageSize,
                TotalCount = result.TotalCount,
                TotalPages = result.TotalPages,
                HasNextPage = result.HasNextPage,
                HasPreviousPage = result.HasPreviousPage
            };
        }
    }

    /// <summary>
    /// Maps a <see cref="Result{FileContent}"/> to HTTP results for file download operations.
    /// </summary>
    /// <param name="result">The <see cref="Result{FileContent}"/> to map.</param>
    /// <param name="logger">An optional logger that logs failures during mapping and takes precedence over shared HTTP Result logging.</param>
    /// <returns>A <see cref="Results{FileContentHttpResult, NotFound, UnauthorizedHttpResult, BadRequest, ProblemHttpResult}"/> representing the HTTP response.</returns>
    public static Results<FileContentHttpResult, NotFound, UnauthorizedHttpResult, BadRequest, ProblemHttpResult> MapFile(
        Result<FileContent> result,
        ILogger logger = null)
    {
        // Check for custom handlers first
        if (TryMapCustomError(result, logger, out var customResult))
        {
            switch (customResult)
            {
                case FileContentHttpResult fileResult:
                    return fileResult;
                case NotFound notFoundResult:
                    return notFoundResult;
                case UnauthorizedHttpResult unauthorizedResult:
                    return unauthorizedResult;
                case BadRequest badRequestResult:
                    return badRequestResult;
                case ProblemHttpResult problemResult:
                    return problemResult;
                default:
                    // Wrap unrecognized custom results directly in a ProblemHttpResult
                    logger?.LogInformation("Custom error handler returned an unrecognized type. Wrapping in ProblemHttpResult.");
                    return WrapCustomResult(customResult, result, logger);
            }
        }

        return result switch
        {
            { IsSuccess: true, Value: var fileContent } => TypedResults.File(
                fileContents: fileContent.Content,
                contentType: fileContent.ContentType,
                fileDownloadName: fileContent.FileName,
                enableRangeProcessing: fileContent.EnableRangeProcessing,
                lastModified: fileContent.LastModified,
                entityTag: string.IsNullOrWhiteSpace(fileContent.EntityTag) ? null : EntityTagHeaderValue.Parse(fileContent.EntityTag)),

            _ => MapFailureProblem<ProblemHttpResult>(logger, result)
        };
    }

    /// <summary>
    /// Extension method to generate a file download result with a function that creates the file name.
    /// </summary>
    /// <param name="result">The Result containing file content information.</param>
    /// <param name="fileNameFactory">A function that generates a filename based on the file content.</param>
    /// <param name="logger">Optional logger for error cases.</param>
    /// <returns>HTTP result for file download.</returns>
    public static Results<FileContentHttpResult, NotFound, UnauthorizedHttpResult, BadRequest, ProblemHttpResult> MapFile(
        Result<FileContent> result,
        Func<FileContent, string> fileNameFactory,
        ILogger logger = null)
    {
        ArgumentNullException.ThrowIfNull(fileNameFactory);

        if (result.IsSuccess && result.Value != null)
        {
            // Create a new FileContent with the generated filename
            var fileName = fileNameFactory(result.Value);
            var newFileContent = new FileContent(
                result.Value.Content,
                fileName,
                result.Value.ContentType,
                result.Value.EnableRangeProcessing,
                result.Value.LastModified,
                result.Value.EntityTag);

            return MapFile(Result<FileContent>.Success(newFileContent), logger);
        }

        // For failure cases, just pass through the original result
        return MapFile(result, logger);
    }

    /// <summary>
    /// Executes the map unauthorized error operation.
    /// </summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="logger">The logger that receives diagnostic events.</param>
    /// <param name="result">The result used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public static TResult MapUnauthorizedError<TResult>(ILogger logger, Result result)
        where TResult : IResult
    {
        // Check for custom handlers first
        if (TryMapCustomError(result, logger, out var customResult))
        {
            if (customResult is TResult unauthorizedResult)
            {
                return unauthorizedResult;
            }

            // Log a warning if the custom handler returned an incompatible type
            logger?.LogWarning("Custom error handler returned incompatible type. Expected {ExpectedType}, got {ActualType}. Falling back to default handling.", typeof(TResult).Name, customResult.GetType().Name);
        }

        logger?.LogWarning("result - unauthorized access detected: {Error}", result.ToString());
        return MatchResponse<TResult>(TypedResults.Unauthorized());
    }

    /// <summary>
    /// Executes the map not found error operation.
    /// </summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="logger">The logger that receives diagnostic events.</param>
    /// <param name="result">The result used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public static TResult MapNotFoundError<TResult>(ILogger logger, Result result)
        where TResult : IResult
    {
        // Check for custom handlers first
        if (TryMapCustomError(result, logger, out var customResult))
        {
            if (customResult is TResult notFoundResult)
            {
                return notFoundResult;
            }

            // Log a warning if the custom handler returned an incompatible type
            logger?.LogWarning("Custom error handler returned incompatible type. Expected {ExpectedType}, got {ActualType}. Falling back to default handling.", typeof(TResult).Name, customResult.GetType().Name);
        }

        logger?.LogWarning("result - not found: {Error}", result.ToString());
        return MatchResponse<TResult>(TypedResults.NotFound());
    }

    /// <summary>
    /// Maps a failed result to the requested HTTP 400 response.
    /// </summary>
    /// <typeparam name="TResult">The response type. Built-in mappings support <see cref="BadRequest"/>, <c>BadRequest&lt;string&gt;</c>, <c>BadRequest&lt;ProblemDetails&gt;</c>, and <see cref="ProblemHttpResult"/>.</typeparam>
    /// <param name="logger">The logger that receives diagnostic events.</param>
    /// <param name="result">The result used by the operation.</param>
    /// <returns>The declared response with validation details when its type accepts them.</returns>
    /// <exception cref="InvalidOperationException">Thrown when a registered handler or the built-in mappings cannot provide the requested response type.</exception>
    public static TResult MapBadRequestError<TResult>(ILogger logger, Result result)
        where TResult : IResult
    {
        // Check for custom handlers first
        if (TryMapCustomError(result, logger, out var customResult))
        {
            if (customResult is TResult badRequestResult)
            {
                return badRequestResult;
            }

            logger?.LogWarning(
                "Custom error handler returned incompatible type. Expected {ExpectedType}, got {ActualType}. Falling back to default handling.",
                typeof(TResult).Name,
                customResult.GetType().Name);
        }

        logger?.LogWarning("result - bad request error occurred: {Error}", result.ToString());

        return MatchResponse<TResult>(typeof(TResult) switch
        {
            var type when type == typeof(BadRequest) => TypedResults.BadRequest(),
            var type when type == typeof(BadRequest<string>) => TypedResults.BadRequest(CreateBadRequestDetails(result, shared: false).Detail),
            var type when type == typeof(BadRequest<ProblemDetails>) => TypedResults.BadRequest(CreateBadRequestDetails(result, shared: false)),
            _ => TypedResults.Problem(CreateBadRequestDetails(result))
        });
    }

    /// <summary>
    /// Executes the map conflict error operation.
    /// </summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="logger">The logger that receives diagnostic events.</param>
    /// <param name="result">The result used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public static TResult MapConflictError<TResult>(ILogger logger, Result result)
        where TResult : IResult
    {
        // Check for custom handlers first
        if (TryMapCustomError(result, logger, out var customResult))
        {
            if (customResult is TResult problemResult)
            {
                return problemResult;
            }

            // Log a warning if the custom handler returned an incompatible type
            logger?.LogWarning("Custom error handler returned incompatible type. Expected {ExpectedType}, got {ActualType}. Falling back to default handling.", typeof(TResult).Name, customResult.GetType().Name);
        }

        logger?.LogWarning("result - conflict error occurred: {Error}", result.ToString());
        return MatchProblemResponse<TResult>(TypedResults.Problem(CreateProblem(result, 409, "Conflict")));
    }

    /// <summary>
    /// Executes the map concurrency error operation.
    /// </summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="logger">The logger that receives diagnostic events.</param>
    /// <param name="result">The result used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public static TResult MapConcurrencyError<TResult>(ILogger logger, Result result)
        where TResult : IResult
    {
        // Check for custom handlers first
        if (TryMapCustomError(result, logger, out var customResult))
        {
            if (customResult is TResult problemResult)
            {
                return problemResult;
            }

            // Log a warning if the custom handler returned an incompatible type
            logger?.LogWarning("Custom error handler returned incompatible type. Expected {ExpectedType}, got {ActualType}. Falling back to default handling.", typeof(TResult).Name, customResult.GetType().Name);
        }

        logger?.LogWarning("result - concurrency error occurred: {Error}", result.ToString());
        return MatchProblemResponse<TResult>(TypedResults.Problem(CreateProblem(result, 409, "Concurrency Error")));
    }

    /// <summary>
    /// Executes the map domain policy error operation.
    /// </summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="logger">The logger that receives diagnostic events.</param>
    /// <param name="result">The result used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public static TResult MapDomainPolicyError<TResult>(ILogger logger, Result result)
        where TResult : IResult
    {
        // Check for custom handlers first
        if (TryMapCustomError(result, logger, out var customResult))
        {
            if (customResult is TResult problemResult)
            {
                return problemResult;
            }

            // Log a warning if the custom handler returned an incompatible type
            logger?.LogWarning("Custom error handler returned incompatible type. Expected {ExpectedType}, got {ActualType}. Falling back to default handling.", typeof(TResult).Name, customResult.GetType().Name);
        }

        logger?.LogWarning("result - domain policy error occurred: {Error}", result.ToString());
        return MatchProblemResponse<TResult>(TypedResults.Problem(CreateProblem(result, 400, "Domain Policy Error")));
    }

    /// <summary>
    /// Executes the map operation cancelled error operation.
    /// </summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="logger">The logger that receives diagnostic events.</param>
    /// <param name="result">The result used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public static TResult MapOperationCancelledError<TResult>(ILogger logger, Result result)
        where TResult : IResult
    {
        // Check for custom handlers first
        if (TryMapCustomError(result, logger, out var customResult))
        {
            if (customResult is TResult problemResult)
            {
                return problemResult;
            }

            // Log a warning if the custom handler returned an incompatible type
            logger?.LogWarning("Custom error handler returned incompatible type. Expected {ExpectedType}, got {ActualType}. Falling back to default handling.", typeof(TResult).Name, customResult.GetType().Name);
        }

        logger?.LogWarning("result - operation cancelled: {Error}", result.ToString());
        return MatchProblemResponse<TResult>(TypedResults.Problem(CreateProblem(result, 499, "Operation Cancelled")));
    }

    /// <summary>
    /// Executes the map timeout error operation.
    /// </summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="logger">The logger that receives diagnostic events.</param>
    /// <param name="result">The result used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public static TResult MapTimeoutError<TResult>(ILogger logger, Result result)
        where TResult : IResult
    {
        // Check for custom handlers first
        if (TryMapCustomError(result, logger, out var customResult))
        {
            if (customResult is TResult problemResult)
            {
                return problemResult;
            }

            // Log a warning if the custom handler returned an incompatible type
            logger?.LogWarning("Custom error handler returned incompatible type. Expected {ExpectedType}, got {ActualType}. Falling back to default handling.", typeof(TResult).Name, customResult.GetType().Name);
        }

        logger?.LogWarning("result - timeout error occurred: {Error}", result.ToString());
        return MatchProblemResponse<TResult>(TypedResults.Problem(CreateProblem(result, 504, "Timeout Error", logger: logger)));
    }

    /// <summary>
    /// Executes the map exception error operation.
    /// </summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="logger">The logger that receives diagnostic events.</param>
    /// <param name="result">The result used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public static TResult MapExceptionError<TResult>(ILogger logger, Result result)
        where TResult : IResult
    {
        // Check for custom handlers first
        if (TryMapCustomError(result, logger, out var customResult))
        {
            if (customResult is TResult problemResult)
            {
                return problemResult;
            }

            // Log a warning if the custom handler returned an incompatible type
            logger?.LogWarning("Custom error handler returned incompatible type. Expected {ExpectedType}, got {ActualType}. Falling back to default handling.", typeof(TResult).Name, customResult.GetType().Name);
        }

        logger?.LogError(result.Errors.OfType<ExceptionError>().FirstOrDefault()?.Exception,
            "result - exception error occurred: {Error}", result.ToString());
        return MatchProblemResponse<TResult>(TypedResults.Problem(CreateProblem(result, 500, "Exception Error", logger: logger)));
    }

    /// <summary>
    /// Executes the map rule error operation.
    /// </summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="logger">The logger that receives diagnostic events.</param>
    /// <param name="result">The result used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public static TResult MapRuleError<TResult>(ILogger logger, Result result)
        where TResult : IResult
    {
        // Check for custom handlers first
        if (TryMapCustomError(result, logger, out var customResult))
        {
            if (customResult is TResult problemResult)
            {
                return problemResult;
            }

            // Log a warning if the custom handler returned an incompatible type
            logger?.LogWarning("Custom error handler returned incompatible type. Expected {ExpectedType}, got {ActualType}. Falling back to default handling.", typeof(TResult).Name, customResult.GetType().Name);
        }

        logger?.LogWarning("result - rule error occurred: {Error}", result.ToString());
        return MatchProblemResponse<TResult>(TypedResults.Problem(CreateProblem(result, 500, "Rule Error", logger: logger)));
    }

    /// <summary>
    /// Executes the map rule exception error operation.
    /// </summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="logger">The logger that receives diagnostic events.</param>
    /// <param name="result">The result used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public static TResult MapRuleExceptionError<TResult>(ILogger logger, Result result)
        where TResult : IResult
    {
        // Check for custom handlers first
        if (TryMapCustomError(result, logger, out var customResult))
        {
            if (customResult is TResult problemResult)
            {
                return problemResult;
            }

            // Log a warning if the custom handler returned an incompatible type
            logger?.LogWarning("Custom error handler returned incompatible type. Expected {ExpectedType}, got {ActualType}. Falling back to default handling.", typeof(TResult).Name, customResult.GetType().Name);
        }

        logger?.LogError(result.Errors.OfType<RuleExceptionError>().FirstOrDefault()?.Exception,
            "result - rule exception error occurred: {Error}", result.ToString());
        return MatchProblemResponse<TResult>(TypedResults.Problem(CreateProblem(result, 500, "Rule Exception Error", logger: logger)));
    }

    /// <summary>
    /// Executes the map error operation.
    /// </summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="logger">The logger that receives diagnostic events.</param>
    /// <param name="result">The result used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public static TResult MapError<TResult>(ILogger logger, Result result)
        where TResult : IResult
    {
        // Check for custom handlers first
        //if (ResultMapErrorHandlerRegistry.TryExecuteCustomHandler(result, logger, out var customResult))
        //{
        //    if (customResult is TProblem problemResult)
        //    {
        //        return problemResult;
        //    }

        //    // Log a warning if the custom handler returned an incompatible type
        //    logger?.LogWarning("Custom error handler returned incompatible type. " +
        //                     "Expected {ExpectedType}, got {ActualType}. Falling back to default handling.",
        //        typeof(TProblem).Name, customResult.GetType().Name);
        //}

        logger?.LogError("result - unexpected error occurred: {Error}", result.ToString());
        return MatchProblemResponse<TResult>(TypedResults.Problem(CreateProblem(result, 500, "Unexpected Error", instance: Guid.NewGuid().ToString(), logger: logger)));
    }

    ///// <summary>
    ///// Maps an unexpected Result failure to a Problem response, including details.
    ///// </summary>
    ///// <typeparam name="TResult">HTTP result type to return (e.g., ProblemHttpResult).</typeparam>
    ///// <param name="logger">Logger for structured diagnostics (optional).</param>
    ///// <param name="result">The failed result to map.</param>
    ///// <returns>A Problem HTTP result typed as <typeparamref name="TResult"/>.</returns>
    //public static TResult MapError<TResult>(ILogger logger, Result result)
    //    where TResult : IResult
    //{
    //    // Collect structured fields
    //    var messages = result.Messages ?? [];
    //    var errorTypes = result.Errors?.Select(e => e.GetType().Name).ToArray() ?? [];
    //    var instanceId = Guid.NewGuid().ToString();

    //    logger?.LogError("result - unexpected error occurred: IsSuccess={IsSuccess} Messages={MessagesCount} Errors={ErrorsCount} ErrorTypes={ErrorTypes} Instance={InstanceId}", result.IsSuccess, messages.Count, result.Errors?.Count ?? 0, errorTypes, instanceId);

    //    // Build Problem with extensions
    //    var problem = TypedResults.Problem(
    //        detail: string.Join("; ", messages.DefaultIfEmpty("Unexpected error occurred.")),
    //        instance: instanceId,
    //        statusCode: 500,
    //        title: "Unexpected Error",
    //        type: "https://developer.mozilla.org/en-US/docs/Web/HTTP/Status/500");
    //    problem.ProblemDetails.Extensions["errors"] = errorTypes;
    //    problem.ProblemDetails.Extensions["result"] = result.ToString();

    //    return (TResult)(IResult)problem;
    //}

    private static bool TryMapCustomError(Result result, ILogger logger, out IResult customResult)
    {
        if (!ResultMapErrorHandlerRegistry.TryExecuteCustomHandler(result, logger, out customResult))
        {
            return false;
        }

        if (logger is null && customResult is ProblemHttpResult { StatusCode: >= 500 } customProblem)
        {
            // A handler can reuse its response across failures. Keep logging metadata on this mapping only.
            var problem = ProblemDetailsPayload.Copy(customProblem.ProblemDetails, retainResultDetails: true);
            ResponseProblemDetailsService.TrackResult(problem, result, logger);
            customResult = TypedResults.Problem(problem);
        }

        return true;
    }

    private static ProblemHttpResult WrapCustomResult(IResult customResult, Result result, ILogger logger)
    {
        var status = (customResult as IStatusCodeHttpResult)?.StatusCode ?? StatusCodes.Status500InternalServerError;
        var problem = ResultHttpProblemDetails.Create(status, "Custom Error");
        problem.Detail = "A custom error handler was executed. See extensions for details.";
        problem.Extensions["customResultType"] = customResult.GetType().Name;
        ResponseProblemDetailsService.TrackResult(problem, result, logger);
        return TypedResults.Problem(problem);
    }

    /// <summary>Maps a failed Result through the custom handlers and default HTTP failure classification.</summary>
    public static IResult MapDefaultFailure(Result result) =>
        TryMapCustomError(result, null, out var custom)
            ? custom
            : MapFailureProblem<ProblemHttpResult>(null, result);

    private static TResult MapFailureProblem<TResult>(ILogger logger, Result result)
        where TResult : IResult => result.Errors switch
        {
            var errors when errors.Has<ForbiddenError>() => MapStatusProblem<TResult>(logger, result, StatusCodes.Status403Forbidden),
            var errors when errors.Has<UnauthorizedError>() => MapStatusProblem<TResult>(logger, result, StatusCodes.Status401Unauthorized),
            var errors when errors.Has<EntityNotFoundError>() || errors.Has<NotFoundError>() => MapStatusProblem<TResult>(logger, result, StatusCodes.Status404NotFound),
            var errors when errors.Has<ValidationError>() || errors.Has<FluentValidationError>() || errors.Has<CollectionValidationError>() => MapBadRequestError<TResult>(logger, result),
            var errors when errors.Has<ConflictError>() => MapConflictError<TResult>(logger, result),
            var errors when errors.Has<ConcurrencyError>() => MapConcurrencyError<TResult>(logger, result),
            var errors when errors.Has<DomainPolicyError>() => MapDomainPolicyError<TResult>(logger, result),
            var errors when errors.Has<OperationCancelledError>() => MapOperationCancelledError<TResult>(logger, result),
            var errors when errors.Has<TimeoutError>() => MapTimeoutError<TResult>(logger, result),
            var errors when errors.Has<ExceptionError>() => MapExceptionError<TResult>(logger, result),
            var errors when errors.Has<RuleExceptionError>() => MapRuleExceptionError<TResult>(logger, result),
            var errors when errors.Has<RuleError>() => MapRuleError<TResult>(logger, result),
            _ => MapError<TResult>(logger, result)
        };

    private static TResult MapStatusProblem<TResult>(ILogger logger, Result result, int statusCode)
        where TResult : IResult
    {
        logger?.LogWarning("result - HTTP {StatusCode}: {Error}", statusCode, result.ToString());
        return MatchProblemResponse<TResult>(TypedResults.Problem(ResultHttpProblemDetails.Create(statusCode, result: result)));
    }

    private static ProblemDetails CreateProblem(
        Result result, int statusCode, string title, string instance = null, Dictionary<string, string[]> errors = null, ILogger logger = null)
    {
        var problem = ResultHttpProblemDetails.Create(statusCode, title, result);
        problem.Instance = instance;
        ResponseProblemDetailsService.TrackResult(problem, result, logger);

        if (problem is not ResultHttpProblemDetails)
        {
            problem.Detail = result.ToString();
            problem.Extensions["data"] = errors is null ? new { result } : new { result, errors };
        }
        else if (errors is not null)
        {
            problem.Detail = ProblemDetailsPayload.ValidationDetail;
            problem.Extensions["data"] = new { errors };
        }

        return problem;
    }

    private static TResult MatchProblemResponse<TResult>(ProblemHttpResult response)
        where TResult : IResult =>
        typeof(TResult) == typeof(InternalServerError)
            ? MatchResponse<TResult>(TypedResults.InternalServerError())
            : MatchResponse<TResult>(response);

    private static TResult MatchResponse<TResult>(IResult response)
        where TResult : IResult =>
        response is TResult compatible
            ? compatible
            : throw new InvalidOperationException(
                $"The built-in response {response.GetType()} cannot be returned as {typeof(TResult)}. " +
                "Declare a supported response type or register a custom error handler that returns the requested type.");

    private static ProblemDetails CreateBadRequestDetails(Result result, bool shared = true)
    {
        if (result.Errors.Has<ValidationError>() || result.Errors.Has<FluentValidationError>())
        {
            return CreateValidationDetails(result, shared);
        }

        return CreateProblem(result, StatusCodes.Status400BadRequest, "Bad Request");
    }

    private static ProblemDetails CreateValidationDetails(Result result, bool shared)
    {
        var validationErrors = new List<(string PropertyName, string Message)>();

        foreach (var error in result.Errors)
        {
            if (error is ValidationError ve)
            {
                validationErrors.Add((ve.PropertyName, ve.Message));
            }
            else if (error is FluentValidationError fve)
            {
                foreach (var failure in fve.Errors)
                {
                    validationErrors.Add((failure.PropertyName, failure.ErrorMessage));
                }
            }
        }

        var errors = ProblemDetailsPayload.GroupValidation(validationErrors);
        var problem = CreateProblem(result, StatusCodes.Status400BadRequest, "Validation Error",
            errors: errors.Count > 0 ? errors : null);
        if (shared)
        {
            problem.Detail = ProblemDetailsPayload.ValidationDetail;
        }

        return problem;
    }
}
