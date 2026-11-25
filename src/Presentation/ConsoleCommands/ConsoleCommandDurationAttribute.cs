// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

/// <summary>Enables friendly invariant duration parsing for a TimeSpan option or argument.</summary>
/// <remarks>Applies to TimeSpan and nullable TimeSpan properties. Supports standard invariant TimeSpan text and the ms, s, m, and h suffixes.</remarks>
/// <example><code>[ConsoleCommandOption("timeout"), ConsoleCommandDuration]
/// public TimeSpan Timeout { get; set; }</code></example>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class ConsoleCommandDurationAttribute : Attribute
{
}
