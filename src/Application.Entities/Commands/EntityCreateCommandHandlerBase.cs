// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Entities;
using BridgingIT.DevKit.Application.Commands;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Repositories;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

public abstract class EntityCreateCommandHandlerBase<TCommand, TEntity>
    : CommandHandlerBase<TCommand, Result<EntityCreatedCommandResult>>
    where TCommand : EntityCreateCommandBase<TEntity>
    where TEntity : class, IEntity
{
    private readonly IGenericRepository<TEntity> repository;
    private readonly IStringLocalizer localizer;
    private List<IEntityCreateCommandRule<TEntity>> rules = null;
    private List<Func<TCommand, IEntityCreateCommandRule<TEntity>>> rulesFuncs = null;

    protected EntityCreateCommandHandlerBase(
        ILoggerFactory loggerFactory,
        IGenericRepository<TEntity> repository,
        IEnumerable<IEntityCreateCommandRule<TEntity>> rules = null,
        IStringLocalizer localizer = null)
        : base(loggerFactory)
    {
        EnsureArg.IsNotNull(repository, nameof(repository));

        this.repository = repository;
        this.rules = rules?.ToList();
        this.localizer = localizer;
    }

    public virtual EntityCreateCommandHandlerBase<TCommand, TEntity> AddRule(
        IEntityCreateCommandRule<TEntity> rule)
    {
        (this.rules ??= new()).AddOrUpdate(rule);

        return this;
    }

    public virtual EntityCreateCommandHandlerBase<TCommand, TEntity> AddRule<TRule>()
        where TRule : class, IEntityCreateCommandRule<TEntity> =>
        this.AddRule(Factory<TRule>.Create());

    public virtual EntityCreateCommandHandlerBase<TCommand, TEntity> AddRule(
        Func<TCommand, IEntityCreateCommandRule<TEntity>> rule)
    {
        (this.rulesFuncs ??= new()).AddOrUpdate(rule);

        return this;
    }

    public virtual IEnumerable<IEntityCreateCommandRule<TEntity>> AddRules(TCommand command)
    {
        return[];
    }

    public override async Task<CommandResponse<Result<EntityCreatedCommandResult>>> Process(
        TCommand command,
        CancellationToken cancellationToken)
    {
        EnsureArg.IsNotNull(command, nameof(command));

        await this.CheckRulesAsync(command);
        this.SetAudit(command);

        await this.repository.InsertAsync(command.Entity, cancellationToken).AnyContext();

        return new CommandResponse<Result<EntityCreatedCommandResult>>
        {
            Result = Result<EntityCreatedCommandResult>.Success(
                new EntityCreatedCommandResult(command.Entity.Id.ToString()),
                this.localizer is not null ? this.localizer[$"{typeof(TEntity).Name} Saved"] : string.Empty)
        };
    }

    private async Task CheckRulesAsync(TCommand command)
    {
        var rules = (this.rules ??= new()).Union(this.AddRules(command).SafeNull()).ToList();
        this.rulesFuncs?.ForEach(s => rules.Add(s.Invoke(command)));

        this.Logger.LogInformation("{LogKey} entity rules check (type={CommandType}, id={CommandId}, handler={CommandHandler})", Constants.LogKey, command.GetType().Name, command.Id, this.GetType().Name);
        this.Logger.LogInformation($"{{LogKey}} entity rules: {rules.SafeNull().Select(b => b.GetType().PrettyName()).ToString(", ")}", Constants.LogKey);

        await Check.ThrowAsync(rules, command.Entity, this.localizer);
    }

    private void SetAudit(TCommand command)
    {
        if (command.Entity is IAuditable auditable)
        {
            auditable.AuditState ??= new();
            auditable.AuditState.SetCreated(command.Identity);
        }
    }
}
