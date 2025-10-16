// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Application.Modules.Core;

public class TodoItemModel
{
    public string Id { get; set; }

    public string UserId { get; set; }

    public long Number { get; set; }

    public string Title { get; set; }

    public string Description { get; set; }

    public int Status { get; set; }

    public int Priority { get; set; }

    public DateTime? DueDate { get; set; }

    public int OrderIndex { get; set; }

    public string Assignee { get; set; }

    public ICollection<TodoStepModel> Steps { get; set; } = [];

    public string ConcurrencyVersion { get; set; }
}
