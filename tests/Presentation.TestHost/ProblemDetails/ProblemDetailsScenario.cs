namespace Presentation.TestHost.ProblemDetails;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Diagnostics;
using HttpResult = Microsoft.AspNetCore.Http.IResult;

/// <summary>Configures the optional API and mixed-host Problem Details probes.</summary>
/// <example><code>new ProblemDetailsScenario(builder.Configuration).AddServices(builder.Services);</code></example>
public sealed class ProblemDetailsScenario(IConfiguration configuration)
{
    /// <summary>The configuration section for the fixture.</summary>
    public const string Settings = "TestScenarios:ProblemDetails";

    /// <summary>The API path prefix used by the fixture.</summary>
    public const string ApiPrefix = "/api/problem-details";
    private const string ErrorPath = "/problem-details/error";
    private readonly string profile = configuration[Settings + ":Profile"] ?? "Disabled";

    private bool Enabled => this.profile is "Api" or "ApiDefaults" or "Mixed";

    /// <summary>Registers services for the selected profile.</summary>
    /// <example><code>scenario.AddServices(builder.Services);</code></example>
    public void AddServices(IServiceCollection services)
    {
        if (!this.Enabled)
        {
            return;
        }

        services.AddControllers();
        services.AddOpenApi("problem-details", options => options.AddSchemaTransformer<ResultProblemDetailsSchemaTransformer>());
        services.AddResponseProblemDetails(o =>
        {
            o.IncludeExceptionDetails = configuration.GetValue<bool>(Settings + ":IncludeExceptionDetails");
            if (this.profile == "ApiDefaults")
            {
                return;
            }

            o.Map<InventoryConflictException>(409, "Inventory item conflict", "https://inventory.example/problems/item-conflict");
            if (this.profile == "Mixed")
            {
                o.ShouldHandleRequest = IsApiRequest;
            }
        });
    }

    /// <summary>Installs middleware for the selected profile.</summary>
    public void UseMiddleware(WebApplication app)
    {
        if (this.profile is "Api" or "ApiDefaults")
        {
            app.UseResponseProblemDetails();
        }
        else if (this.profile == "Mixed")
        {
            app.UseExceptionHandler(ErrorPath);
            app.UseWhen(IsApiRequest, api => api.UseStatusCodePages());
            app.UseWhen(context => !IsApiRequest(context), ui =>
                ui.UseStatusCodePagesWithReExecute("/problem-details/status/{0}", createScopeForStatusCodePages: false));
        }
    }

    /// <summary>Maps the fixture endpoints for the selected profile.</summary>
    /// <example><code>scenario.MapEndpoints(app);</code></example>
    public void MapEndpoints(WebApplication app)
    {
        if (!this.Enabled)
        {
            return;
        }

        app.MapControllers();
        app.MapOpenApi();
        var api = app.MapGroup(ApiPrefix);
        api.MapGet("/throw/validation", HttpResult () => throw CreateValidationException()).ProducesResultProblem(400);
        api.MapGet("/result/validation", () => CreateValidationResult().MapHttpNoContent()).ProducesResultProblem(400);
        api.MapGet("/throw/server", HttpResult (HttpContext context) =>
        {
            context.Response.Headers["X-Inventory-Version"] = "42";
            throw CreateServerException();
        }).ProducesResultProblem(500);
        api.MapGet("/result/server", () => CreateServerResult().MapHttpNoContent()).ProducesResultProblem(500);
        api.MapGet("/throw/mapped", HttpResult () => throw new InventoryConflictException()).ProducesResultProblem(409);
        api.MapGet("/throw/not-implemented", HttpResult () => throw new NotImplementedException("Fictional inventory failure."));
        api.MapGet("/throw/upstream", HttpResult () => throw new HttpRequestException("Fictional inventory failure."));
        api.MapGet("/throw/not-supported", HttpResult () => throw new NotSupportedException());
        api.MapGet("/empty", () => TypedResults.StatusCode(404));
        api.MapGet("/explicit", () => TypedResults.Text("Inventory item unavailable.", statusCode: 404));
        api.MapGet("/empty-with-length", (HttpContext context) =>
        {
            context.Response.ContentLength = 0;
            return TypedResults.StatusCode(404);
        });

        app.MapMethods("/problem-details/ui/throw", ["GET", "POST"], HttpResult () => throw CreateServerException());
        app.MapGet("/problem-details/ui/result", () => CreateServerResult().MapHttpNoContent());
        app.MapMethods(ErrorPath, ["GET", "POST"], () =>
            TypedResults.Content("<h1>Inventory error page</h1>", "text/html", statusCode: 500)).ExcludeFromDescription();
        app.MapGet("/problem-details/status/{status:int}", (int status) =>
            TypedResults.Content($"<h1>Inventory status {status}</h1>", "text/html", statusCode: status)).ExcludeFromDescription();
    }

    /// <summary>Creates the fixture exception for missing inventory names.</summary>
    public static ValidationException CreateValidationException() =>
        new([new ValidationFailure("Entity.Name", "Name is required.")]);

    /// <summary>Creates the equivalent missing-name Result failure.</summary>
    public static Result CreateValidationResult() =>
        Result.Failure(new ValidationError("Name is required.", "Entity.Name"));

    /// <summary>Creates the fixture server exception with an inner cause.</summary>
    public static InvalidOperationException CreateServerException() =>
        new("Fictional store failure.", new ArgumentException("Fictional inner failure."));

    /// <summary>Captures the fixture server exception in a failed Result.</summary>
    public static Result CreateServerResult() => Result.Failure(new ExceptionError(CreateServerException()));

    private static bool IsApiRequest(HttpContext context)
    {
        var original = context.Features.Get<IExceptionHandlerPathFeature>()?.Path;
        return (original is null ? context.Request.Path : new PathString(original)).StartsWithSegments(ApiPrefix);
    }
}

/// <summary>Represents an inventory conflict handled by the fixture mapping.</summary>
public sealed class InventoryConflictException : Exception;
