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
    /// <summary>
    /// Gets or sets the captured at utc.
    /// </summary>
    public DateTimeOffset CapturedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the action base.
    /// </summary>
    public string ActionBase { get; set; } = "/_bdk/dashboard/queueing";

    /// <summary>
    /// Gets or sets the stats.
    /// </summary>
    public QueueMessageStats Stats { get; set; } = new();

    /// <summary>
    /// Gets or sets the summary.
    /// </summary>
    public QueueBrokerSummary Summary { get; set; } = new();

    /// <summary>
    /// Gets or sets the messages.
    /// </summary>
    public IReadOnlyList<QueueMessageInfo> Messages { get; set; } = [];

    /// <summary>
    /// Gets or sets the message content indexed by queue message primary key.
    /// </summary>
    /// <example>
    /// <code>
    /// var content = model.MessageContentById[message.Id];
    /// </code>
    /// </example>
    public IReadOnlyDictionary<Guid, QueueMessageContentInfo> MessageContentById { get; set; } = new Dictionary<Guid, QueueMessageContentInfo>();

    /// <summary>
    /// Gets or sets the subscriptions.
    /// </summary>
    public IReadOnlyList<QueueSubscriptionInfo> Subscriptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the waiting messages.
    /// </summary>
    public IReadOnlyList<QueueMessageInfo> WaitingMessages { get; set; } = [];

    /// <summary>
    /// Gets the errors.
    /// </summary>
    public List<string> Errors { get; } = [];

    /// <summary>
    /// Gets or sets the is available.
    /// </summary>
    public bool IsAvailable { get; set; } = true;
}
