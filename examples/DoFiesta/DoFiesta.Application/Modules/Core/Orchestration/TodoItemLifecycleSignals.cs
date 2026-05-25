// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Application.Modules.Core;

public static class TodoItemLifecycleSignals
{
    public const string Updated = "TodoUpdated";

    public const string Deleted = "TodoDeleted";
}

public class TodoItemLifecycleUpdateSignal
{
    public string Title { get; set; }

    public string Description { get; set; }

    public string Assignee { get; set; }

    public string Status { get; set; }

    public string Priority { get; set; }

    public DateTime? DueDate { get; set; }

    public string SynchronizedBy { get; set; }

    public DateTime SynchronizedUtc { get; set; }
}

public class TodoItemLifecycleDeletedSignal
{
    public string Reason { get; set; }

    public string DeletedBy { get; set; }

    public DateTime DeletedUtc { get; set; }
}