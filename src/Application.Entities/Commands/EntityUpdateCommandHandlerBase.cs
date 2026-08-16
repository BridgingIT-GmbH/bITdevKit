// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Entities;

using Microsoft.Extensions.Localization;
using Constants = Commands.Constants;

/// <summary>
/// Represents entity update command handler base.
/// </summary>
/// <typeparam name="TCommand">The command type.</typeparam>
/// <typeparam name="TEntity">The entity type.</typeparam>
[Obsolete("Use the new Requester from now on")]
public abstract class EntityUpdateCommandHandlerBase<TCommand, TEntity>
    : CommandHandlerBase<TCommand, Result<EntityUpdatedCommandResult>>
    where TCommand : EntityUpdateCommandBase<TEntity>
    where TEntity : class, IEntity
{
    private readonly IGenericRepository<TEntity> repository;
    private readonly IStringLocalizer localizer;
    private List<IEntityUpdateCommandRule<TEntity>> rules;
    private List<Func<TCommand, IEntityUpdateCommandRule<TEntity>>> rulesFuncs;

    /// <summary>
    /// Initializes a new instance of the <c>EntityUpdateCommandHandlerBase</c> class.
    /// </summary>
    /// <param name="loggerFactory">The factory used to create loggers.</param>
    /// <param name="repository">The repository used by the operation.</param>
    /// <param name="rules">The rules used by the operation.</param>
    /// <param name="localizer">The localizer used by the operation.</param>
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

    /// <summary>
    /// Adds rule.
    /// </summary>
    /// <param name="rule">The rule used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public virtual EntityUpdateCommandHandlerBase<TCommand, TEntity> AddRule(IEntityUpdateCommandRule<TEntity> rule)
    {
        (this.rules ??= []).AddOrUpdate(rule);

        return this;
    }

    /// <summary>
    /// Represents add rule.
    /// </summary>
    /// <typeparam name="TRule">The rule type.</typeparam>
    public virtual EntityUpdateCommandHandlerBase<TCommand, TEntity> AddRule<TRule>()
        where TRule : class, IEntityUpdateCommandRule<TEntity>
    {
        return this.AddRule(Factory<TRule>.Create());
    }

    /// <summary>
    /// Adds rule.
    /// </summary>
    /// <param name="rule">The rule used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public virtual EntityUpdateCommandHandlerBase<TCommand, TEntity> AddRule(
        Func<TCommand, IEntityUpdateCommandRule<TEntity>> rule)
    {
        (this.rulesFuncs ??= []).AddOrUpdate(rule);

        return this;
    }

    /// <summary>
    /// Adds rules.
    /// </summary>
    /// <param name="command">The command used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public virtual IEnumerable<IEntityUpdateCommandRule<TEntity>> AddRules(TCommand command)
    {
        return [];
    }

    /// <inheritdoc/>
    public override async Task<CommandResponse<Result<EntityUpdatedCommandResult>>> Process(
        TCommand command,
        CancellationToken cancellationToken)
    {
        EnsureArg.IsNotNull(command, nameof(command));

        var entity = await this.repository.FindOneAsync(command.Entity.Id, cancellationToken: cancellationToken)
            .AnyContext();
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
        if (entity is null)
        {
            throw new EntityNotFoundException($"{typeof(TEntity).Name}: {command.Entity?.Id}");
        }

        if (entity is IAuditable auditable && auditable.AuditState.IsDeleted())
        {
            throw new EntityNotFoundException($"{typeof(TEntity).Name}: {command.Entity?.Id}");
        }

        if (entity is ISoftDeletable deletable && deletable.Deleted == true)
        {
            throw new EntityNotFoundException($"{typeof(TEntity).Name}: {command.Entity?.Id}");
        }
    }

    private async Task CheckRulesAsync(TCommand command)
    {
        var rules = (this.rules ??= []).Union(this.AddRules(command).SafeNull()).ToList();
        this.rulesFuncs?.ForEach(s => rules.Add(s.Invoke(command)));

        this.Logger.LogInformation(
            "[{LogKey}] entity rules check (type={CommandType}, id={CommandRequestId}, handler={CommandHandler})",
            Constants.LogKey,
            command.GetType().Name,
            command.RequestId,
            this.GetType().Name);
        this.Logger.LogInformation(
            $"{{LogKey}} entity rules: {rules.SafeNull().Select(b => b.GetType().PrettyName()).ToString(", ")}",
            Constants.LogKey);

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

            entity.AuditState ??= new AuditState();
            entity.AuditState.SetUpdated(command.Identity);
        }
    }
}
