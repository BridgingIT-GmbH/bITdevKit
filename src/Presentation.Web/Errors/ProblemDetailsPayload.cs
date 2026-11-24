// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Errors;

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security;
using System.Text.Json.Serialization;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Problem = Microsoft.AspNetCore.Mvc.ProblemDetails;

/// <summary>Provides the shared wire values used by DevKit HTTP Problem Details.</summary>
public static class ProblemDetailsPayload
{
    /// <summary>The public detail for an unexpected failure.</summary>
    public const string UnexpectedDetail = "An unexpected error occurred.";
    /// <summary>The public detail for a field validation failure.</summary>
    public const string ValidationDetail = "One or more validation errors occurred.";
    /// <summary>The JSON media type for Problem Details.</summary>
    public const string ContentType = "application/problem+json";

    private const string CorrelationIdKey = "correlationId";
    private static readonly ConditionalWeakTable<Problem, string> MapperCorrelations = new();

    /// <summary>Returns the DevKit type URI for an HTTP status.</summary>
    public static string StatusType(int status) => $"https://developer.mozilla.org/en-US/docs/Web/HTTP/Status/{status}";

    /// <summary>Groups validation messages by normalized field name.</summary>
    public static Dictionary<string, string[]> GroupValidation(IEnumerable<(string Name, string Message)> errors) =>
        errors.GroupBy(error => NormalizeName(error.Name))
            .ToDictionary(group => group.Key, group => group.Select(error => error.Message).ToArray());

    /// <summary>Creates the default public problem for the supplied exception.</summary>
    public static Problem CreateExceptionProblem(Exception exception)
    {
        var (status, title, detail) = exception switch
        {
            ValidationException => (400, "Validation Error", ValidationDetail),
            RuleException or DomainPolicyException => (400, "Bad Request", "The request could not be completed."),
            SecurityException => (401, "Unauthorized", "Access is not authorized."),
            ModuleNotEnabledException => (503, "Module Not Enabled", "The requested feature is unavailable."),
            AggregateNotFoundException => (404, "Aggregate Not Found", "The requested resource was not found."),
            EntityNotFoundException => (404, "Entity Not Found", "The requested resource was not found."),
            _ => (500, "Internal Server Error", UnexpectedDetail)
        };

        var problem = new Problem { Status = status, Title = title, Type = StatusType(status), Detail = detail };
        if (exception is ValidationException validation &&
            GroupValidation(validation.Errors.OrderBy(error => error.PropertyName, StringComparer.Ordinal)
                .Select(error => (error.PropertyName, error.ErrorMessage))) is { Count: > 0 } errors)
        {
            problem.Extensions["data"] = new { errors };
        }

        return problem;
    }

    /// <summary>Copies a problem's response fields while retaining application-defined problem types.</summary>
    /// <param name="problem">The problem to copy before adding request-specific fields.</param>
    /// <param name="retainResultDetails">Whether to retain the DevKit Result subtype for later diagnostic preparation.</param>
    /// <returns>A copy with its own extensions and validation messages. Other application-defined values are shallow copied.</returns>
    public static Problem Copy(Problem problem, bool retainResultDetails = false)
    {
        var copy = !retainResultDetails && problem is ResultHttpProblemDetails
            ? new Problem { Detail = problem.Detail, Instance = problem.Instance, Status = problem.Status, Title = problem.Title, Type = problem.Type }
            : (Problem)CloneObject(problem);
        var extensions = problem.Extensions;
        copy.Extensions = new Dictionary<string, object>(extensions, GetComparer(extensions));
        if (MapperCorrelations.TryGetValue(problem, out var mapperDefault))
        {
            MapperCorrelations.Add(copy, mapperDefault);
        }

        if (copy is HttpValidationProblemDetails { Errors: { } errors } validation)
        {
            validation.Errors = errors.ToDictionary(error => error.Key, error => error.Value?.ToArray(), GetComparer(errors));
        }

        return copy;
    }

    // ProblemDetails has no copy contract. Preserve custom types and constructors without serializing their fields.
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "MemberwiseClone")]
    private static extern object CloneObject(object instance);

    private static IEqualityComparer<string> GetComparer<T>(IDictionary<string, T> values) =>
        values is Dictionary<string, T> dictionary ? dictionary.Comparer : StringComparer.Ordinal;

    /// <summary>Adds a default correlation ID or replaces the mapper's default with the ID resolved for response execution.</summary>
    /// <param name="problem">The problem to enrich.</param>
    /// <param name="context">The HTTP request, or null during Result mapping.</param>
    /// <param name="automatic">Whether the response uses automatic exception or status handling.</param>
    public static void SetCorrelation(Problem problem, HttpContext context, bool automatic)
    {
        var identifier = ResolveCorrelation(context, automatic);
        if (identifier is null)
        {
            return;
        }

        if (context is not null && MapperCorrelations.TryGetValue(problem, out var mapperDefault) &&
            problem.Extensions.TryGetValue(CorrelationIdKey, out var correlationId) &&
            ReferenceEquals(mapperDefault, correlationId))
        {
            problem.Extensions[CorrelationIdKey] = identifier;
        }
        else if (problem.Extensions.TryAdd(CorrelationIdKey, identifier) && context is null)
        {
            MapperCorrelations.TryAdd(problem, identifier);
        }
    }

    /// <summary>Gets the Result correlation ID from the selected problem or resolves it from the current request.</summary>
    /// <param name="context">The HTTP request.</param>
    /// <param name="problem">The payload selected for writing, or null for a custom response.</param>
    /// <returns>The correlation identifier for response logging.</returns>
    public static object GetResultCorrelation(HttpContext context, Problem problem = null) =>
        problem?.Extensions.TryGetValue(CorrelationIdKey, out var identifier) is true
            ? identifier
            : ResolveCorrelation(context, automatic: false);

    private static string ResolveCorrelation(HttpContext context, bool automatic) =>
        context?.TryGetCorrelationId() ??
        (automatic ? context?.TraceIdentifier :
            CorrelationId.Current ?? Activity.Current?.TraceId.ToHexString() ?? context?.TraceIdentifier);

    /// <summary>Adds the current activity or request trace identifier when none is supplied.</summary>
    public static void SetTrace(Problem problem, HttpContext context, string key = "traceId") =>
        problem.Extensions.TryAdd(key, Activity.Current?.Id ?? context.TraceIdentifier);

    /// <summary>Formats opt-in exception diagnostics and their inner exceptions.</summary>
    public static DiagnosticEntry BuildDiagnostic(Exception exception)
    {
        var inner = exception is AggregateException aggregate
            ? aggregate.InnerExceptions.Select(BuildDiagnostic).ToArray()
            : exception.InnerException is { } cause ? [BuildDiagnostic(cause)] : null;
        return new(exception.GetType().FullName, exception.Message, exception.StackTrace, inner is { Length: > 0 } ? inner : null);
    }

    private static string NormalizeName(string name)
    {
        if (name?.StartsWith("Entity.", StringComparison.OrdinalIgnoreCase) == true)
        {
            name = name["Entity.".Length..];
        }

        return string.IsNullOrWhiteSpace(name) ? "validation" : name;
    }

    /// <summary>Represents one exception diagnostic in the shared response format.</summary>
    public sealed record DiagnosticEntry(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("message")] string Message,
        [property: JsonPropertyName("stackTrace"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string StackTrace = null,
        [property: JsonPropertyName("innerExceptions"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DiagnosticEntry[] InnerExceptions = null);
}
