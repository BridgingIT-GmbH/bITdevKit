// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using BridgingIT.DevKit.Common;

/// <summary>
/// Accepts external events into the provider-neutral event-trigger pipeline.
/// </summary>
public interface IJobEventIngress
{
    /// <summary>
    /// Durably accepts an event so it can later materialize job occurrences during the normal scheduler sweep.
    /// </summary>
    Task<IResult<JobAcceptedEvent>> AcceptAsync(
        string source,
        object data,
        Type dataType,
        JobAcceptedEventOptions options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Durably accepts an event so it can later materialize job occurrences during the normal scheduler sweep.
    /// </summary>
    Task<IResult<JobAcceptedEvent>> AcceptAsync<TEvent>(
        string source,
        TEvent data,
        JobAcceptedEventOptions options = null,
        CancellationToken cancellationToken = default)
        where TEvent : class;
}
