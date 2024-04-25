// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Client;

using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

public class ApiClientBase
{
    public string BearerToken { get; private set; }

    public void SetBearerToken(string token)
    {
        this.BearerToken = token;
    }

    protected Task<HttpRequestMessage> CreateHttpRequestMessageAsync(CancellationToken cancellationToken)
    {
        var message = new HttpRequestMessage();
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", this.BearerToken);

        return Task.FromResult(message);
    }
}
