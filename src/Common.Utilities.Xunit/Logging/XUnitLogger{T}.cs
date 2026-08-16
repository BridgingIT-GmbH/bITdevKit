// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

/// <summary>
///     Writes log events categorized by <typeparamref name="T"/> to xUnit test output.
/// </summary>
/// <typeparam name="T">The logger category type.</typeparam>
/// <param name="output">The xUnit output sink.</param>
/// <param name="scopeProvider">The provider that tracks active logging scopes.</param>
public sealed class XunitLogger<T>(ITestOutputHelper output, LoggerExternalScopeProvider scopeProvider)
    : XunitLogger(output, scopeProvider, typeof(T).FullName), ILogger<T>
{ }
