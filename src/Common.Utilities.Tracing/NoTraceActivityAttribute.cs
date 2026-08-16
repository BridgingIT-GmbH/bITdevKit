// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
///     Excludes a decorated method from activity tracing by <see cref="TraceActivityDecorator{TDecorated}"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public class NoTraceActivityAttribute : Attribute;
