// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Queueing.Dashboard.Pages;

using BridgingIT.DevKit.Application.Queueing;

/// <summary>
/// View model for the server-rendered Queueing dashboard content.
/// </summary>
/// <example>
/// <code>
/// var model = new DashboardQueueingViewModel();
/// </code>
/// </example>
public sealed class DashboardQueueingViewModel
{
    public DateTimeOffset CapturedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public string ActionBase { get; set; } = "/_bdk/dashboard/queueing";

    public QueueMessageStats Stats { get; set; } = new();

    public QueueBrokerSummary Summary { get; set; } = new();

    public IReadOnlyList<QueueMessageInfo> Messages { get; set; } = [];

    public IReadOnlyList<QueueSubscriptionInfo> Subscriptions { get; set; } = [];

    public IReadOnlyList<QueueMessageInfo> WaitingMessages { get; set; } = [];

    public List<string> Errors { get; } = [];

    public bool IsAvailable { get; set; } = true;
}
