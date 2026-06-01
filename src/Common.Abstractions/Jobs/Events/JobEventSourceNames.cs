// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

/// <summary>
/// Provides the built-in logical event-source names.
/// </summary>
public static class JobEventSourceNames
{
    /// <summary>
    /// Identifies events accepted from the notifier integration.
    /// </summary>
    public const string Notifier = "notifier";

    /// <summary>
    /// Identifies events accepted from the messaging integration.
    /// </summary>
    public const string Messaging = "messaging";

    /// <summary>
    /// Identifies events accepted from the queueing integration.
    /// </summary>
    public const string Queueing = "queueing";
}
