// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Application.Identity;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Examples.DoFiesta.Domain.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Handles the seeding of core domain data, into the database during startup.
/// Also sets the entity permissions
/// </summary>
public class CoreDomainSeederTask(
    ILoggerFactory loggerFactory,
    IGenericRepository<TodoItem> todoItemRepository,
    IGenericRepository<Subscription> subscriptionRepository,
    IEntityPermissionProvider entityPermissionProvider) : IStartupTask
{
    private readonly ILogger<CoreDomainSeederTask> logger =
        loggerFactory?.CreateLogger<CoreDomainSeederTask>() ??
        NullLoggerFactory.Instance.CreateLogger<CoreDomainSeederTask>();

    /// <summary>
    /// Executes the startup task asynchronously to seed core domain data into the database.
    /// </summary>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        this.logger.LogInformation("{LogKey} seed core (task={StartupTaskType})", "IFR", this.GetType().PrettyName());

        await this.SeedTodoItems(todoItemRepository, cancellationToken);
        await this.SeedSubscriptions(subscriptionRepository, cancellationToken);
        this.SeedRolePermissions();

        await this.LogPermissions(cancellationToken);
    }

    /// <summary>
    /// Seeds permissions for the roles
    /// </summary>
    private void SeedRolePermissions()
    {
        new EntityPermissionProviderBuilder(entityPermissionProvider)
            .ForRole(Role.Administrators)
                .WithPermission<TodoItem>(Permission.Read)
                .WithPermission<TodoItem>(Permission.Write)
                .WithPermission<TodoItem>(Permission.List)
                .WithPermission<TodoItem>(Permission.Delete)
            .ForRole(Role.Administrators)
                .WithPermission<Subscription>(Permission.Read)
                .WithPermission<Subscription>(Permission.Write)
                .WithPermission<Subscription>(Permission.List)
                .WithPermission<Subscription>(Permission.Delete).Build();
    }

    /// <summary>
    /// Seeds TodoItems into the repository if they do not already exist.
    /// </summary>
    private async Task<TodoItem[]> SeedTodoItems(IGenericRepository<TodoItem> repository, CancellationToken cancellationToken)
    {
        this.logger.LogInformation("{LogKey} seed todoitems (task={StartupTaskType})", "IFR", this.GetType().PrettyName());

        var entities = CoreSeedEntities.TodoItems.Create();
        foreach (var entity in entities)
        {
            if (!await repository.ExistsAsync(entity.Id, cancellationToken))
            {
                // Seed TodoItem for User
                entity.AuditState.SetCreated("seed", nameof(CoreDomainSeederTask));
                await repository.InsertAsync(entity, cancellationToken);

                // Seed User permissions for this TodoItem
                new EntityPermissionProviderBuilder(entityPermissionProvider)
                    .ForUser(entity.UserId)
                        .WithPermission<TodoItem>(entity.Id, Permission.Read)
                        .WithPermission<TodoItem>(entity.Id, Permission.Write)
                        .WithPermission<TodoItem>(entity.Id, Permission.Delete).Build();
            }
            else
            {
                return entities;
            }
        }

        return entities;
    }

    /// <summary>
    /// Seeds subscription entities into the repository if they do not already exist.
    /// </summary>
    private async Task<Subscription[]> SeedSubscriptions(IGenericRepository<Subscription> repository, CancellationToken cancellationToken)
    {
        this.logger.LogInformation("{LogKey} seed subscriptions (task={StartupTaskType})", "IFR", this.GetType().PrettyName());

        var entities = CoreSeedEntities.Subscriptions.Create();
        foreach (var entity in entities)
        {
            if (!await repository.ExistsAsync(entity.Id, cancellationToken))
            {
                entity.AuditState.SetCreated("seed", nameof(CoreDomainSeederTask));
                await repository.InsertAsync(entity, cancellationToken);
            }
            else
            {
                return entities;
            }
        }

        return entities;
    }

    /// <summary>
    /// Logs the permissions associated with the entities
    /// </summary>
    private async Task LogPermissions(CancellationToken cancellationToken)
    {
        var permissions = await entityPermissionProvider.GetPermissionsAsync(typeof(TodoItem).FullName, null, cancellationToken);
        this.logger.LogInformation("{LogKey} seeded permission overview (count=#{PermissionCount})", "IFR", permissions?.Count);

        foreach (var permission in permissions.SafeNull())
        {
            this.logger.LogDebug("{LogKey} seeded permission: {Permission}", "IFR", permission);
        }
    }
}