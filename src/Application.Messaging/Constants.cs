// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

/// <summary>
/// Represents constants.
/// </summary>
public struct Constants
{
    /// <summary>
    /// Defines the log key value.
    /// </summary>
    public const string LogKey = "MSG";

    /// <summary>
    /// Defines the correlation id key value.
    /// </summary>
    public const string CorrelationIdKey = "CorrelationId";

    /// <summary>
    /// Defines the flow id key value.
    /// </summary>
    public const string FlowIdKey = "FlowId";

    /// <summary>
    /// Defines the timestamp key value.
    /// </summary>
    public const string TimestampKey = "Timestamp";

    /// <summary>
    /// Defines the trace operation publish name value.
    /// </summary>
    public const string TraceOperationPublishName = "MESSAGE_PUBLISH";

    /// <summary>
    /// Defines the trace operation handle name value.
    /// </summary>
    public const string TraceOperationHandleName = "MESSAGE_HANDLE";
}
