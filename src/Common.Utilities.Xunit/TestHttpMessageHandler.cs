// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System;
using System.Threading.Tasks;
using System.Net.Http;

/// <summary>
/// A test utility class for unit testing HTTP-based services.
/// <para>
/// <b>TestHttpMessageHandler</b> allows you to inject custom HTTP responses
/// when using <see cref="HttpClient"/> in unit tests. This makes it possible
/// to simulate various HTTP scenarios (success, error, etc) without making
/// actual network calls or using a mocking framework for <see cref="HttpClient"/>.
/// </para>
/// <para>
/// Usage example:
/// <code>
/// var handler = new TestHttpMessageHandler(request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
/// var httpClient = new HttpClient(handler);
/// </code>
/// </para>
/// </summary>
public class TestHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> handlerFunc;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestHttpMessageHandler"/> class.
    /// </summary>
    /// <param name="handlerFunc">
    /// A delegate that receives the <see cref="HttpRequestMessage"/> and returns a <see cref="Task{HttpResponseMessage}"/>.
    /// Use this to define the response logic for HTTP requests in your tests.
    /// </param>
    public TestHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handlerFunc)
    {
        this.handlerFunc = handlerFunc ?? throw new ArgumentNullException(nameof(handlerFunc));
    }

    /// <summary>
    /// Sends an HTTP request as an asynchronous operation using the custom handler logic provided.
    /// </summary>
    /// <param name="request">The HTTP request message to send.</param>
    /// <param name="cancellationToken">A cancellation token to cancel operation.</param>
    /// <returns>A task that represents the asynchronous operation, containing the HTTP response.</returns>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
        => this.handlerFunc(request);
}