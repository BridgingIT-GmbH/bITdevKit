// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Application.Modules.Core;

using System.Runtime.CompilerServices;
using BridgingIT.DevKit.Application.Identity;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Examples.DoFiesta.Domain.Model;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Prepares an authorized finite Todo stream with detached, bounded pages.</summary>
/// <example><code>var result = await requester.SendAsync(new TodoItemStreamQuery(), cancellationToken: ct);</code></example>
[Query]
[HandlerTimeout(5000)]
public partial class TodoItemStreamQuery
{
    [Handle]
    private async Task<Result<IAsyncEnumerable<TodoItemModel>>> HandleAsync(
        IServiceScopeFactory scopes,
        ICurrentUserAccessor currentUser,
        IEntityPermissionEvaluator<TodoItem> permissions,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId;
        var roles = currentUser.Roles?.ToArray() ?? [];
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(userId))
        {
            return Result<IAsyncEnumerable<TodoItemModel>>.Failure().WithError(new UnauthorizedError());
        }

        if (!await permissions.HasPermissionAsync(userId, roles, Permission.List, cancellationToken: cancellationToken))
        {
            return Result<IAsyncEnumerable<TodoItemModel>>.Failure().WithError(new ForbiddenError());
        }

        return Result<IAsyncEnumerable<TodoItemModel>>.Success(StreamAsync(scopes, userId, roles));
    }

    private static async IAsyncEnumerable<TodoItemModel> StreamAsync(IServiceScopeFactory scopes, string userId, string[] roles, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        const int BATCH_SIZE = 100;
        var offset = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            List<TodoItemModel> page = [];
            int sourceCount;
            await using (var scope = scopes.CreateAsyncScope())
            {
                var repository = scope.ServiceProvider.GetRequiredService<IGenericRepository<TodoItem>>();
                var mapper = scope.ServiceProvider.GetRequiredService<IMapper>();
                var permissions = scope.ServiceProvider.GetRequiredService<IEntityPermissionEvaluator<TodoItem>>();
                var options = new FindOptions<TodoItem> { NoTracking = true, Skip = offset, Take = BATCH_SIZE, Order = new(e => e.Id) };
                var entities = (await repository.FindAllAsync(options, cancellationToken)).ToList();
                sourceCount = entities.Count;
                foreach (var entity in entities)
                {
                    if (await permissions.HasPermissionAsync(userId, roles, entity.Id, Permission.Read, cancellationToken: cancellationToken))
                    {
                        page.Add(mapper.Map<TodoItem, TodoItemModel>(entity));
                    }
                }
            }

            foreach (var model in page)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return model;
            }

            if (sourceCount < BATCH_SIZE) { yield break; }

            offset = checked(offset + sourceCount);
        }
    }
}
