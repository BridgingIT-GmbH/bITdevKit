// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Messaging.Dashboard.Pages;

using BridgingIT.DevKit.Application.Messaging;

/// <summary>
/// View model for the server-rendered Messaging dashboard content.
/// </summary>
/// <example>
/// <code>
/// var model = new DashboardMessagingViewModel();
/// </code>
/// </example>
public sealed class DashboardMessagingViewModel
{
    public DateTimeOffset CapturedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public string ActionBase { get; set; } = "/_bdk/dashboard/messaging";

    public BrokerMessageStats Stats { get; set; } = new();

    public BrokerMessageBrokerSummary Summary { get; set; } = new();

    public IReadOnlyList<BrokerMessageInfo> Messages { get; set; } = [];

    /// <summary>
    /// Gets or sets the message details indexed by broker message primary key.
    /// </summary>
    /// <example>
    /// <code>
    /// var detail = model.MessageDetailsById[message.Id];
    /// </code>
    /// </example>
    public IReadOnlyDictionary<Guid, BrokerMessageInfo> MessageDetailsById { get; set; } = new Dictionary<Guid, BrokerMessageInfo>();

    /// <summary>
    /// Gets or sets the message content indexed by broker message primary key.
    /// </summary>
    /// <example>
    /// <code>
    /// var content = model.MessageContentById[message.Id];
    /// </code>
    /// </example>
    public IReadOnlyDictionary<Guid, BrokerMessageContentInfo> MessageContentById { get; set; } = new Dictionary<Guid, BrokerMessageContentInfo>();

    public IReadOnlyList<BrokerMessageSubscriptionInfo> Subscriptions { get; set; } = [];

    public IReadOnlyList<BrokerMessageInfo> WaitingMessages { get; set; } = [];

    public List<string> Errors { get; } = [];

    public bool IsAvailable { get; set; } = true;
}
