// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Entities;

using BridgingIT.DevKit.Application.Commands;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Repositories;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

public abstract class EntityUpdateCommandHandlerBase<TCommand, TEntity>
    : CommandHandlerBase<TCommand, Result<EntityUpdatedCommandResult>>
    where TCommand : EntityUpdateCommandBase<TEntity>
    where TEntity : class, IEntity
{
    private readonly IGenericRepository<TEntity> repository;
    private readonly IStringLocalizer localizer;
    private List<IEntityUpdateCommandRule<TEntity>> rules;
    private List<Func<TCommand, IEntityUpdateCommandRule<TEntity>>> rulesFuncs = null;

    protected EntityUpdateCommandHandlerBase(
        ILoggerFactory loggerFactory,
        IGenericRepository<TEntity> repository,
        IEnumerable<IEntityUpdateCommandRule<TEntity>> rules = null,
        IStringLocalizer localizer = null)
        : base(loggerFactory)
    {
        EnsureArg.IsNotNull(repository, nameof(repository));

        this.repository = repository;
        this.rules = rules?.ToList();
        this.localizer = localizer;
    }

    public virtual EntityUpdateCommandHandlerBase<TCommand, TEntity> AddRule(
        IEntityUpdateCommandRule<TEntity> rule)
    {
        (this.rules ??= new()).AddOrUpdate(rule);

        return this;
    }

    public virtual EntityUpdateCommandHandlerBase<TCommand, TEntity> AddRule<TRule>()
        where TRule : class, IEntityUpdateCommandRule<TEntity> =>
        this.AddRule(Factory<TRule>.Create());

    public virtual EntityUpdateCommandHandlerBase<TCommand, TEntity> AddRule(
        Func<TCommand, IEntityUpdateCommandRule<TEntity>> rule)
    {
        (this.rulesFuncs ??= new()).AddOrUpdate(rule);

        return this;
    }

    public virtual IEnumerable<IEntityUpdateCommandRule<TEntity>> AddRules(TCommand command)
    {
        return[];
    }

    public override async Task<CommandResponse<Result<EntityUpdatedCommandResult>>> Process(
        TCommand command,
        CancellationToken cancellationToken)
    {
        EnsureArg.IsNotNull(command, nameof(command));

        var entity = await this.repository.FindOneAsync(command.Entity.Id, cancellationToken: cancellationToken).AnyContext();
        this.EnsureEntityFound(command, entity);
        await this.CheckRulesAsync(command);
        this.SetAudit(command);

        await this.repository.UpsertAsync(command.Entity, cancellationToken).AnyContext();

        return new CommandResponse<Result<EntityUpdatedCommandResult>>
        {
            Result = Result<EntityUpdatedCommandResult>.Success(
                new EntityUpdatedCommandResult(command.Entity.Id.ToString()),
                this.localizer != null ? this.localizer[$"{typeof(TEntity).Name} Saved"] : string.Empty)
        };
    }

    private void EnsureEntityFound(TCommand command, TEntity entity)
    {
        if (entity == null)
        {
            throw new EntityNotFoundException($"{typeof(TEntity).Name}: {command.Entity?.Id}");
        }
        else if (entity is IAuditable auditable && auditable.AuditState.IsDeleted())
        {
            throw new EntityNotFoundException($"{typeof(TEntity).Name}: {command.Entity?.Id}");
        }
        else if (entity is ISoftDeletable deletable && deletable.Deleted == true)
        {
            throw new EntityNotFoundException($"{typeof(TEntity).Name}: {command.Entity?.Id}");
        }
    }

    private async Task CheckRulesAsync(TCommand command)
    {
        var rules = (this.rules ??= new()).Union(this.AddRules(command).SafeNull()).ToList();
        this.rulesFuncs?.ForEach(s => rules.Add(s.Invoke(command)));

        this.Logger.LogInformation("{LogKey} entity rules check (type={CommandType}, id={CommandId}, handler={CommandHandler})", Commands.Constants.LogKey, command.GetType().Name, command.Id, this.GetType().Name);
        this.Logger.LogInformation($"{{LogKey}} entity rules: {rules.SafeNull().Select(b => b.GetType().PrettyName()).ToString(", ")}", Commands.Constants.LogKey);

        await Check.ThrowAsync(rules, command.Entity);
    }

    private void SetAudit(TCommand command)
    {
        if (command.Entity is IAuditable entity)
        {
            if (entity?.AuditState?.IsDeleted() == true)
            {
                throw new EntityNotFoundException($"{typeof(TEntity).Name}: {command.Entity?.Id}");
            }

            entity.AuditState ??= new();
            entity.AuditState.SetUpdated(command.Identity);
        }
    }
}
