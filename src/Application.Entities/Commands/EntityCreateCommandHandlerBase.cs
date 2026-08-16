// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Entities;

using Microsoft.Extensions.Localization;
using Constants = Commands.Constants;

/// <summary>
/// Represents entity create command handler base.
/// </summary>
/// <typeparam name="TCommand">The command type.</typeparam>
/// <typeparam name="TEntity">The entity type.</typeparam>
[Obsolete("Use the new Requester from now on")]
public abstract class EntityCreateCommandHandlerBase<TCommand, TEntity>
    : CommandHandlerBase<TCommand, Result<EntityCreatedCommandResult>>
    where TCommand : EntityCreateCommandBase<TEntity>
    where TEntity : class, IEntity
{
    private readonly IGenericRepository<TEntity> repository;
    private readonly IStringLocalizer localizer;
    private List<IEntityCreateCommandRule<TEntity>> rules;
    private List<Func<TCommand, IEntityCreateCommandRule<TEntity>>> rulesFuncs;

    /// <summary>
    /// Initializes a new instance of the <c>EntityCreateCommandHandlerBase</c> class.
    /// </summary>
    /// <param name="loggerFactory">The factory used to create loggers.</param>
    /// <param name="repository">The repository used by the operation.</param>
    /// <param name="rules">The rules used by the operation.</param>
    /// <param name="localizer">The localizer used by the operation.</param>
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

    /// <summary>
    /// Adds rule.
    /// </summary>
    /// <param name="rule">The rule used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public virtual EntityCreateCommandHandlerBase<TCommand, TEntity> AddRule(IEntityCreateCommandRule<TEntity> rule)
    {
        (this.rules ??= []).AddOrUpdate(rule);

        return this;
    }

    /// <summary>
    /// Represents add rule.
    /// </summary>
    /// <typeparam name="TRule">The rule type.</typeparam>
    public virtual EntityCreateCommandHandlerBase<TCommand, TEntity> AddRule<TRule>()
        where TRule : class, IEntityCreateCommandRule<TEntity>
    {
        return this.AddRule(Factory<TRule>.Create());
    }

    /// <summary>
    /// Adds rule.
    /// </summary>
    /// <param name="rule">The rule used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public virtual EntityCreateCommandHandlerBase<TCommand, TEntity> AddRule(
        Func<TCommand, IEntityCreateCommandRule<TEntity>> rule)
    {
        (this.rulesFuncs ??= []).AddOrUpdate(rule);

        return this;
    }

    /// <summary>
    /// Adds rules.
    /// </summary>
    /// <param name="command">The command used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public virtual IEnumerable<IEntityCreateCommandRule<TEntity>> AddRules(TCommand command)
    {
        return [];
    }

    /// <inheritdoc/>
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

        await Check.ThrowAsync(rules, command.Entity, this.localizer);
    }

    private void SetAudit(TCommand command)
    {
        if (command.Entity is IAuditable auditable)
        {
            auditable.AuditState ??= new AuditState();
            auditable.AuditState.SetCreated(command.Identity);
        }
    }
}
