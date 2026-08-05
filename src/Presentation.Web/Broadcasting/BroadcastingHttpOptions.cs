// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

/// <summary>Configures the standard Broadcasting HTTP transport and receiver.</summary>
/// <example>
/// <code>
/// services.AddBroadcasting()
///     .WithHttpTransport(options => options.SharedSecret(configuration["Broadcasting:SharedSecret"]));
/// </code>
/// </example>
public sealed class BroadcastingHttpOptions
{
    /// <summary>Gets or sets an optional process-specific base or receiver address.</summary>
    public Uri AdvertisedAddress { get; set; }

    /// <summary>Gets or sets the internal receiver route.</summary>
    public string ReceiverRoute { get; set; } = "/_bdk/api/broadcasting";

    /// <summary>Gets whether built-in shared-secret authentication is selected.</summary>
    public bool SharedSecretEnabled { get; set; }

    /// <summary>Gets the exact configured shared secret, with null represented as empty.</summary>
    public string SharedSecret { get; set; } = string.Empty;

    /// <summary>Gets or sets the selected HTTP authentication implementation type.</summary>
    public Type AuthenticationType { get; set; }

    /// <summary>Calculates the HTTP body limit required for a serialized envelope.</summary>
    /// <param name="maximumPayloadBytes">The maximum raw payload size in bytes.</param>
    /// <returns>The corresponding maximum request-body size in bytes.</returns>
    /// <example><code>var limit = options.GetRequestBodyLimit(65_536);</code></example>
    public long GetRequestBodyLimit(long maximumPayloadBytes) =>
        checked(16_384L + (4L * ((maximumPayloadBytes + 2L) / 3L)));
}

/// <summary>Fluently configures one shared <see cref="BroadcastingHttpOptions"/> instance.</summary>
/// <example><code>var builder = new BroadcastingHttpOptionsBuilder(options).ReceiverRoute("/_bdk/api/broadcasting");</code></example>
public sealed class BroadcastingHttpOptionsBuilder
{
    /// <summary>Creates a builder that updates the supplied HTTP options instance.</summary>
    /// <param name="target">The HTTP options instance to update.</param>
    /// <example><code>var builder = new BroadcastingHttpOptionsBuilder(options);</code></example>
    public BroadcastingHttpOptionsBuilder(BroadcastingHttpOptions target)
    {
        this.Target = target ?? throw new ArgumentNullException(nameof(target));
    }

    private BroadcastingHttpOptions Target { get; }

    /// <summary>Sets the process-specific advertised base or receiver address.</summary>
    public BroadcastingHttpOptionsBuilder AdvertisedAddress(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            this.Target.AdvertisedAddress = null;
            return this;
        }

        if (
            !Uri.TryCreate(address, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https")
            || uri.Host is "0.0.0.0" or "::" or "[::]" or "*" or "+"
            || !string.IsNullOrEmpty(uri.UserInfo)
            || uri.AbsoluteUri.Length > 2048
        )
        {
            throw new ArgumentException(
                "The advertised address must identify one concrete HTTP or HTTPS process.",
                nameof(address)
            );
        }

        this.Target.AdvertisedAddress = uri;
        return this;
    }

    /// <summary>Sets the receiver route below the host address.</summary>
    public BroadcastingHttpOptionsBuilder ReceiverRoute(string route)
    {
        if (string.IsNullOrWhiteSpace(route))
        {
            throw new ArgumentException("The receiver route is required.", nameof(route));
        }

        this.Target.ReceiverRoute = $"/{route.Trim().Trim('/')}";
        return this;
    }

    /// <summary>
    /// Selects built-in shared-secret authentication; null, empty, and whitespace values are valid.
    /// </summary>
    public BroadcastingHttpOptionsBuilder SharedSecret(string secret = null)
    {
        if (
            this.Target.AuthenticationType is not null
            && this.Target.AuthenticationType != typeof(AllowAllBroadcastHttpAuthentication)
            && this.Target.AuthenticationType != typeof(SharedSecretBroadcastHttpAuthentication)
        )
        {
            throw new InvalidOperationException(
                "A different explicit broadcast HTTP authentication is already selected."
            );
        }

        this.Target.SharedSecretEnabled = true;
        this.Target.SharedSecret = secret ?? string.Empty;
        this.Target.AuthenticationType = typeof(SharedSecretBroadcastHttpAuthentication);
        return this;
    }
}
