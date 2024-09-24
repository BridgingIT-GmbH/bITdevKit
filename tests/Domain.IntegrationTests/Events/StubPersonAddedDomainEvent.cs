// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.IntegrationTests.Events;

using Microsoft.Extensions.Logging;

public class StubPersonAddedDomainEvent(Guid personId) : DomainEventBase
{
    public Guid PersonId { get; } = personId;
}

public class StubPersonAddedDomainEventHandler(ILoggerFactory loggerFactory) : DomainEventHandlerBase<StubPersonAddedDomainEvent>(loggerFactory)
{
    public static Guid PersonId { get; internal set; }

    public static bool Handled { get; internal set; }

    public override bool CanHandle(StubPersonAddedDomainEvent notification)
    {
        return true;
    }

    public override async Task Process(StubPersonAddedDomainEvent notification, CancellationToken cancellationToken)
    {
        await Task.Run(() =>
            {
                Handled = true;
                PersonId = notification.PersonId;
            },
            cancellationToken);
    }
}