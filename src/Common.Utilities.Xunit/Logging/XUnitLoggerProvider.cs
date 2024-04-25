// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

public sealed class XunitLoggerProvider : ILoggerProvider
{
    private readonly ITestOutputHelper output;
    private readonly LoggerExternalScopeProvider scopeProvider = new();

    public XunitLoggerProvider(ITestOutputHelper output)
    {
        this.output = output;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new XunitLogger(this.output, this.scopeProvider, categoryName);
    }

    public void Dispose()
    {
    }
}