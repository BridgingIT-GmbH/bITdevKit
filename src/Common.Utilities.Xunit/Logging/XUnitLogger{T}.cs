// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

public sealed class XunitLogger<T>(
    ITestOutputHelper output,
    LoggerExternalScopeProvider scopeProvider) : XunitLogger(output, scopeProvider, typeof(T).FullName), ILogger<T>
{
}