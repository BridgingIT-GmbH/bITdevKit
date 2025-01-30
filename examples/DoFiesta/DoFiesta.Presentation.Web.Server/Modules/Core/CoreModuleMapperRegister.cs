// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Presentation.Web.Server.Modules.Core;

using BridgingIT.DevKit.Examples.DoFiesta.Application;
using BridgingIT.DevKit.Examples.DoFiesta.Domain.Model;
using Mapster;

public class CatalogMapperRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.ForType<TodoItem, TodoItemModel>()
            .Map(d => d.Status, s => s.Status.Value)
            .Map(d => d.Priority, s => s.Priority.Value)
            .Map(d => d.Assignee, s => s.Assignee.Value);

        config.ForType<TodoStep, TodoStepModel>()
            .Map(d => d.Status, s => s.Status.Value);

        config.ForType<Subscription, SubscriptionModel>()
            .Map(d => d.Plan, s => s.Plan.Value)
            .Map(d => d.Status, s => s.Status.Value)
            .Map(d => d.BillingCycle, s => s.BillingCycle.Value);
    }
}