// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Entities;

using Microsoft.Extensions.Localization;
using Constants = Commands.Constants;

public abstract class EntityDeleteCommandHandlerBase<TCommand, TEntity>
    : CommandHandlerBase<TCommand, Result<EntityDeletedCommandResult>>
    where TCommand : EntityDeleteCommandBase<TEntity>
    where TEntity : class, IEntity
{
    private readonly IGenericRepository<TEntity> repository;
    private readonly IStringLocalizer localizer;
    private List<IEntityDeleteCommandRule<TEntity>> rules;
    private List<Func<TCommand, IEntityDeleteCommandRule<TEntity>>> rulesFuncs;

    protected EntityDeleteCommandHandlerBase(
        ILoggerFactory loggerFactory,
        IGenericRepository<TEntity> repository,
        IEnumerable<IEntityDeleteCommandRule<TEntity>> rules = null,
        IStringLocalizer localizer = null)
        : base(loggerFactory)
    {
        EnsureArg.IsNotNull(repository, nameof(repository));

        this.repository = repository;
        this.rules = rules?.ToList();
        this.localizer = localizer;
    }

    public virtual EntityDeleteCommandHandlerBase<TCommand, TEntity> AddRule(IEntityDeleteCommandRule<TEntity> rule)
    {
        (this.rules ??= []).AddOrUpdate(rule);

        return this;
    }

    public virtual EntityDeleteCommandHandlerBase<TCommand, TEntity> AddRule<TRule>()
        where TRule : class, IEntityDeleteCommandRule<TEntity>
    {
        return this.AddRule(Factory<TRule>.Create());
    }

    public virtual EntityDeleteCommandHandlerBase<TCommand, TEntity> AddRule(
        Func<TCommand, IEntityDeleteCommandRule<TEntity>> rule)
    {
        (this.rulesFuncs ??= []).AddOrUpdate(rule);

        return this;
    }

    public virtual IEnumerable<IEntityDeleteCommandRule<TEntity>> AddRules(TCommand command)
    {
        return [];
    }

    public override async Task<CommandResponse<Result<EntityDeletedCommandResult>>> Process(
        TCommand command,
        CancellationToken cancellationToken)
    {
        EnsureArg.IsNotNull(command, nameof(command));

        TEntity entity;

        if (!command.EntityId.IsNullOrEmpty())
        {
            entity = await this.repository.FindOneAsync(command.EntityId, cancellationToken: cancellationToken)
                .AnyContext();
            this.EnsureEntityFound(command, entity);

            command.Entity = entity;
        }
        else if (command.Entity != null)
        {
            entity = command.Entity;
        }
        else
        {
            throw new EntityNotFoundException($"{typeof(TEntity).Name}: {command.EntityId}");
        }

        await this.CheckRulesAsync(command, entity);

        if (entity is IAuditable)
        {
            this.SetAudit(command);

            await this.repository.UpsertAsync(entity, cancellationToken).AnyContext();
        }
        else if (entity is ISoftDeletable)
        {
            this.SetDeleted(command);

            await this.repository.UpsertAsync(entity, cancellationToken).AnyContext();
        }
        else
        {
            await this.repository.DeleteAsync(entity, cancellationToken).AnyContext();
        }

        return new CommandResponse<Result<EntityDeletedCommandResult>>
        {
            Result = Result<EntityDeletedCommandResult>.Success(
                new EntityDeletedCommandResult(entity.Id.ToString()),
                this.localizer != null ? this.localizer[$"{typeof(TEntity).Name} Deleted"] : string.Empty)
        };
    }

    private void EnsureEntityFound(TCommand command, TEntity entity)
    {
        if (entity == null)
        {
            throw new EntityNotFoundException(
                $"{typeof(TEntity).Name}: {command.EntityId}"); // this could be a EntityDeleteRule
        }

        if (entity is IAuditable auditable && auditable.AuditState.IsDeleted())
        {
            throw new EntityNotFoundException($"{typeof(TEntity).Name}: {command.EntityId}");
        }

        if (entity is ISoftDeletable deletable && deletable.Deleted == true)
        {
            throw new EntityNotFoundException($"{typeof(TEntity).Name}: {command.EntityId}");
        }
    }

    private async Task CheckRulesAsync(TCommand command, TEntity entity)
    {
        var rules = (this.rules ??= []).Union(this.AddRules(command).SafeNull()).ToList();
        this.rulesFuncs?.ForEach(s => rules.Add(s.Invoke(command)));

        this.Logger.LogInformation(
            "{LogKey} entity rules check (type={CommandType}, id={CommandRequestId}, handler={CommandHandler})",
            Constants.LogKey,
            command.GetType().Name,
            command.RequestId,
            this.GetType().Name);
        this.Logger.LogInformation(
            $"{{LogKey}} entity rules: {rules.SafeNull().Select(b => b.GetType().PrettyName()).ToString(", ")}",
            Constants.LogKey);

        await Check.ThrowAsync(rules, entity);
    }

    private void SetAudit(TCommand command)
    {
        if (command.Entity is IAuditable entity)
        {
            entity.AuditState ??= new AuditState();
            entity.AuditState.SetDeleted(command.Identity);
        }
    }

    private void SetDeleted(TCommand command)
    {
        if (command.Entity is ISoftDeletable entity)
        {
            entity.SetDeleted();
        }
    }
}