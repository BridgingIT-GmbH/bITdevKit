// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using System.Net;
using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using IResult = Microsoft.AspNetCore.Http.IResult;

/// <summary>Configures the internal Broadcasting receiver endpoint group.</summary>
/// <example><code>var options = new BroadcastingEndpointsOptions(httpOptions);</code></example>
public sealed class BroadcastingEndpointsOptions : EndpointsOptionsBase
{
    /// <summary>Initializes endpoint defaults isolated from application fallback authorization.</summary>
    public BroadcastingEndpointsOptions(BroadcastingHttpOptions httpOptions)
    {
        this.GroupPath = httpOptions.ReceiverRoute;
        this.GroupTag = "_bdk.Broadcasting";
        this.AllowAnonymous = true;
        this.ExcludeFromDescription = true;
    }
}

/// <summary>Maps the internal HTTP broadcast receiver.</summary>
/// <example><code>app.MapEndpoints();</code></example>
public sealed class BroadcastingEndpoints(
    BroadcastingOptions broadcastingOptions,
    BroadcastingHttpOptions httpOptions,
    IBroadcastHttpAuthentication authentication,
    IBroadcastReceiver receiver,
    IMetricsService metrics = null,
    ILogger<BroadcastingEndpoints> logger = null
) : EndpointsBase
{
    private readonly BroadcastingEndpointsOptions endpointOptions = new(httpOptions)
    {
        Enabled = broadcastingOptions.Enabled,
    };

    /// <inheritdoc />
    public override void Map(IEndpointRouteBuilder app)
    {
        if (!this.endpointOptions.Enabled)
        {
            return;
        }

        var group = this.MapGroup(app, this.endpointOptions);
        var bodyLimit = httpOptions.GetRequestBodyLimit(broadcastingOptions.MaximumPayloadBytes);
        group
            .MapPost(string.Empty, (Func<HttpContext, Task<IResult>>)this.ReceiveAsync)
            .WithName("_bdk.Broadcasting.Receive")
            .WithMetadata(new RequestSizeLimitAttribute(bodyLimit))
            .DisableAntiforgery();
    }

    private async Task<IResult> ReceiveAsync(HttpContext context)
    {
        if (
            !await authentication
                .AuthenticateAsync(context, context.RequestAborted)
                .ConfigureAwait(false)
        )
        {
            metrics?.Increment("broadcasting_authentication", "rejected");
            if (logger is not null)
            {
                BroadcastingTypedLogger.LogAuthenticationRejected(logger, "UTL");
            }

            return Results.Json(
                new BroadcastNodeDeliveryResult(
                    broadcastingOptions.NodeIdentity ?? string.Empty,
                    BroadcastDeliveryOutcome.Rejected,
                    "Broadcast transport authentication failed."
                ),
                statusCode: (int)HttpStatusCode.Unauthorized
            );
        }

        metrics?.Increment("broadcasting_authentication", "accepted");
        var bodyLimit = httpOptions.GetRequestBodyLimit(broadcastingOptions.MaximumPayloadBytes);
        var bodySizeFeature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (bodySizeFeature is { IsReadOnly: false })
        {
            bodySizeFeature.MaxRequestBodySize = bodyLimit;
        }

        if (context.Request.ContentLength is > 0 && context.Request.ContentLength > bodyLimit)
        {
            return Results.Json(
                new BroadcastNodeDeliveryResult(
                    broadcastingOptions.NodeIdentity ?? string.Empty,
                    BroadcastDeliveryOutcome.Rejected,
                    "Request body exceeds the configured limit."
                ),
                statusCode: (int)HttpStatusCode.RequestEntityTooLarge
            );
        }

        BroadcastEnvelope envelope;
        try
        {
            envelope = await context
                .Request.ReadFromJsonAsync<BroadcastEnvelope>(context.RequestAborted)
                .ConfigureAwait(false);
        }
        catch (BadHttpRequestException exception)
            when (exception.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            return Results.Json(
                new BroadcastNodeDeliveryResult(
                    broadcastingOptions.NodeIdentity ?? string.Empty,
                    BroadcastDeliveryOutcome.Rejected,
                    "Request body exceeds the configured limit."
                ),
                statusCode: StatusCodes.Status413PayloadTooLarge
            );
        }
        catch
        {
            return Results.Json(
                new BroadcastNodeDeliveryResult(
                    broadcastingOptions.NodeIdentity ?? string.Empty,
                    BroadcastDeliveryOutcome.Rejected,
                    "Malformed broadcast envelope."
                ),
                statusCode: (int)HttpStatusCode.BadRequest
            );
        }

        if (envelope is null)
        {
            return Results.BadRequest();
        }

        var result = await receiver
            .ReceiveAsync(envelope, context.RequestAborted)
            .ConfigureAwait(false);
        var statusCode = result.Outcome switch
        {
            BroadcastDeliveryOutcome.Accepted
            or BroadcastDeliveryOutcome.AlreadyProcessed
            or BroadcastDeliveryOutcome.Expired
            or BroadcastDeliveryOutcome.Unsupported => HttpStatusCode.OK,
            BroadcastDeliveryOutcome.Rejected
                when string.Equals(
                    result.Detail,
                    "Target scope is not configured locally.",
                    StringComparison.Ordinal
                ) => HttpStatusCode.Forbidden,
            BroadcastDeliveryOutcome.Rejected => HttpStatusCode.BadRequest,
            _ => HttpStatusCode.InternalServerError,
        };
        return Results.Json(result, statusCode: (int)statusCode);
    }
}
