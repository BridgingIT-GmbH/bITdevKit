// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Application.Orchestrations;
using BridgingIT.DevKit.Common;

public class TodoItemLifecycleOrchestrationData : IOrchestrationData
{
    public Guid TodoItemId { get; set; }

    public long Number { get; set; }

    public string Title { get; set; }

    public string Description { get; set; }

    public string Assignee { get; set; }

    public string Status { get; set; }

    public string Priority { get; set; }

    public DateTime? DueDate { get; set; }

    public DateTime? LeadReminderSentUtc { get; set; }

    public DateTime? LastOverdueReminderSentUtc { get; set; }

    public int OverdueReminderCount { get; set; }

    public bool Deleted { get; set; }

    public string DeletedReason { get; set; }

    public string LastNotificationKind { get; set; }

    public string LastSynchronizedBy { get; set; }

    public DateTime? LastSynchronizedUtc { get; set; }

    public bool ShouldSendLeadReminder { get; set; }

    public bool ShouldSendOverdueReminder { get; set; }

    public List<string> Trace { get; set; } = [];
}
