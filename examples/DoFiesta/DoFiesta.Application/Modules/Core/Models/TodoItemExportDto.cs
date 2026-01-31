// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Common.DataPorter;

/// <summary>
/// DTO for exporting TodoItem data.
/// Demonstrates attribute-based configuration for DataPorter.
/// </summary>
[DataPorterSheet("Todo Items")]
public class TodoItemExportDto
{
    [DataPorterColumn("ID", Order = 0, Width = 25)]
    public string Id { get; set; }

    [DataPorterColumn("Number", Order = 1, Width = 10, HorizontalAlignment = HorizontalAlignment.Right)]
    public long Number { get; set; }

    [DataPorterColumn("Title", Order = 2, Width = 40, Required = true)]
    public string Title { get; set; }

    [DataPorterColumn("Description", Order = 3, Width = 60)]
    public string Description { get; set; }

    [DataPorterColumn("Status", Order = 4, Width = 15)]
    public string Status { get; set; }

    [DataPorterColumn("Priority", Order = 5, Width = 15)]
    public string Priority { get; set; }

    [DataPorterColumn("Due Date", Order = 6, Width = 20, Format = "yyyy-MM-dd")]
    public DateTime? DueDate { get; set; }

    [DataPorterColumn("Assignee", Order = 7, Width = 30)]
    public string Assignee { get; set; }

    [DataPorterColumn("Order", Order = 8, Width = 10, HorizontalAlignment = HorizontalAlignment.Right)]
    public int OrderIndex { get; set; }

    [DataPorterIgnore]
    public string UserId { get; set; }

    [DataPorterIgnore]
    public string ConcurrencyVersion { get; set; }
}
