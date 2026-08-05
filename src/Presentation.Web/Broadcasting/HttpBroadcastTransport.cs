// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Http;

/// <summary>Sends broadcast envelopes directly to registered node receiver addresses over HTTP.</summary>
/// <param name="clientFactory">The HTTP client factory.</param>
/// <param name="authentication">The dedicated broadcast transport authentication.</param>
/// <example><code>services.AddBroadcasting().WithHttpTransport();</code></example>
public sealed class HttpBroadcastTransport(
    IHttpClientFactory clientFactory,
    IBroadcastHttpAuthentication authentication
) : IBroadcastTransport
{
    /// <summary>Gets the named HTTP client used for direct broadcast delivery.</summary>
    public const string ClientName = "BridgingIT.DevKit.Broadcasting";

    /// <inheritdoc />
    public async Task<BroadcastNodeDeliveryResult> SendAsync(
        BroadcastNodeRegistration target,
        BroadcastEnvelope envelope,
        CancellationToken cancellationToken = default
    )
    {
        if (
            target.AdvertisedAddress is null
            || target.AdvertisedAddress.Scheme is not ("http" or "https")
            || target.AdvertisedAddress.Host
                is "0.0.0.0" or "::" or "[::]" or "*" or "+"
            || !string.IsNullOrEmpty(target.AdvertisedAddress.UserInfo)
            || target.AdvertisedAddress.AbsoluteUri.Length > 2048
        )
        {
            return new(
                target.NodeIdentity,
                BroadcastDeliveryOutcome.Unreachable,
                "No receiver address is registered."
            );
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, target.AdvertisedAddress)
        {
            Content = JsonContent.Create(envelope),
        };
        request.Headers.TryAddWithoutValidation(
            "X-Bdk-Broadcast-Id",
            envelope.BroadcastId.ToString("D")
        );
        if (!string.IsNullOrWhiteSpace(envelope.CorrelationId))
        {
            request.Headers.TryAddWithoutValidation(
                CorrelationId.HeaderName,
                envelope.CorrelationId
            );
        }

        try
        {
            await authentication.ApplyAsync(request, cancellationToken).ConfigureAwait(false);
            var client = clientFactory.CreateClient(ClientName);
            using var response = await client
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            try
            {
                var result = await response
                    .Content.ReadFromJsonAsync<BroadcastNodeDeliveryResult>(cancellationToken)
                    .ConfigureAwait(false);
                return result ?? FromStatusCode(target.NodeIdentity, response.StatusCode);
            }
            catch (JsonException)
            {
                return FromStatusCode(target.NodeIdentity, response.StatusCode);
            }
        }
        catch (HttpRequestException)
        {
            return new(target.NodeIdentity, BroadcastDeliveryOutcome.Unreachable);
        }
    }

    private static BroadcastNodeDeliveryResult FromStatusCode(
        string nodeIdentity,
        HttpStatusCode statusCode
    ) =>
        new(
            nodeIdentity,
            (int)statusCode switch
            {
                StatusCodes.Status401Unauthorized
                or StatusCodes.Status403Forbidden
                or StatusCodes.Status413PayloadTooLarge => BroadcastDeliveryOutcome.Rejected,
                >= 500 => BroadcastDeliveryOutcome.Failed,
                _ => BroadcastDeliveryOutcome.Rejected,
            },
            $"Receiver returned HTTP {(int)statusCode}."
        );
}
