// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Entities;

using BridgingIT.DevKit.Application.Commands;
using BridgingIT.DevKit.Application.Messaging;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

public class EntityCommandMessagingBehavior<TRequest, TResponse> : CommandBehaviorBase<TRequest, TResponse>
    where TRequest : class, IRequest<TResponse>
{
    private readonly IMessageBroker messageBroker;
    private readonly EntityCommandMessagingBehaviorOptions options;

    public EntityCommandMessagingBehavior(
        ILoggerFactory loggerFactory,
        IMessageBroker messageBroker = null,
        EntityCommandMessagingBehaviorOptions options = null)
        : base(loggerFactory)
    {
        this.messageBroker = messageBroker;
        this.options = options ?? new EntityCommandMessagingBehaviorOptions();
    }

    protected override bool CanProcess(TRequest request)
    {
        if (!this.options.Enabled)
        {
            return false;
        }

        if (request is IEntityCreateCommand instanceCreate && instanceCreate.Entity != null)
        {
            return this.options?.ExcludedEntityTypes?.Contains(instanceCreate.Entity?.GetType()) == false;
        }
        else if (request is IEntityUpdateCommand instanceUpdate && instanceUpdate.Entity != null)
        {
            return this.options?.ExcludedEntityTypes?.Contains(instanceUpdate.Entity?.GetType()) == false;
        }
        else if (request is IEntityDeleteCommand instanceDelete && instanceDelete.Entity != null)
        {
            return this.options?.ExcludedEntityTypes?.Contains(instanceDelete.Entity?.GetType()) == false;
        }

        return false;
    }

    protected override async Task<TResponse> Process(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var result = await next().AnyContext(); // continue pipeline

        if (this.messageBroker == null)
        {
            this.Logger.LogWarning("{LogKey} cannot send entity message, no messagebroker specified", Commands.Constants.LogKey);
            return result;
        }

        if (request is IEntityCreateCommand instanceCreate && instanceCreate?.Entity != null)
        {
            await this.ProcessCreated(result, instanceCreate, cancellationToken);
        }
        else if (request is IEntityUpdateCommand instanceUpdate && instanceUpdate?.Entity != null)
        {
            await this.ProcessUpdated(result, instanceUpdate, cancellationToken);
        }
        else if (request is IEntityDeleteCommand instanceDelete && instanceDelete?.Entity != null)
        {
            await this.ProcessDeleted(result, instanceDelete, cancellationToken);
        }

        return result;
    }

    private async Task ProcessCreated(TResponse result, IEntityCreateCommand instance, CancellationToken cancellationToken)
    {
        if (result is CommandResponse<Result<EntityCreatedCommandResult>> commandResult
            && commandResult.Result?.IsSuccess == true)
        {
            var message = Factory.Create<IMessage>(typeof(EntityCreatedMessage<>), instance.Entity.GetType(), instance.Entity as IEntity);
            if (message != null)
            {
                await Task.Delay(this.options.PublishDelay, cancellationToken);
                this.Logger.LogInformation("{LogKey} send entity created message (type={EntityType})", Commands.Constants.LogKey, instance.Entity.GetType().PrettyName());
                await this.messageBroker?.Publish(message, cancellationToken);
            }
        }
    }

    private async Task ProcessUpdated(TResponse result, IEntityUpdateCommand instance, CancellationToken cancellationToken)
    {
        if (result is CommandResponse<Result<EntityUpdatedCommandResult>> commandResult &&
            commandResult.Result?.IsSuccess == true)
        {
            var message = Factory.Create<IMessage>(typeof(EntityUpdatedMessage<>), instance.Entity.GetType(), instance.Entity as IEntity);
            if (message != null)
            {
                await Task.Delay(this.options.PublishDelay, cancellationToken);
                this.Logger.LogInformation("{LogKey} send entity updated message (type={EntityType})", Commands.Constants.LogKey, instance?.Entity.GetType().PrettyName());
                await this.messageBroker?.Publish(message, cancellationToken);
            }
        }
    }

    private async Task ProcessDeleted(TResponse result, IEntityDeleteCommand instance, CancellationToken cancellationToken)
    {
        if (result is CommandResponse<Result<EntityDeletedCommandResult>> commandResult &&
            commandResult.Result?.IsSuccess == true)
        {
            var message = Factory.Create<IMessage>(typeof(EntityDeletedMessage<>), instance.Entity.GetType(), instance.Entity as IEntity);
            if (message != null)
            {
                await Task.Delay(this.options.PublishDelay, cancellationToken);
                this.Logger.LogInformation("{LogKey} send entity deleted message (type={EntityType})", Commands.Constants.LogKey, instance.Entity.GetType().PrettyName());
                await this.messageBroker?.Publish(message, cancellationToken);
            }
        }
    }
}
