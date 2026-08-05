// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Propagates the current application correlation identifier to outbound HTTP requests.
/// </summary>
/// <remarks>
/// A valid ambient <see cref="CorrelationId.Current"/> value takes precedence over a request header.
/// When neither contains a valid value, the handler generates a 12-character lowercase identifier.
/// The resolved identifier is available through <see cref="CorrelationId.Current"/> while subsequent
/// handlers and the primary HTTP handler execute.
/// </remarks>
/// <example>
/// <code>
/// services.AddHttpClient&lt;WeatherClient&gt;()
///     .AddCorrelationIdPropagation();
/// </code>
/// </example>
public sealed class CorrelationIdPropagationHandler : DelegatingHandler
{
    private const int GeneratedIdLength = 12;

    /// <summary>
    /// Adds the resolved correlation identifier to the request and invokes the next handler.
    /// </summary>
    /// <param name="request">The outbound HTTP request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The outbound HTTP response.</returns>
    /// <example><code>var response = await client.SendAsync(request, cancellationToken);</code></example>
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var correlationId = ResolveCorrelationId(request);
        request.Headers.Remove(CorrelationId.HeaderName);
        request.Headers.Add(CorrelationId.HeaderName, correlationId);

        using var scope = CorrelationId.BeginScope(correlationId);
        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private static string ResolveCorrelationId(HttpRequestMessage request)
    {
        var ambientCorrelationId = CorrelationId.Current;
        if (CorrelationId.IsValid(ambientCorrelationId))
        {
            return ambientCorrelationId;
        }

        if (request.Headers.TryGetValues(CorrelationId.HeaderName, out var values))
        {
            var candidates = values.Take(2).ToArray();
            if (candidates.Length == 1 && CorrelationId.IsValid(candidates[0]))
            {
                return candidates[0];
            }
        }

        return KeyGenerator.CreateLowercase(GeneratedIdLength);
    }
}
