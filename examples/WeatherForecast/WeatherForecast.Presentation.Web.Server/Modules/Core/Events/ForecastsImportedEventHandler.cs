// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Presentation.Web.Server.Modules.Core;

using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Examples.WeatherForecast.Application.Modules.Core;
using EnsureThat;
using MediatR;
using Microsoft.AspNetCore.SignalR;

public class ForecastsImportedEventHandler(IHubContext<NotificationHub> hub) : INotificationHandler<ForecastsImportedEvent> // TODO: umstellen auf Message (Handler)
{
    private readonly IHubContext<NotificationHub> hub = hub;

    public async Task Handle(ForecastsImportedEvent notification, CancellationToken cancellationToken)
    {
        EnsureArg.IsNotNull(notification, nameof(notification));

        await this.hub.Clients.All.SendAsync("ReceiveMessage", $"[ForecastsImported]: {notification.Cities.ToString(", ")}", cancellationToken: cancellationToken);
    }
}
