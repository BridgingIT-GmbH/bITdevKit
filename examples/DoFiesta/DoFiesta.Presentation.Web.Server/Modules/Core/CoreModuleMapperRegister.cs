// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Presentation.Web.Server.Modules.Core;

using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Examples.DoFiesta.Application.Modules.Core;
using BridgingIT.DevKit.Examples.DoFiesta.Domain.Model;
using Mapster;

public class CatalogMapperRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        // Generic type conversions
        config.Default.EnumMappingStrategy(EnumMappingStrategy.ByValue);

        //// Register type converters for TodoItemId
        //config.NewConfig<TodoItemId, string>()
        //    .MapWith(src => src.Value.ToString());
        //config.NewConfig<string, TodoItemId>()
        //    .MapWith(src => TodoItemId.Create(src));

        //// Register type converters for TodoStepId
        //config.NewConfig<TodoStepId, string>()
        //    .MapWith(src => src.Value.ToString());
        //config.NewConfig<string, TodoStepId>()
        //    .MapWith(src => TodoStepId.Create(src));

        //// Register type converters for SubscriptionId
        //config.NewConfig<SubscriptionId, string>()
        //    .MapWith(src => src.Value.ToString());
        //config.NewConfig<string, SubscriptionId>()
        //    .MapWith(src => SubscriptionId.Create(src));

        // Register type converter for EmailAddress
        config.NewConfig<EmailAddress, string>()
            .MapWith(src => src.Value);
        config.NewConfig<string, EmailAddress>()
            .MapWith(src => EmailAddress.Create(src));

        // Register type converters for enumerations
        RegisterConverter<TodoStatus>(config);
        RegisterConverter<TodoPriority>(config);
        RegisterConverter<SubscriptionStatus>(config);
        RegisterConverter<SubscriptionPlan>(config);
        RegisterConverter<SubscriptionBillingCycle>(config);

        // Main type mappings
        config.ForType<TodoItem, TodoItemModel>()
            .Map(dest => dest.ConcurrencyVersion, src => src.ConcurrencyVersion.ToString())
            .IgnoreNullValues(true);
        config.ForType<TodoItemModel, TodoItem>()
            .Map(dest => dest.ConcurrencyVersion, src => src.ConcurrencyVersion != null ? Guid.Parse(src.ConcurrencyVersion) : Guid.Empty)
            .IgnoreNullValues(true);

        config.ForType<TodoStep, TodoStepModel>()
            .IgnoreNullValues(true);
        config.ForType<TodoStepModel, TodoStep>()
            .IgnoreNullValues(true);

        config.ForType<Subscription, SubscriptionModel>()
            .Map(dest => dest.ConcurrencyVersion, src => src.ConcurrencyVersion.ToString())
            .IgnoreNullValues(true);
        config.ForType<SubscriptionModel, Subscription>()
            .Map(dest => dest.ConcurrencyVersion, src => src.ConcurrencyVersion != null ? Guid.Parse(src.ConcurrencyVersion) : Guid.Empty)
            .IgnoreNullValues(true);
    }

    private static void RegisterConverter<T>(TypeAdapterConfig config)
        where T : Enumeration
    {
        config.NewConfig<T, int>()
            .MapWith(src => src.Id);

        config.NewConfig<int, T>()
            .MapWith(src => Enumeration.GetAll<T>().First(x => x.Id == src));
    }
}