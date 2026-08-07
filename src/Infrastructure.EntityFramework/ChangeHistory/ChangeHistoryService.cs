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
/// EF-backed implementation of the ChangeHistory application service contract.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <typeparam name="TContext">The EF Core context type.</typeparam>
/// <example>
/// <code>
/// services.AddScoped&lt;IChangeHistoryService&lt;Customer&gt;, ChangeHistoryService&lt;Customer, AppDbContext&gt;&gt;();
/// </code>
/// </example>
public sealed class ChangeHistoryService<TEntity, TContext>(
    ChangeHistoryQueryService<TContext> queryService,
    ChangeHistoryRestoreCommandHandler<TEntity, TContext> restoreHandler,
    ChangeHistoryOptions changeHistoryOptions = null,
    IChangeHistoryReadAuthorizer<TContext> readAuthorizer = null,
    IChangeHistoryRestoreRequestAuthorizer<TEntity, TContext> restoreAuthorizer = null)
    : IChangeHistoryService<TEntity, TContext>
    where TEntity : class, IEntity
    where TContext : DbContext
{
    /// <inheritdoc />
    public async Task<Result<ChangeHistoryFindAllResult>> FindAllAsync(
        ChangeHistoryFindAllQuery query,
        CancellationToken cancellationToken = default)
    {
        var authorizationResult = await ChangeHistoryAuthorization.AuthorizeReadAsync(
            changeHistoryOptions,
            readAuthorizer,
            query?.EntityType,
            query?.EntityId,
            query?.ChangeSetId,
            query?.IncludeValues ?? true,
            cancellationToken).AnyContext();
        if (authorizationResult.IsFailure)
        {
            return Result<ChangeHistoryFindAllResult>.Failure()
                .WithErrors(authorizationResult.Errors)
                .WithMessages(authorizationResult.Messages);
        }

        var result = await queryService.FindAllAsync(query, cancellationToken).AnyContext();
        if (result.IsFailure)
        {
            return Result<ChangeHistoryFindAllResult>.Failure()
                .WithErrors(result.Errors)
                .WithMessages(result.Messages);
        }

        return Result<ChangeHistoryFindAllResult>.Success(new ChangeHistoryFindAllResult(
            result.Value,
            result.TotalCount,
            result.CurrentPage,
            result.PageSize,
            result.TotalPages,
            result.HasPreviousPage,
            result.HasNextPage));
    }

    /// <inheritdoc />
    public async Task<Result<ChangeHistoryFindAllChangeSetsResult>> FindAllChangeSetsAsync(
        ChangeHistoryFindAllQuery query,
        CancellationToken cancellationToken = default)
    {
        var authorizationResult = await ChangeHistoryAuthorization.AuthorizeReadAsync(
            changeHistoryOptions,
            readAuthorizer,
            query?.EntityType,
            query?.EntityId,
            query?.ChangeSetId,
            query?.IncludeValues ?? true,
            cancellationToken).AnyContext();
        if (authorizationResult.IsFailure)
        {
            return Result<ChangeHistoryFindAllChangeSetsResult>.Failure()
                .WithErrors(authorizationResult.Errors)
                .WithMessages(authorizationResult.Messages);
        }

        var result = await queryService.FindAllChangeSetsAsync(query, cancellationToken).AnyContext();
        if (result.IsFailure)
        {
            return Result<ChangeHistoryFindAllChangeSetsResult>.Failure()
                .WithErrors(result.Errors)
                .WithMessages(result.Messages);
        }

        return Result<ChangeHistoryFindAllChangeSetsResult>.Success(new ChangeHistoryFindAllChangeSetsResult(
            result.Value,
            result.TotalCount,
            result.CurrentPage,
            result.PageSize,
            result.TotalPages,
            result.HasPreviousPage,
            result.HasNextPage));
    }

    /// <inheritdoc />
    public async Task<Result<ChangeHistoryChangeSetRecord>> FindOneChangeSetAsync(
        ChangeHistoryFindOneChangeSetQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query is null)
        {
            return Result<ChangeHistoryChangeSetRecord>.Failure(new ValidationError("A ChangeHistory change-set query is required."));
        }

        var authorizationResult = await ChangeHistoryAuthorization.AuthorizeReadAsync(
            changeHistoryOptions,
            readAuthorizer,
            query.EntityType,
            query.EntityId,
            query.ChangeSetId,
            query.IncludeValues,
            cancellationToken).AnyContext();
        if (authorizationResult.IsFailure)
        {
            return Result<ChangeHistoryChangeSetRecord>.Failure()
                .WithErrors(authorizationResult.Errors)
                .WithMessages(authorizationResult.Messages);
        }

        return await queryService.FindOneChangeSetAsync(query, cancellationToken).AnyContext();
    }

    /// <inheritdoc />
    public async Task<Result<ChangeHistoryRestoreResult>> RestoreAsync(
        ChangeHistoryRestoreCommand<TEntity> command,
        CancellationToken cancellationToken = default)
    {
        if (command is null)
        {
            return Result<ChangeHistoryRestoreResult>.Failure(new ValidationError("A ChangeHistory restore command is required."));
        }

        var authorizationResult = await ChangeHistoryAuthorization.AuthorizeRestoreAsync(
            changeHistoryOptions,
            restoreAuthorizer,
            typeof(TEntity),
            command.EntityId,
            command.ChangeSetId,
            cancellationToken).AnyContext();
        if (authorizationResult.IsFailure)
        {
            return Result<ChangeHistoryRestoreResult>.Failure()
                .WithErrors(authorizationResult.Errors)
                .WithMessages(authorizationResult.Messages);
        }

        return await restoreHandler.HandleAsync(command, cancellationToken).AnyContext();
    }
}
