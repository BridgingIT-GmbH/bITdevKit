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
///     Exception handler for <see cref="DbUpdateException" />.
///     Returns appropriate error responses for database update failures such as
///     constraint violations, foreign key errors, and other database errors.
/// </summary>
/// <remarks>
///     This handler should be registered after <see cref="DbUpdateConcurrencyExceptionHandler" />
///     since <see cref="DbUpdateConcurrencyException" /> inherits from <see cref="DbUpdateException" />.
/// </remarks>
public class DbUpdateExceptionHandler(
    ILogger<DbUpdateExceptionHandler> logger,
    GlobalExceptionHandlerOptions options)
    : ExceptionHandlerBase<DbUpdateException>(logger, options)
{
    /// <inheritdoc />
    protected override int StatusCode => StatusCodes.Status422UnprocessableEntity;

    /// <inheritdoc />
    protected override string Title => "Database Update Failed";

    /// <inheritdoc />
    protected override string GetDetail(DbUpdateException exception)
    {
        if (!this.Options.IncludeExceptionDetails)
        {
            return "A database error occurred while saving changes.";
        }

        var innerMessage = exception.InnerException?.Message ?? exception.Message;

        return $"[{exception.GetType().Name}] {innerMessage}";
    }

    /// <inheritdoc />
    protected override ProblemDetails CreateProblemDetails(
        HttpContext httpContext,
        DbUpdateException exception)
    {
        var (statusCode, errorType) = this.ClassifyException(exception);
        var problemDetails = base.CreateProblemDetails(httpContext, exception);

        problemDetails.Status = statusCode;
        problemDetails.Extensions["errorType"] = errorType;

        if (this.Options.IncludeExceptionDetails)
        {
            var affectedEntities = exception.Entries
                .Select(e => new
                {
                    Entity = e.Entity.GetType().Name,
                    State = e.State.ToString()
                })
                .ToArray();

            if (affectedEntities.Length > 0)
            {
                problemDetails.Extensions["affectedEntities"] = affectedEntities;
            }
        }

        return problemDetails;
    }

    /// <summary>
    ///     Classifies the exception based on the inner exception message to determine
    ///     the appropriate status code and error type.
    /// </summary>
    private (int StatusCode, string ErrorType) ClassifyException(DbUpdateException exception)
    {
        var innerMessage = exception.InnerException?.Message?.ToUpperInvariant() ?? string.Empty;

        // Unique constraint violation
        if (innerMessage.Contains("UNIQUE") ||
            innerMessage.Contains("DUPLICATE") ||
            innerMessage.Contains("IX_") ||
            innerMessage.Contains("UK_") ||
            innerMessage.Contains("PRIMARY KEY"))
        {
            return (StatusCodes.Status409Conflict, "UniqueConstraintViolation");
        }

        // Foreign key constraint violation
        if (innerMessage.Contains("FOREIGN KEY") ||
            innerMessage.Contains("REFERENCE") ||
            innerMessage.Contains("FK_"))
        {
            return (StatusCodes.Status422UnprocessableEntity, "ForeignKeyViolation");
        }

        // Not null constraint violation
        if (innerMessage.Contains("NOT NULL") ||
            innerMessage.Contains("CANNOT INSERT NULL") ||
            innerMessage.Contains("NULL VALUE"))
        {
            return (StatusCodes.Status422UnprocessableEntity, "NotNullViolation");
        }

        // Check constraint violation
        if (innerMessage.Contains("CHECK CONSTRAINT") ||
            innerMessage.Contains("CK_"))
        {
            return (StatusCodes.Status422UnprocessableEntity, "CheckConstraintViolation");
        }

        // String/data truncation
        if (innerMessage.Contains("TRUNCAT") ||
            innerMessage.Contains("TOO LONG") ||
            innerMessage.Contains("DATA TOO LONG"))
        {
            return (StatusCodes.Status422UnprocessableEntity, "DataTruncation");
        }

        // Default
        return (StatusCodes.Status422UnprocessableEntity, "DatabaseError");
    }
}