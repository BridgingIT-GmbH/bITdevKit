// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Entities.Dashboard;

using System.Data.Common;
using System.Diagnostics;
using System.Text.RegularExpressions;
using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Http;
using IResult = Microsoft.AspNetCore.Http.IResult;

/// <summary>A validation message tied to a property (or to the whole record when <see cref="Field"/> is null).</summary>
/// <param name="Field">The property name, or null.</param>
/// <param name="Message">The message.</param>
internal sealed record EntitiesDashboardFieldError(string Field, string Message);

/// <summary>
/// The JSON response of every write endpoint (<c>success</c>, <c>statusCode</c>, <c>message</c>, <c>fieldErrors</c>,
/// <c>correlationId</c> and an operation specific <c>payload</c>). Exception messages are not included, except the first
/// line of a database rejection (see <see cref="FromDatabaseException"/>).
/// </summary>
internal sealed partial class EntitiesDashboardOutcome
{
    /// <summary>Gets a value indicating whether the operation succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the HTTP status code that is returned with the outcome.</summary>
    public int StatusCode { get; init; }

    /// <summary>Gets the user-facing message.</summary>
    public string Message { get; init; }

    /// <summary>Gets the field level validation messages.</summary>
    public IReadOnlyList<EntitiesDashboardFieldError> FieldErrors { get; init; } = [];

    /// <summary>Gets the correlation id of a server failure.</summary>
    public string CorrelationId { get; init; }

    /// <summary>Gets the operation specific payload (for example the saved id, the diff or the bulk-delete summary).</summary>
    public object Payload { get; init; }

    /// <summary>Gets the correlation id for the current request (trace id or the request trace identifier).</summary>
    /// <param name="http">The HTTP context.</param>
    /// <returns>The correlation id.</returns>
    public static string GetCorrelationId(HttpContext http) =>
        http?.TryGetCorrelationId() ?? Activity.Current?.TraceId.ToString() ?? http?.TraceIdentifier;

    /// <summary>Creates a successful outcome.</summary>
    /// <param name="message">The message.</param>
    /// <param name="payload">The payload.</param>
    /// <param name="statusCode">The status code (200 or 201).</param>
    /// <returns>The outcome.</returns>
    public static EntitiesDashboardOutcome Ok(string message, object payload = null, int statusCode = StatusCodes.Status200OK) =>
        new() { Success = true, StatusCode = statusCode, Message = message, Payload = payload };

    /// <summary>Creates a failed outcome for client input that cannot be processed.</summary>
    /// <param name="message">The message.</param>
    /// <param name="statusCode">The status code (400, 404, 409 or 422).</param>
    /// <param name="fieldErrors">The field errors.</param>
    /// <returns>The outcome.</returns>
    public static EntitiesDashboardOutcome Failed(string message, int statusCode = StatusCodes.Status400BadRequest, IReadOnlyList<EntitiesDashboardFieldError> fieldErrors = null) =>
        new() { Success = false, StatusCode = statusCode, Message = message, FieldErrors = fieldErrors ?? [] };

    /// <summary>Creates the conflict outcome for a stale concurrency version.</summary>
    /// <returns>The outcome.</returns>
    public static EntitiesDashboardOutcome Conflict() =>
        Failed("The record was changed by someone else. Reload it and apply your changes again.", StatusCodes.Status409Conflict);

    /// <summary>Creates the outcome for a missing record.</summary>
    /// <returns>The outcome.</returns>
    public static EntitiesDashboardOutcome NotFound() =>
        Failed("The record was not found.", StatusCodes.Status404NotFound);

    /// <summary>Creates a generic server failure outcome carrying only the correlation id.</summary>
    /// <param name="correlationId">The correlation id.</param>
    /// <returns>The outcome.</returns>
    public static EntitiesDashboardOutcome ServerError(string correlationId) =>
        new()
        {
            Success = false,
            StatusCode = StatusCodes.Status500InternalServerError,
            Message = "The operation failed unexpectedly.",
            CorrelationId = correlationId
        };

    /// <summary>
    /// Creates a 422 outcome for a rejection by the database (for example a truncated value or a violated constraint).
    /// Only the first line of the database message is exposed, never the stack trace. When the message names a column of
    /// the entity the message is attached to that property.
    /// </summary>
    /// <param name="exception">The failure, searched for a <see cref="DbException"/> in its inner exception chain.</param>
    /// <param name="correlationId">The correlation id.</param>
    /// <param name="propertyNames">The property names of the entity, used to match the reported column.</param>
    /// <returns>The outcome, or null when the failure is not a database rejection.</returns>
    public static EntitiesDashboardOutcome FromDatabaseException(Exception exception, string correlationId, IEnumerable<string> propertyNames)
    {
        DbException database = null;
        for (var current = exception; current != null && database == null; current = current.InnerException)
        {
            database = current as DbException;
        }

        if (database == null)
        {
            return null;
        }

        var detail = (database.Message ?? string.Empty).Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();
        if (string.IsNullOrWhiteSpace(detail))
        {
            return null;
        }

        if (detail.Length > 400)
        {
            detail = detail[..400] + "…";
        }

        var column = ColumnPattern().Match(detail);
        var field = column.Success
            ? propertyNames?.FirstOrDefault(p => string.Equals(p, column.Groups[1].Value, StringComparison.OrdinalIgnoreCase))
            : null;

        return new EntitiesDashboardOutcome
        {
            Success = false,
            StatusCode = StatusCodes.Status422UnprocessableEntity,
            Message = field == null ? "The database rejected the values." : "The database rejected the values. See the highlighted problem.",
            FieldErrors = [new EntitiesDashboardFieldError(field, detail)],
            CorrelationId = correlationId
        };
    }

    /// <summary>Maps the errors of a failed result to an outcome: 422 for validation, rule or database errors, 404, 409 or 500.</summary>
    /// <param name="errors">The result errors.</param>
    /// <param name="correlationId">The correlation id used for unexpected failures.</param>
    /// <param name="propertyNames">The entity property names used to attach database errors to a field.</param>
    /// <returns>The outcome.</returns>
    public static EntitiesDashboardOutcome FromErrors(IReadOnlyList<IResultError> errors, string correlationId, IEnumerable<string> propertyNames = null)
    {
        errors ??= [];

        if (errors.Any(IsConcurrency))
        {
            return Conflict();
        }

        if (errors.Any(e => e is NotFoundError or EntityNotFoundError))
        {
            return NotFound();
        }

        var fields = new List<EntitiesDashboardFieldError>();
        foreach (var error in errors)
        {
            switch (error)
            {
                case ValidationError validation:
                    fields.Add(new EntitiesDashboardFieldError(validation.PropertyName, validation.Message));
                    break;
                case FluentValidationError fluent:
                    fields.AddRange(fluent.Errors.Select(f => new EntitiesDashboardFieldError(
                        string.IsNullOrWhiteSpace(f.PropertyName) ? null : f.PropertyName, f.ErrorMessage)));
                    break;
                case InvalidInputError or RequiredFieldError or RuleError or BusinessRuleError or DomainPolicyError or DuplicateError or EntityDuplicateError:
                    fields.Add(new EntitiesDashboardFieldError(null, error.Message));
                    break;
            }
        }

        if (fields.Count > 0)
        {
            return Failed("The values could not be saved. Fix the highlighted problems and try again.", StatusCodes.Status422UnprocessableEntity, fields);
        }

        if (errors.Any(e => e is ForbiddenError or UnauthorizedError or AccessDeniedError))
        {
            return Failed("You are not allowed to perform this operation.", StatusCodes.Status403Forbidden);
        }

        foreach (var error in errors.OfType<ExceptionError>())
        {
            var database = FromDatabaseException(error.Exception, correlationId, propertyNames);
            if (database != null)
            {
                return database;
            }
        }

        return ServerError(correlationId);
    }

    /// <summary>Converts the outcome to a JSON response using its status code.</summary>
    /// <returns>The HTTP result.</returns>
    public IResult ToResult() =>
        Results.Json(this, EntitiesDashboardJson.Options, "application/json", this.StatusCode);

    private static bool IsConcurrency(IResultError error) =>
        error is ConcurrencyError or ConflictError or StaleDataError
        || (error is ExceptionError { ExceptionType: { } type } && type.Contains("Concurrency", StringComparison.OrdinalIgnoreCase));

    [GeneratedRegex("column '([^']+)'", RegexOptions.IgnoreCase)]
    private static partial Regex ColumnPattern();
}
