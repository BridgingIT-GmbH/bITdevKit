// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

/// <summary>
/// Defines common context-property keys used by event-source adapters.
/// </summary>
public static class JobEventContextPropertyNames
{
    /// <summary>
    /// Identifies the event source id stored on job execution context properties.
    /// </summary>
    public const string SourceId = "jobs.event.sourceId";

    /// <summary>
    /// Identifies the idempotency key stored on job execution context properties.
    /// </summary>
    public const string IdempotencyKey = "jobs.event.idempotencyKey";

    /// <summary>
    /// Identifies the correlation id stored on job execution context properties.
    /// </summary>
    public const string CorrelationId = "jobs.event.correlationId";
}
