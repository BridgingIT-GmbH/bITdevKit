// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Presentation.Web;

using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using IResult = Microsoft.AspNetCore.Http.IResult;

/// <summary>Configures the Broadcasting management search endpoints.</summary>
/// <example><code>services.AddBroadcastingManagementEndpoints(options => options.RequirePolicy = "Operations.Read");</code></example>
public sealed class BroadcastingManagementEndpointsOptions : EndpointsOptionsBase
{
    /// <summary>Initializes the management route with authorization required.</summary>
    /// <example><code>var options = new BroadcastingManagementEndpointsOptions();</code></example>
    public BroadcastingManagementEndpointsOptions()
    {
        this.GroupPath = "/_bdk/api/broadcasting";
        this.GroupTag = "_bdk.Broadcasting";
        this.RequireAuthorization = true;
    }
}

/// <summary>Exposes operational node search independently of the anonymous transport receiver.</summary>
/// <param name="diagnostics">The safe registry query service.</param>
/// <param name="options">The management route and authorization settings.</param>
/// <example><code>services.AddBroadcastingManagementEndpoints();</code></example>
public sealed class BroadcastingManagementEndpoints(
    IBroadcastingDiagnostics diagnostics,
    BroadcastingManagementEndpointsOptions options) : EndpointsBase
{
    /// <inheritdoc />
    public override void Map(IEndpointRouteBuilder app)
    {
        if (!this.Enabled || !options.Enabled)
        {
            return;
        }

        this.MapGroup(app, options).MapPost("search", this.SearchAsync)
            .WithName("_bdk.Broadcasting.SearchNodes")
            .WithSummary("Search registered broadcast nodes")
            .Produces<ResultPaged<BroadcastNodeRegistration>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .WithFilterSchema(isRequestBody: true);
    }

    private async Task<IResult> SearchAsync(HttpContext http, CancellationToken cancellationToken)
    {
        var filter = await http.FromBodyFilterAsync();
        if (filter is null)
        {
            return http.Response.HasStarted ? Results.Empty : Results.BadRequest("A FilterModel request body is required.");
        }

        try
        {
            var result = await diagnostics.SearchNodesAsync(filter, cancellationToken);
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or FormatException or InvalidCastException or OverflowException)
        {
            return Results.Problem(exception.Message, statusCode: StatusCodes.Status400BadRequest, title: "Invalid search filter");
        }
    }
}
