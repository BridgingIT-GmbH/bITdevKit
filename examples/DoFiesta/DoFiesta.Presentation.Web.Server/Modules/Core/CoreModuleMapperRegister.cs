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
        config.ForType<TodoItem, TodoItemModel>()
        .Map(dest => dest.Id, src => src.Id.Value.ToString())
        .Map(dest => dest.Status, src => src.Status.Id)
        .Map(dest => dest.Priority, src => src.Priority.Id)
        .Map(dest => dest.Assignee, src => src.Assignee.Value)
        .Map(dest => dest.ConcurrencyVersion, src => src.ConcurrencyVersion.ToString())
        .IgnoreNullValues(true);

        config.ForType<TodoStep, TodoStepModel>()
        .Map(dest => dest.Id, src => src.Id.Value.ToString())
        .Map(dest => dest.TodoItemId, src => src.TodoItemId.Value.ToString())
        .Map(dest => dest.Status, src => src.Status.Id)
        .IgnoreNullValues(true);

        config.ForType<Subscription, SubscriptionModel>()
            .IgnoreNullValues(true);
            //.Map(d => d.Plan, s => s.Plan.Value)
            //.Map(d => d.Status, s => s.Status.Value)
            //.Map(d => d.BillingCycle, s => s.BillingCycle.Value);
    }
}