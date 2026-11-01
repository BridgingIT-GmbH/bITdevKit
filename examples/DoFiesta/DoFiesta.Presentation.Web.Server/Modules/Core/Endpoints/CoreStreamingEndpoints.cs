// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Presentation.Web.Server.Modules.Core;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Examples.DoFiesta.Application.Modules.Core;
using BridgingIT.DevKit.Presentation.Web;

/// <summary>Demonstrates cancellable live NDJSON without introducing a durable event feed.</summary>
/// <example><code>GET /api/core/streaming/ticks?intervalMilliseconds=20000</code></example>
public sealed class CoreStreamingEndpoints : EndpointsBase
{
    /// <summary>Maps the authenticated fictional tick endpoint.</summary>
    /// <example><code>endpoints.Map(app);</code></example>
    public override void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/core/streaming/ticks", async (IRequester requester, int? intervalMilliseconds, CancellationToken ct) =>
                (await requester.SendAsync(new StreamTicksQuery { IntervalMilliseconds = intervalMilliseconds ?? 1000 }, cancellationToken: ct)).MapHttpNdjson())
            .RequireAuthorization()
            .WithTags("CoreModule.Streaming")
            .WithName("Core.Streaming.Ticks")
            .WithDescription("Fictional live ticks until cancellation. Starts fresh on every request; no replay or automatic reconnection.")
            .WithNdjson<StreamTickModel>(o => o.Mode = NdjsonMode.Envelope)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesResultProblem(StatusCodes.Status400BadRequest);
    }
}
