// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Host;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

/// <summary>
///     Exception handler for <see cref="DbUpdateConcurrencyException" />.
///     Returns a 409 Conflict response when a concurrency conflict is detected
///     during a database update operation (optimistic concurrency violation).
/// </summary>
public class DbUpdateConcurrencyExceptionHandler(
    ILogger<DbUpdateConcurrencyExceptionHandler> logger,
    GlobalExceptionHandlerOptions options)
    : ExceptionHandlerBase<DbUpdateConcurrencyException>(logger, options)
{
    /// <inheritdoc />
    protected override int StatusCode => StatusCodes.Status409Conflict;

    /// <inheritdoc />
    protected override string Title => "Concurrency Conflict";

    /// <inheritdoc />
    protected override string GetDetail(DbUpdateConcurrencyException exception)
    {
        if (!this.Options.IncludeExceptionDetails)
        {
            return "The record was modified by another user. Please refresh and try again.";
        }

        var entityNames = exception.Entries
            .Select(e => e.Entity.GetType().Name).Distinct();

        return $"[{exception.GetType().Name}] Concurrency conflict detected for: {string.Join(", ", entityNames)}. {exception.Message}";
    }

    /// <inheritdoc />
    protected override ProblemDetails CreateProblemDetails(
        HttpContext httpContext,
        DbUpdateConcurrencyException exception)
    {
        var problemDetails = base.CreateProblemDetails(httpContext, exception);

        if (this.Options.IncludeExceptionDetails)
        {
            problemDetails.Extensions["affectedEntities"] = exception.Entries
                .Select(e => new
                {
                    Entity = e.Entity.GetType().Name,
                    State = e.State.ToString()
                }).ToArray();
        }

        return problemDetails;
    }

    /// <inheritdoc />
    protected override void LogException(DbUpdateConcurrencyException exception)
    {
        var entityNames = exception.Entries
            .Select(e => e.Entity.GetType().Name).Distinct();

        this.Logger?.LogWarning(
            exception,
            "{ExceptionType} occurred for entities [{Entities}]: {ExceptionMessage}",
            exception.GetType().Name,
            string.Join(", ", entityNames),
            exception.Message);
    }
}