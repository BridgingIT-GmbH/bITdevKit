// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Presentation.Web.Server.Modules.Core;

using BridgingIT.DevKit.Examples.DoFiesta.Application.Modules.Core;
using BridgingIT.DevKit.Examples.DoFiesta.Domain.Model;
using Mapster;

public class CatalogMapperRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        // Entity -> Model mappings
        config.ForType<TodoItem, TodoItemModel>()
            .Map(dest => dest.Id, src => src.Id.Value.ToString())
            .Map(dest => dest.Status, src => src.Status.Id)
            .Map(dest => dest.Priority, src => src.Priority.Id)
            .Map(dest => dest.Assignee, src => src.Assignee.Value)
            .Map(dest => dest.ConcurrencyVersion, src => src.ConcurrencyVersion.ToString())
            .IgnoreNullValues(true);

        // Model -> Entity mappings
        config.ForType<TodoItemModel, TodoItem>()
            .Map(dest => dest.Id, src => TodoItemId.Create(src.Id))
            .Map(dest => dest.Status, src => TodoStatus.GetAll<TodoStatus>().First(x => x.Id == src.Status))
            .Map(dest => dest.Priority, src => TodoPriority.GetAll<TodoPriority>().First(x => x.Id == src.Priority))
            .Map(dest => dest.Assignee, src => EmailAddress.Create(src.Assignee))
            .Map(dest => dest.ConcurrencyVersion, src => Guid.Parse(src.ConcurrencyVersion))
            .IgnoreNullValues(true);

        // Entity -> Model mappings
        config.ForType<TodoStep, TodoStepModel>()
            .Map(dest => dest.Id, src => src.Id.Value.ToString())
            .Map(dest => dest.TodoItemId, src => src.TodoItemId.Value.ToString())
            .Map(dest => dest.Status, src => src.Status.Id)
            .IgnoreNullValues(true);

        // Model -> Entity mappings
        config.ForType<TodoStepModel, TodoStep>()
            .Map(dest => dest.Id, src => TodoStepId.Create(src.Id))
            .Map(dest => dest.TodoItemId, src => TodoItemId.Create(src.TodoItemId))
            .Map(dest => dest.Status, src => TodoStatus.GetAll<TodoStatus>().First(x => x.Id == src.Status))
            .IgnoreNullValues(true);

        // Entity -> Model mappings
        config.ForType<Subscription, SubscriptionModel>()
            .Map(dest => dest.Id, src => src.Id.Value.ToString())
            .Map(dest => dest.Plan, src => src.Plan.Id)
            .Map(dest => dest.Status, src => src.Status.Id)
            .Map(dest => dest.BillingCycle, src => src.BillingCycle.Id)
            .Map(dest => dest.ConcurrencyVersion, src => src.ConcurrencyVersion.ToString())
            .IgnoreNullValues(true);

        // Model -> Entity mappings
        config.ForType<SubscriptionModel, Subscription>()
            .Map(dest => dest.Id, src => SubscriptionId.Create(src.Id))
            .Map(dest => dest.Plan, src => SubscriptionPlan.GetAll<SubscriptionPlan>().First(x => x.Id == src.Plan))
            .Map(dest => dest.Status, src => SubscriptionStatus.GetAll<SubscriptionStatus>().First(x => x.Id == src.Status))
            .Map(dest => dest.BillingCycle, src => SubscriptionBillingCycle.GetAll<SubscriptionBillingCycle>().First(x => x.Id == src.BillingCycle))
            .Map(dest => dest.ConcurrencyVersion, src => Guid.Parse(src.ConcurrencyVersion))
            .IgnoreNullValues(true);
    }
}