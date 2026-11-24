// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Errors;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Problem = Microsoft.AspNetCore.Mvc.ProblemDetails;

/// <summary>Configures DevKit HTTP Problem Details for exceptions and failed Results.</summary>
/// <example><code>builder.Services.AddResponseProblemDetails(options => options.IncludeExceptionDetails = builder.Environment.IsDevelopment());</code></example>
public sealed class ProblemDetailsOptions
{
    /// <summary>Gets or sets whether written problems include exception diagnostics. Defaults to false.</summary>
    public bool IncludeExceptionDetails { get; set; }

    /// <summary>Gets or sets the request predicate for automatic exception and status-code handling.</summary>
    /// <remarks>When unset, DevKit lets ASP.NET Core handle exceptions that it has rewritten to an error page.</remarks>
    public Func<HttpContext, bool> ShouldHandleRequest { get; set; }

    /// <summary>Gets or sets the final enrichment callback for accepted problem writes.</summary>
    public Action<HttpContext, Problem> OnBeforeWriteDetails { get; set; }

    /// <summary>Gets response header names to preserve when automatic error handling clears a response.</summary>
    public ISet<string> AllowedHeaderNames { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "CorrelationId", "FlowId", "TraceId", "WWW-Authenticate", "Strict-Transport-Security",
        "Access-Control-Allow-Credentials", "Access-Control-Allow-Headers", "Access-Control-Allow-Methods",
        "Access-Control-Allow-Origin", "Access-Control-Expose-Headers", "Access-Control-Max-Age"
    };

    /// <summary>Gets or sets whether DevKit logs handled server exceptions. Defaults to true.</summary>
    public bool ShouldLogUnhandledException { get; set; } = true;

    /// <summary>Gets or sets whether DevKit logs mapped 5xx Problem Details and custom MVC Result responses at execution. Defaults to true.</summary>
    public bool ShouldLogResultErrors { get; set; } = true;

    private readonly List<Registration> hostMappings = [];
    private readonly List<Registration> moduleMappings = [];

    /// <summary>Gets the host mappings in declaration order.</summary>
    public IReadOnlyList<Registration> HostMappings => this.hostMappings.AsReadOnly();

    /// <summary>Gets the module defaults in declaration order.</summary>
    public IReadOnlyList<Registration> ModuleMappings => this.moduleMappings.AsReadOnly();

    /// <summary>Registers a host mapping with the supplied status and optional public title and type.</summary>
    /// <example><code>options.Map&lt;KeyNotFoundException&gt;(404);</code></example>
    public ProblemDetailsOptions Map<TException>(int statusCode, string title = null, string typeUri = null)
        where TException : Exception =>
        this.AddStatus<TException>(this.hostMappings, statusCode, title, typeUri);

    /// <summary>Registers a host mapping produced from the exception.</summary>
    public ProblemDetailsOptions Map<TException>(Func<TException, Problem> factory)
        where TException : Exception =>
        this.Map(WrapFactory(factory));

    /// <summary>Registers a host mapping produced from the exception and request when the predicate matches.</summary>
    public ProblemDetailsOptions Map<TException>(Func<TException, HttpContext, Problem> factory,
        Func<TException, HttpContext, bool> when = null)
        where TException : Exception =>
        this.Add(this.hostMappings, factory, when);

    /// <summary>Registers a module default that applies after host mappings.</summary>
    /// <example><code>options.MapDefault&lt;TimeoutException&gt;(504);</code></example>
    public ProblemDetailsOptions MapDefault<TException>(int statusCode, string title = null, string typeUri = null)
        where TException : Exception =>
        this.AddStatus<TException>(this.moduleMappings, statusCode, title, typeUri);

    /// <summary>Registers a module default produced from the exception.</summary>
    public ProblemDetailsOptions MapDefault<TException>(Func<TException, Problem> factory)
        where TException : Exception =>
        this.MapDefault(WrapFactory(factory));

    /// <summary>Registers a module default produced from the exception and request when the predicate matches.</summary>
    public ProblemDetailsOptions MapDefault<TException>(Func<TException, HttpContext, Problem> factory,
        Func<TException, HttpContext, bool> when = null)
        where TException : Exception =>
        this.Add(this.moduleMappings, factory, when);

    /// <summary>Evaluates the configured request predicate, allowing the request when no predicate is set.</summary>
    public bool Handles(HttpContext context) => this.ShouldHandleRequest?.Invoke(context) ?? true;

    private static Func<TException, HttpContext, Problem> WrapFactory<TException>(Func<TException, Problem> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        return (exception, _) => factory(exception);
    }

    private ProblemDetailsOptions AddStatus<TException>(List<Registration> mappings, int statusCode, string title, string typeUri)
        where TException : Exception
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(statusCode, 400);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(statusCode, 599);
        return this.Add<TException>(mappings, (_, _) => new Problem
        {
            Status = statusCode,
            Title = title ?? ReasonPhrases.GetReasonPhrase(statusCode),
            Type = typeUri ?? ProblemDetailsPayload.StatusType(statusCode)
        }, null);
    }

    private ProblemDetailsOptions Add<TException>(List<Registration> mappings, Func<TException, HttpContext, Problem> factory,
        Func<TException, HttpContext, bool> when)
        where TException : Exception
    {
        ArgumentNullException.ThrowIfNull(factory);
        mappings.Add(new(
            new ExceptionMapping
            {
                ExceptionType = typeof(TException),
                ProblemDetailsFactory = (exception, context) => factory((TException)exception, context)
            },
            when is null ? null : (exception, context) => when((TException)exception, context)));
        return this;
    }

    /// <summary>Pairs an exception mapping with its optional request predicate.</summary>
    public sealed record Registration(ExceptionMapping Mapping, Func<Exception, HttpContext, bool> When);
}
