// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using BridgingIT.DevKit.Application.Entities;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Authorizes requester/query-level ChangeHistory reads outside HTTP endpoint policy checks.
/// </summary>
/// <typeparam name="TContext">The EF Core context that stores ChangeHistory rows.</typeparam>
/// <example>
/// <code>
/// services.AddChangeHistory()
///     .WithReadAuthorizer&lt;AppDbContext, AppHistoryReadAuthorizer&gt;();
/// </code>
/// </example>
public interface IChangeHistoryReadAuthorizer<TContext>
    where TContext : DbContext
{
    /// <summary>
    /// Authorizes a flat or grouped ChangeHistory read request.
    /// </summary>
    /// <param name="context">The authorization context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A success result when authorized; otherwise a failure result.</returns>
    Task<Result> AuthorizeAsync(
        ChangeHistoryReadAuthorizationContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Authorizes requester-level ChangeHistory restore requests before restore command execution.
/// </summary>
/// <typeparam name="TEntity">The entity type being restored.</typeparam>
/// <typeparam name="TContext">The EF Core context that stores ChangeHistory rows.</typeparam>
/// <example>
/// <code>
/// services.AddChangeHistory(options =&gt; options.Track&lt;Customer&gt;())
///     .WithRestoreRequestAuthorizer&lt;Customer, AppDbContext, CustomerHistoryRestoreAuthorizer&gt;();
/// </code>
/// </example>
public interface IChangeHistoryRestoreRequestAuthorizer<TEntity, TContext>
    where TEntity : class, IEntity
    where TContext : DbContext
{
    /// <summary>
    /// Authorizes a ChangeHistory restore requester call.
    /// </summary>
    /// <param name="context">The authorization context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A success result when authorized; otherwise a failure result.</returns>
    Task<Result> AuthorizeAsync(
        ChangeHistoryRestoreRequestAuthorizationContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Contains metadata for requester/query-level ChangeHistory read authorization.
/// </summary>
/// <example>
/// <code>
/// if (context.Policy == "History.Read") return Result.Success();
/// </code>
/// </example>
public sealed class ChangeHistoryReadAuthorizationContext
{
    /// <summary>
    /// Gets or sets the configured global read authorization policy name.
    /// </summary>
    public string Policy { get; set; }

    /// <summary>
    /// Gets or sets the requested entity type filter.
    /// </summary>
    public string EntityType { get; set; }

    /// <summary>
    /// Gets or sets the requested entity id filter.
    /// </summary>
    public string EntityId { get; set; }

    /// <summary>
    /// Gets or sets the requested change set id filter.
    /// </summary>
    public Guid? ChangeSetId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether serialized values were requested.
    /// </summary>
    public bool IncludeValues { get; set; }
}

/// <summary>
/// Contains metadata for requester-level ChangeHistory restore authorization.
/// </summary>
/// <example>
/// <code>
/// return context.Policy == "History.Restore" ? Result.Success() : Result.Failure();
/// </code>
/// </example>
public sealed class ChangeHistoryRestoreRequestAuthorizationContext
{
    /// <summary>
    /// Gets or sets the configured global restore authorization policy name.
    /// </summary>
    public string Policy { get; set; }

    /// <summary>
    /// Gets or sets the entity type being restored.
    /// </summary>
    public Type EntityType { get; set; }

    /// <summary>
    /// Gets or sets the entity id being restored.
    /// </summary>
    public object EntityId { get; set; }

    /// <summary>
    /// Gets or sets the source change set id.
    /// </summary>
    public Guid ChangeSetId { get; set; }
}

internal static class ChangeHistoryAuthorization
{
    public static Task<Result> AuthorizeReadAsync<TContext>(
        ChangeHistoryOptions options,
        IChangeHistoryReadAuthorizer<TContext> authorizer,
        ChangeHistoryFindAllRequest<TContext> request,
        Guid? changeSetId,
        CancellationToken cancellationToken)
        where TContext : DbContext
        => AuthorizeReadAsync(
            options,
            authorizer,
            request.EntityType,
            request.EntityId,
            changeSetId,
            request.IncludeValues,
            cancellationToken);

    public static Task<Result> AuthorizeReadAsync<TContext>(
        ChangeHistoryOptions options,
        IChangeHistoryReadAuthorizer<TContext> authorizer,
        string entityType,
        string entityId,
        Guid? changeSetId,
        bool includeValues,
        CancellationToken cancellationToken)
        where TContext : DbContext
    {
        if (authorizer is null)
        {
            return Task.FromResult(Result.Success());
        }

        return authorizer.AuthorizeAsync(new ChangeHistoryReadAuthorizationContext
        {
            Policy = options?.ReadAuthorizationPolicy,
            EntityType = entityType,
            EntityId = entityId,
            ChangeSetId = changeSetId,
            IncludeValues = includeValues
        }, cancellationToken);
    }

    public static Task<Result> AuthorizeRestoreAsync<TEntity, TContext>(
        ChangeHistoryOptions options,
        IChangeHistoryRestoreRequestAuthorizer<TEntity, TContext> authorizer,
        ChangeHistoryRestoreRequest<TEntity, TContext> request,
        CancellationToken cancellationToken)
        where TEntity : class, IEntity
        where TContext : DbContext
    {
        if (authorizer is null)
        {
            return Task.FromResult(Result.Success());
        }

        return authorizer.AuthorizeAsync(new ChangeHistoryRestoreRequestAuthorizationContext
        {
            Policy = options?.RestoreAuthorizationPolicy,
            EntityType = typeof(TEntity),
            EntityId = request.EntityId,
            ChangeSetId = request.ChangeSetId
        }, cancellationToken);
    }

    public static Task<Result> AuthorizeRestoreAsync<TEntity, TContext>(
        ChangeHistoryOptions options,
        IChangeHistoryRestoreRequestAuthorizer<TEntity, TContext> authorizer,
        Type entityType,
        object entityId,
        Guid changeSetId,
        CancellationToken cancellationToken)
        where TEntity : class, IEntity
        where TContext : DbContext
    {
        if (authorizer is null)
        {
            return Task.FromResult(Result.Success());
        }

        return authorizer.AuthorizeAsync(new ChangeHistoryRestoreRequestAuthorizationContext
        {
            Policy = options?.RestoreAuthorizationPolicy,
            EntityType = entityType,
            EntityId = entityId,
            ChangeSetId = changeSetId
        }, cancellationToken);
    }
}
