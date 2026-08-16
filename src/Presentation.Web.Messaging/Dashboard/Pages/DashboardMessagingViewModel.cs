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
    /// <summary>
    /// Gets or sets the captured at utc.
    /// </summary>
    public DateTimeOffset CapturedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the action base.
    /// </summary>
    public string ActionBase { get; set; } = "/_bdk/dashboard/messaging";

    /// <summary>
    /// Gets or sets the stats.
    /// </summary>
    public BrokerMessageStats Stats { get; set; } = new();

    /// <summary>
    /// Gets or sets the summary.
    /// </summary>
    public BrokerMessageBrokerSummary Summary { get; set; } = new();

    /// <summary>
    /// Gets or sets the messages.
    /// </summary>
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

    /// <summary>
    /// Gets or sets the subscriptions.
    /// </summary>
    public IReadOnlyList<BrokerMessageSubscriptionInfo> Subscriptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the waiting messages.
    /// </summary>
    public IReadOnlyList<BrokerMessageInfo> WaitingMessages { get; set; } = [];

    /// <summary>
    /// Gets the errors.
    /// </summary>
    public List<string> Errors { get; } = [];

    /// <summary>
    /// Gets or sets the is available.
    /// </summary>
    public bool IsAvailable { get; set; } = true;
}
