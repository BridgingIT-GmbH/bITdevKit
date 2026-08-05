// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;

/// <summary>Applies outbound and validates inbound Broadcasting HTTP transport authentication.</summary>
/// <example><code>services.AddSingleton&lt;IBroadcastHttpAuthentication, CertificateBroadcastAuthentication&gt;();</code></example>
public interface IBroadcastHttpAuthentication
{
    /// <summary>Applies authentication data to an outbound broadcast request.</summary>
    ValueTask ApplyAsync(HttpRequestMessage request, CancellationToken cancellationToken = default);

    /// <summary>Authenticates an inbound request before its body is read.</summary>
    ValueTask<bool> AuthenticateAsync(
        HttpContext context,
        CancellationToken cancellationToken = default
    );
}

/// <summary>Allows requests when no explicit Broadcasting transport authentication is selected.</summary>
/// <example><code>services.AddBroadcasting().WithHttpTransport();</code></example>
public sealed class AllowAllBroadcastHttpAuthentication : IBroadcastHttpAuthentication
{
    /// <inheritdoc />
    public ValueTask ApplyAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken = default
    ) => ValueTask.CompletedTask;

    /// <inheritdoc />
    public ValueTask<bool> AuthenticateAsync(
        HttpContext context,
        CancellationToken cancellationToken = default
    ) => ValueTask.FromResult(true);
}

/// <summary>Provides the DevKit built-in exact shared-secret HTTP authentication.</summary>
/// <example><code>services.AddBroadcasting().WithHttpTransport(options => options.SharedSecret(secret));</code></example>
public sealed class SharedSecretBroadcastHttpAuthentication(BroadcastingHttpOptions options)
    : IBroadcastHttpAuthentication
{
    /// <summary>Gets the built-in request header name.</summary>
    public const string HeaderName = "X-Bdk-Broadcast-Key";

    /// <inheritdoc />
    public ValueTask ApplyAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var value = Convert.ToBase64String(Encoding.UTF8.GetBytes(options.SharedSecret));
        request.Headers.TryAddWithoutValidation(HeaderName, value);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<bool> AuthenticateAsync(
        HttpContext context,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        var values = context.Request.Headers[HeaderName];
        if (values.Count > 1)
        {
            return ValueTask.FromResult(false);
        }

        byte[] supplied;
        try
        {
            supplied =
                values.Count == 0 || string.IsNullOrEmpty(values[0])
                    ? []
                    : Convert.FromBase64String(values[0]);
        }
        catch (FormatException)
        {
            return ValueTask.FromResult(false);
        }

        var expected = Encoding.UTF8.GetBytes(options.SharedSecret);
        return ValueTask.FromResult(CryptographicOperations.FixedTimeEquals(expected, supplied));
    }
}
