// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Common;

/// <summary>Requests the cache-backed task-title suggestion catalog.</summary>
/// <example><code>var result = await requester.SendAsync(new TaskLookupsQuery(), cancellationToken: cancellationToken);</code></example>
public sealed class TaskLookupsQuery : RequestBase<IReadOnlyList<TaskLookupModel>>
{
}

/// <summary>Obtains task-title suggestions from the application's declared lookup collection.</summary>
/// <param name="catalog">The cache-backed reference catalog.</param>
/// <example><code>await requester.SendAsync(new TaskLookupsQuery(), cancellationToken: cancellationToken);</code></example>
public sealed class TaskLookupsQueryHandler(TaskLookupCatalog catalog) : RequestHandlerBase<TaskLookupsQuery, IReadOnlyList<TaskLookupModel>>
{
    /// <inheritdoc />
    protected override async Task<Result<IReadOnlyList<TaskLookupModel>>> HandleAsync(TaskLookupsQuery request, SendOptions options, CancellationToken cancellationToken) =>
        await catalog.GetAllAsync(cancellationToken);
}