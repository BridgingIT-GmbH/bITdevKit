// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Infrastructure;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Application.Orchestrations;
using BridgingIT.DevKit.Examples.DoFiesta.Application.Modules.Core;
using BridgingIT.DevKit.Examples.DoFiesta.Domain.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

/// <summary>
/// Coordinates durable orchestration lifecycle operations for todo items.
/// </summary>
/// <remarks>
/// This component ensures that each todo item has a corresponding orchestration instance,
/// forwards todo updates as durable signals, and notifies the orchestration when an item is deleted.
/// </remarks>
public class TodoItemOrchestrationCoordinator(
    CoreDbContext dbContext,
    IOrchestrationService orchestrationService,
    ILoggerFactory loggerFactory) : ITodoItemOrchestrationCoordinator
{
    private const string OrchestrationInstanceIdPropertyKey = "orchestration.instanceId";
    private const string OrchestrationNamePropertyKey = "orchestration.name";
    private const string OrchestrationCreatedUtcPropertyKey = "orchestration.createdUtc";
    private const string OrchestrationUpdatedUtcPropertyKey = "orchestration.updatedUtc";

    private readonly CoreDbContext dbContext = dbContext;
    private readonly IOrchestrationService orchestrationService = orchestrationService;
    private readonly ILogger<TodoItemOrchestrationCoordinator> logger = loggerFactory.CreateLogger<TodoItemOrchestrationCoordinator>();

    /// <summary>
    /// Ensures that the specified todo item has a running orchestration instance.
    /// </summary>
    /// <param name="todoItem">The todo item to associate with an orchestration instance.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task EnsureStartedAsync(TodoItem todoItem, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(todoItem);

        var todoItemId = todoItem.Id?.Value ?? Guid.Empty;
        if (todoItemId == Guid.Empty)
        {
            return;
        }

        var persistedTodoItem = await this.FindTodoItemAsync(todoItemId, cancellationToken);
        var orchestrationInstanceId = GetOrchestrationInstanceId(todoItem.Properties) ?? GetOrchestrationInstanceId(persistedTodoItem?.Properties);

        if (orchestrationInstanceId.HasValue)
        {
            await this.SynchronizeAsync(todoItem, cancellationToken);
            return;
        }

        var dispatch = await this.orchestrationService.DispatchAsync<TodoItemLifecycleOrchestration, TodoItemLifecycleOrchestrationData>(
            MapData(todoItem),
            cancellationToken);

        if (dispatch.IsFailure)
        {
            this.logger.LogWarning(
                "DoFiesta orchestration could not be started for todo item {TodoItemId}: {Error}",
                todoItemId,
                dispatch.Errors.FirstOrDefault()?.Message);
            return;
        }

        var createdUtc = DateTimeOffset.UtcNow;
        SetOrchestrationMetadata(todoItem.Properties, dispatch.Value, createdUtc, createdUtc);

        if (persistedTodoItem is not null && !ReferenceEquals(persistedTodoItem, todoItem))
        {
            SetOrchestrationMetadata(persistedTodoItem.Properties, dispatch.Value, createdUtc, createdUtc);
        }

        if (persistedTodoItem is not null)
        {
            await this.dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Synchronizes the orchestration instance with the latest todo item state.
    /// </summary>
    /// <param name="todoItem">The todo item whose current state should be signaled to the orchestration.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task SynchronizeAsync(TodoItem todoItem, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(todoItem);

        var todoItemId = todoItem.Id?.Value ?? Guid.Empty;
        if (todoItemId == Guid.Empty)
        {
            return;
        }

        var persistedTodoItem = await this.FindTodoItemAsync(todoItemId, cancellationToken);
        var orchestrationInstanceId = GetOrchestrationInstanceId(todoItem.Properties) ?? GetOrchestrationInstanceId(persistedTodoItem?.Properties);

        if (!orchestrationInstanceId.HasValue)
        {
            await this.EnsureStartedAsync(todoItem, cancellationToken);
            return;
        }

        var result = await this.orchestrationService.SignalAsync(
            orchestrationInstanceId.Value,
            TodoItemLifecycleSignals.Updated,
            MapUpdateSignal(todoItem),
            cancellationToken: cancellationToken);

        if (result.IsFailure)
        {
            this.logger.LogWarning(
                "DoFiesta orchestration could not be synchronized for todo item {TodoItemId}: {Error}",
                todoItemId,
                result.Errors.FirstOrDefault()?.Message);
            return;
        }

        var updatedUtc = DateTimeOffset.UtcNow;
        UpdateOrchestrationTimestamp(todoItem.Properties, updatedUtc);

        if (persistedTodoItem is not null && !ReferenceEquals(persistedTodoItem, todoItem))
        {
            UpdateOrchestrationTimestamp(persistedTodoItem.Properties, updatedUtc);
        }

        if (persistedTodoItem is not null)
        {
            await this.dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Signals that the specified todo item has been deleted.
    /// </summary>
    /// <param name="todoItem">The deleted todo item.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task HandleDeletedAsync(TodoItem todoItem, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(todoItem);

        var todoItemId = todoItem.Id?.Value ?? Guid.Empty;
        if (todoItemId == Guid.Empty)
        {
            return;
        }

        var orchestrationInstanceId = GetOrchestrationInstanceId(todoItem.Properties);
        if (!orchestrationInstanceId.HasValue)
        {
            return;
        }

        var result = await this.orchestrationService.SignalAsync(
            orchestrationInstanceId.Value,
            TodoItemLifecycleSignals.Deleted,
            new TodoItemLifecycleDeletedSignal
            {
                Reason = "Todo item was deleted.",
                DeletedBy = todoItem.UserId,
                DeletedUtc = DateTime.UtcNow,
            },
            cancellationToken: cancellationToken);

        if (result.IsFailure)
        {
            this.logger.LogWarning(
                "DoFiesta orchestration could not be deleted for todo item {TodoItemId}: {Error}",
                todoItemId,
                result.Errors.FirstOrDefault()?.Message);
            return;
        }
    }

    private async Task<TodoItem> FindTodoItemAsync(Guid todoItemId, CancellationToken cancellationToken)
    {
        var trackedTodoItem = this.dbContext.ChangeTracker
            .Entries<TodoItem>()
            .FirstOrDefault(entry => entry.Entity.Id?.Value == todoItemId)
            ?.Entity;

        if (trackedTodoItem is not null)
        {
            return trackedTodoItem;
        }

        return await this.dbContext.TodoItems
            .SingleOrDefaultAsync(item => item.Id == TodoItemId.Create(todoItemId), cancellationToken);
    }

    private static Guid? GetOrchestrationInstanceId(PropertyBag properties)
    {
        return properties is not null &&
               properties.TryGet<string>(OrchestrationInstanceIdPropertyKey, out var value) &&
               Guid.TryParse(value, out var instanceId)
            ? instanceId
            : null;
    }

    private static DateTimeOffset? GetOrchestrationCreatedUtc(PropertyBag properties)
    {
        return properties is not null &&
               properties.TryGet<string>(OrchestrationCreatedUtcPropertyKey, out var value) &&
               DateTimeOffset.TryParse(value, out var createdUtc)
            ? createdUtc
            : null;
    }

    private static void SetOrchestrationMetadata(PropertyBag properties, Guid orchestrationInstanceId, DateTimeOffset createdUtc, DateTimeOffset updatedUtc)
    {
        properties ??= [];
        properties.Set(OrchestrationInstanceIdPropertyKey, orchestrationInstanceId.ToString("D"));
        properties.Set(OrchestrationNamePropertyKey, nameof(TodoItemLifecycleOrchestration));
        properties.Set(OrchestrationCreatedUtcPropertyKey, createdUtc.ToString("O"));
        properties.Set(OrchestrationUpdatedUtcPropertyKey, updatedUtc.ToString("O"));
    }

    private static void UpdateOrchestrationTimestamp(PropertyBag properties, DateTimeOffset updatedUtc)
    {
        if (properties is null || !properties.Contains(OrchestrationInstanceIdPropertyKey))
        {
            return;
        }

        properties.Set(OrchestrationUpdatedUtcPropertyKey, updatedUtc.ToString("O"));
        properties.Set(
            OrchestrationCreatedUtcPropertyKey,
            (GetOrchestrationCreatedUtc(properties) ?? updatedUtc).ToString("O"));
        properties.Set(OrchestrationNamePropertyKey, nameof(TodoItemLifecycleOrchestration));
    }

    private static TodoItemLifecycleOrchestrationData MapData(TodoItem todoItem)
    {
        return new TodoItemLifecycleOrchestrationData
        {
            TodoItemId = todoItem.Id?.Value ?? Guid.Empty,
            Number = todoItem.Number,
            Title = todoItem.Title,
            Description = todoItem.Description,
            Assignee = todoItem.Assignee?.Value,
            Status = todoItem.Status?.Value,
            Priority = todoItem.Priority?.Value,
            DueDate = NormalizeUtc(todoItem.DueDate),
            LastSynchronizedBy = todoItem.UserId,
            LastSynchronizedUtc = DateTime.UtcNow,
        };
    }

    private static TodoItemLifecycleUpdateSignal MapUpdateSignal(TodoItem todoItem)
    {
        return new TodoItemLifecycleUpdateSignal
        {
            Title = todoItem.Title,
            Description = todoItem.Description,
            Assignee = todoItem.Assignee?.Value,
            Status = todoItem.Status?.Value,
            Priority = todoItem.Priority?.Value,
            DueDate = NormalizeUtc(todoItem.DueDate),
            SynchronizedBy = todoItem.UserId,
            SynchronizedUtc = DateTime.UtcNow,
        };
    }

    private static DateTime? NormalizeUtc(DateTime? value)
    {
        return value.HasValue ? NormalizeUtc(value.Value) : null;
    }

    private static DateTime NormalizeUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };
    }
}