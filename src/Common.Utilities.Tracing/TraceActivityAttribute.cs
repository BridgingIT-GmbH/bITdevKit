// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
///     Selects a method for activity tracing and optionally controls its activity name and exception recording.
/// </summary>
/// <param name="name">An explicit activity name, or <see langword="null"/> to use the configured naming schema.</param>
/// <param name="recordExceptions">Whether exceptions raised by the invocation are recorded on the activity.</param>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public class TraceActivityAttribute(string name = null, bool recordExceptions = true) : Attribute
{
    /// <summary>
    ///     Gets the explicit activity name, when configured.
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    ///     Gets a value indicating whether invocation exceptions are recorded on the activity.
    /// </summary>
    public bool RecordExceptions { get; } = recordExceptions;
}
