// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

public sealed class XunitLogger<T> : XunitLogger, ILogger<T>
{
    public XunitLogger(
        ITestOutputHelper output,
        LoggerExternalScopeProvider scopeProvider)
        : base(output, scopeProvider, typeof(T).FullName)
    {
    }
}