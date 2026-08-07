// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Presentation.Web.Server.Modules.Core;

using BridgingIT.DevKit.Application.Identity;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Examples.DoFiesta.Domain.Model;
using BridgingIT.DevKit.Examples.DoFiesta.Infrastructure;
using BridgingIT.DevKit.Infrastructure.EntityFramework;

/// <summary>
/// Authorizes ChangeHistory read requests for the DoFiesta Core module.
/// </summary>
/// <example>
/// <code>
/// services.AddChangeHistory()
///     .WithReadAuthorizer&lt;CoreDbContext, CoreChangeHistoryReadAuthorizer&gt;();
/// </code>
/// </example>
public sealed class CoreChangeHistoryReadAuthorizer(
    ICurrentUserAccessor currentUserAccessor,
    IEntityPermissionEvaluator<TodoItem> todoItemPermissionEvaluator,
    IEntityPermissionEvaluator<Subscription> subscriptionPermissionEvaluator)
    : IChangeHistoryReadAuthorizer<CoreDbContext>
{
    /// <inheritdoc />
    public async Task<Result> AuthorizeAsync(
        ChangeHistoryReadAuthorizationContext context,
        CancellationToken cancellationToken = default)
    {
        var hasPermission = context.EntityType switch
        {
            nameof(TodoItem) => await this.HasPermissionAsync(todoItemPermissionEvaluator, context.EntityId, cancellationToken),
            nameof(Subscription) => await this.HasPermissionAsync(subscriptionPermissionEvaluator, context.EntityId, cancellationToken),
            _ => false
        };

        return Result.SuccessIf(hasPermission, new UnauthorizedError());
    }

    private async Task<bool> HasPermissionAsync<TEntity>(
        IEntityPermissionEvaluator<TEntity> permissionEvaluator,
        string entityId,
        CancellationToken cancellationToken)
        where TEntity : class, IEntity
        => string.IsNullOrWhiteSpace(entityId)
            ? await permissionEvaluator.HasPermissionAsync(currentUserAccessor, Permission.List, cancellationToken: cancellationToken)
            : await permissionEvaluator.HasPermissionAsync(currentUserAccessor, entityId, Permission.Read, cancellationToken: cancellationToken);
}

/// <summary>
/// Authorizes TodoItem ChangeHistory restore requests before command execution.
/// </summary>
/// <example>
/// <code>
/// services.AddChangeHistory(options =&gt; options.Track&lt;TodoItem&gt;())
///     .WithRestoreRequestAuthorizer&lt;TodoItem, CoreDbContext, TodoItemChangeHistoryRestoreRequestAuthorizer&gt;();
/// </code>
/// </example>
public sealed class TodoItemChangeHistoryRestoreRequestAuthorizer(
    ICurrentUserAccessor currentUserAccessor,
    IEntityPermissionEvaluator<TodoItem> permissionEvaluator)
    : IChangeHistoryRestoreRequestAuthorizer<TodoItem, CoreDbContext>
{
    /// <inheritdoc />
    public async Task<Result> AuthorizeAsync(
        ChangeHistoryRestoreRequestAuthorizationContext context,
        CancellationToken cancellationToken = default)
        => Result.SuccessIf(
            await permissionEvaluator.HasPermissionAsync(currentUserAccessor, context.EntityId, Permission.Write, cancellationToken: cancellationToken),
            new UnauthorizedError());
}

/// <summary>
/// Authorizes Subscription ChangeHistory restore requests before command execution.
/// </summary>
/// <example>
/// <code>
/// services.AddChangeHistory(options =&gt; options.Track&lt;Subscription&gt;())
///     .WithRestoreRequestAuthorizer&lt;Subscription, CoreDbContext, SubscriptionChangeHistoryRestoreRequestAuthorizer&gt;();
/// </code>
/// </example>
public sealed class SubscriptionChangeHistoryRestoreRequestAuthorizer(
    ICurrentUserAccessor currentUserAccessor,
    IEntityPermissionEvaluator<Subscription> permissionEvaluator)
    : IChangeHistoryRestoreRequestAuthorizer<Subscription, CoreDbContext>
{
    /// <inheritdoc />
    public async Task<Result> AuthorizeAsync(
        ChangeHistoryRestoreRequestAuthorizationContext context,
        CancellationToken cancellationToken = default)
        => Result.SuccessIf(
            await permissionEvaluator.HasPermissionAsync(currentUserAccessor, context.EntityId, Permission.Write, cancellationToken: cancellationToken),
            new UnauthorizedError());
}

/// <summary>
/// Authorizes TodoItem restore execution after the current entity has been loaded.
/// </summary>
/// <example>
/// <code>
/// options.Track&lt;TodoItem&gt;().UseRestoreAuthorizer&lt;TodoItemChangeHistoryRestoreAuthorizer&gt;();
/// </code>
/// </example>
public sealed class TodoItemChangeHistoryRestoreAuthorizer(
    ICurrentUserAccessor currentUserAccessor,
    IEntityPermissionEvaluator<TodoItem> permissionEvaluator)
    : IChangeHistoryRestoreAuthorizer<TodoItem>
{
    /// <inheritdoc />
    public async Task<Result> AuthorizeAsync(
        TodoItem entity,
        ChangeHistoryRestoreAuthorizationContext context,
        CancellationToken cancellationToken = default)
        => Result.SuccessIf(
            await permissionEvaluator.HasPermissionAsync(currentUserAccessor, entity.Id, Permission.Write, cancellationToken: cancellationToken),
            new UnauthorizedError());
}

/// <summary>
/// Authorizes Subscription restore execution after the current entity has been loaded.
/// </summary>
/// <example>
/// <code>
/// options.Track&lt;Subscription&gt;().UseRestoreAuthorizer&lt;SubscriptionChangeHistoryRestoreAuthorizer&gt;();
/// </code>
/// </example>
public sealed class SubscriptionChangeHistoryRestoreAuthorizer(
    ICurrentUserAccessor currentUserAccessor,
    IEntityPermissionEvaluator<Subscription> permissionEvaluator)
    : IChangeHistoryRestoreAuthorizer<Subscription>
{
    /// <inheritdoc />
    public async Task<Result> AuthorizeAsync(
        Subscription entity,
        ChangeHistoryRestoreAuthorizationContext context,
        CancellationToken cancellationToken = default)
        => Result.SuccessIf(
            await permissionEvaluator.HasPermissionAsync(currentUserAccessor, entity.Id, Permission.Write, cancellationToken: cancellationToken),
            new UnauthorizedError());
}
