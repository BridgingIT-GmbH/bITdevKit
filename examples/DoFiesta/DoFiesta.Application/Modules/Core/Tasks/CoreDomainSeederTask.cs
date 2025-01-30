namespace BridgingIT.DevKit.Examples.DoFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Examples.DoFiesta.Domain.Model;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;

public class CoreDomainSeederTask(
    ILoggerFactory loggerFactory,
    IGenericRepository<TodoItem> todoItemRepository,
    IGenericRepository<Subscription> subscriptionRepository) : IStartupTask
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
    }

    private async Task<TodoItem[]> SeedTodoItems(IGenericRepository<TodoItem> repository, CancellationToken cancellationToken)
    {
        this.logger.LogInformation("{LogKey} seed todoitems (task={StartupTaskType})", "IFR", this.GetType().PrettyName());

        var entities = CoreSeedEntities.TodoItems.Create();
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
}