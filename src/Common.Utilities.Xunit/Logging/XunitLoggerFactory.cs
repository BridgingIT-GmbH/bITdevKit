// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

public static class XunitLoggerFactory
{
    public static ILoggerFactory Create(ITestOutputHelper output)
    {
        var factory = new LoggerFactory();
        var provider = new XunitLoggerProvider(output);
        factory.AddProvider(provider);

        return factory;
    }
}