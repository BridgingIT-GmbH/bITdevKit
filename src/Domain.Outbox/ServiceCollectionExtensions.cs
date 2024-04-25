// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

//namespace Microsoft.Extensions.DependencyInjection;

//using BridgingIT.DevKit.Common;
//using BridgingIT.DevKit.Domain.Outbox;
//using Microsoft.Extensions.Logging;

//public static class ServiceCollectionExtensions
//{
//    public static IServiceCollection AddOutboxDomainEventService( // TODO: move to Application.Outbox
//        this IServiceCollection services,
//        IOutboxDomainEventWorker worker,
//        Builder<OutboxDomainEventOptionsBuilder, OutboxDomainEventOptions> optionsBuilder)
//    {
//        return services
//            .AddOutboxDomainEventService(
//                worker,
//                optionsBuilder(new OutboxDomainEventOptionsBuilder()).Build());
//    }

//    public static IServiceCollection AddOutboxDomainEventService( // TODO: move to Application.Outbox
//        this IServiceCollection services,
//        IOutboxDomainEventWorker worker,
//        OutboxDomainEventOptions options)
//    {
//        services.AddHostedService(sp =>
//            new OutboxDomainEventService(
//                sp.GetRequiredService<ILoggerFactory>(),
//                worker,
//                options));

//        return services;
//    }
//}