// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

/// <summary>
/// Represents an internal export control-flow exception raised when a row interceptor aborts an export operation.
/// </summary>
/// <remarks>
/// This exception is translated into a public <see cref="ExportInterceptionAbortedError"/> by <see cref="DataPorterService"/>.
/// </remarks>
/// <param name="message">The abort reason.</param>
public sealed class ExportInterceptionAbortedException(string message) : Exception(message)
{
}
