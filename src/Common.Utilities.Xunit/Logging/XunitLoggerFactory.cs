// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

/// <summary>
///     Creates logger factories that write through an xUnit output helper.
/// </summary>
public static class XunitLoggerFactory
{
    /// <summary>
    ///     Creates a logger factory with an <see cref="XunitLoggerProvider"/>.
    /// </summary>
    /// <param name="output">The xUnit output sink.</param>
    /// <returns>The created logger factory.</returns>
    public static ILoggerFactory Create(ITestOutputHelper output)
    {
        var factory = new LoggerFactory();
        var provider = new XunitLoggerProvider(output);
        factory.AddProvider(provider);

        return factory;
    }
}
