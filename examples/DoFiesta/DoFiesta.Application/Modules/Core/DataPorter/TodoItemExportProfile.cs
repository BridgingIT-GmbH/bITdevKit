// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Application.Modules.Core.DataPorter;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Common.DataPorter;
using BridgingIT.DevKit.Examples.DoFiesta.Domain.Model;

/// <summary>
/// Export profile for TodoItem using profile-based configuration.
/// Demonstrates advanced features like conditional styling and custom formatting.
/// </summary>
public class TodoItemExportProfile : ExportProfileBase<TodoItemModel>
{
    protected override void Configure()
    {
        this.ToSheet("Todo Items");

        this.ForColumn(t => t.Id)
            .HasName("ID")
            .HasOrder(0)
            .HasWidth(25);

        this.ForColumn(t => t.Number)
            .HasName("Number")
            .HasOrder(1)
            .HasWidth(10)
            .Align(HorizontalAlignment.Right);

        this.ForColumn(t => t.Title)
            .HasName("Title")
            .HasOrder(2)
            .HasWidth(40)
            .StyleWhen(title => title.IsNullOrEmpty(), style => style
                .WithBackgroundColor("#FFCCCC")); // Highlight missing titles

        this.ForColumn(t => t.Description)
            .HasName("Description")
            .HasOrder(3)
            .HasWidth(60);

        this.ForColumn(t => t.Status)
            .HasName("Status")
            .HasOrder(4)
            .HasWidth(15)
            .StyleWhen(status => status == 3, style => style // Completed = 3
                .WithBackgroundColor("#90EE90")
                .Bold())
            .StyleWhen(status => status == 4 || status == 5, style => style // Cancelled/Deleted
                .WithBackgroundColor("#D3D3D3")
                .WithForegroundColor("#808080"));

        this.ForColumn(t => t.Priority)
            .HasName("Priority")
            .HasOrder(5)
            .HasWidth(15)
            .StyleWhen(priority => priority == 4, style => style // Critical = 4
                .WithBackgroundColor("#FF6B6B")
                .Bold())
            .StyleWhen(priority => priority == 3, style => style // High = 3
                .WithBackgroundColor("#FFA500"));

        this.ForColumn(t => t.DueDate)
            .HasName("Due Date")
            .HasOrder(6)
            .HasWidth(20)
            .HasFormat("yyyy-MM-dd")
            .StyleWhen(dueDate => dueDate.HasValue && dueDate.Value < DateTime.UtcNow, style => style
                .WithBackgroundColor("#FFD700")); // Highlight overdue items

        this.ForColumn(t => t.Assignee)
            .HasName("Assignee")
            .HasOrder(7)
            .HasWidth(30);

        this.ForColumn(t => t.OrderIndex)
            .HasName("Order")
            .HasOrder(8)
            .HasWidth(10)
            .Align(HorizontalAlignment.Right);

        // Ignore internal fields
        this.Ignore(t => t.UserId);
        this.Ignore(t => t.ConcurrencyVersion);
        this.Ignore(t => t.Steps);

        // Add header and footer
        this.AddHeader("DoFiesta Todo Items Export");
        this.AddFooter(items => $"Total Items: {items.Count()} | Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
    }
}
