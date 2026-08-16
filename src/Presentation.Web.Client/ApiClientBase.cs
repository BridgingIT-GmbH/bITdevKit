// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Client;

using System.Net.Http.Headers;

/// <summary>
/// Represents api client base.
/// </summary>
public class ApiClientBase
{
    /// <summary>
    /// Gets or sets the bearer token.
    /// </summary>
    public string BearerToken { get; private set; }

    /// <summary>
    /// Executes the set bearer token operation.
    /// </summary>
    /// <param name="token">The token used by the operation.</param>
    public void SetBearerToken(string token)
    {
        this.BearerToken = token;
    }

    /// <summary>
    /// Creates http request message.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected Task<HttpRequestMessage> CreateHttpRequestMessageAsync(CancellationToken cancellationToken)
    {
        var message = new HttpRequestMessage();
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", this.BearerToken);

        return Task.FromResult(message);
    }
}
