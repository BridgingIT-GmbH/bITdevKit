// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Application.Modules.Core.DataPorter;

using System.Globalization;
using BridgingIT.DevKit.Common.DataPorter;

/// <summary>
/// Import profile for TodoItem using profile-based configuration.
/// Demonstrates validation, required fields, and custom parsing.
/// </summary>
public class TodoItemImportProfile : ImportProfileBase<TodoItemModel>
{
    protected override void Configure()
    {
        this.FromSheet("Todo Items");
        this.HeaderRow(0);
        this.SkipDataRows(0);
        this.OnValidationFailure(ImportValidationBehavior.CollectErrors);

        this.ForColumn(t => t.Id)
            .FromHeader("ID")
            .IsRequired("ID is required");

        this.ForColumn(t => t.Number)
            .FromHeader("Number")
            .IsRequired("Number is required")
            .ParseWith(value => long.Parse(value, CultureInfo.InvariantCulture))
            .Validate(num => num > 0, "Number must be greater than 0");

        this.ForColumn(t => t.Title)
            .FromHeader("Title")
            .IsRequired("Title is required")
            .Validate(title => !string.IsNullOrWhiteSpace(title), "Title cannot be empty")
            .Validate(title => title.Length <= 200, "Title cannot exceed 200 characters");

        this.ForColumn(t => t.Description)
            .FromHeader("Description");

        this.ForColumn(t => t.Status)
            .FromHeader("Status")
            .ParseWith(value => int.Parse(value, CultureInfo.InvariantCulture))
            .Validate(status => status >= 1 && status <= 5, "Status must be between 1 and 5");

        this.ForColumn(t => t.Priority)
            .FromHeader("Priority")
            .ParseWith(value => int.Parse(value, CultureInfo.InvariantCulture))
            .Validate(priority => priority >= 1 && priority <= 4, "Priority must be between 1 and 4");

        this.ForColumn(t => t.DueDate)
            .FromHeader("Due Date")
            .HasFormat("yyyy-MM-dd");

        this.ForColumn(t => t.Assignee)
            .FromHeader("Assignee")
            .Validate(assignee => string.IsNullOrEmpty(assignee) ||
                                  assignee.Contains('@'),
                "Assignee must be a valid email address");

        this.ForColumn(t => t.OrderIndex)
            .FromHeader("Order")
            .ParseWith(value => int.Parse(value, CultureInfo.InvariantCulture));

        this.Ignore(t => t.UserId);
        this.Ignore(t => t.ConcurrencyVersion);
        this.Ignore(t => t.Steps);

        // Use factory to initialize default values
        this.UseFactory(() => new TodoItemModel
        {
            Steps = []
        });
    }
}
