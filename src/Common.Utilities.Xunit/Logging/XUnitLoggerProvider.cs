// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

/// <summary>
///     Creates xUnit loggers that share one external-scope provider.
/// </summary>
/// <param name="output">The xUnit output sink used by created loggers.</param>
public sealed class XunitLoggerProvider(ITestOutputHelper output) : ILoggerProvider
{
    private readonly ITestOutputHelper output = output;
    private readonly LoggerExternalScopeProvider scopeProvider = new();

    /// <inheritdoc/>
    public ILogger CreateLogger(string categoryName)
    {
        return new XunitLogger(this.output, this.scopeProvider, categoryName);
    }

    /// <inheritdoc/>
    public void Dispose() { }
}
