// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Infrastructure;

using BridgingIT.DevKit.Infrastructure.EntityFramework;
using Domain.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class TodoItemEntityTypeConfiguration : IEntityTypeConfiguration<TodoItem>
{
    public void Configure(EntityTypeBuilder<TodoItem> builder)
    {
        builder.ToTable("TodoItems").HasKey(x => x.Id).IsClustered(false);

        builder.Property(e => e.ConcurrencyVersion).
            IsConcurrencyToken()
            .ValueGeneratedNever(); // Tell EF Core to use the application-provided value

        builder.Property(e => e.Id)
            .ValueGeneratedOnAdd()
            .HasConversion(id => id.Value, value => TodoItemId.Create(value));

        builder.Property(x => x.Number)
            .IsRequired();

        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Description)
            .HasMaxLength(2000);

        builder.Property(x => x.Category)
            .HasMaxLength(100);

        builder.Property(x => x.Status)
            .HasConversion(new EnumerationConverter<TodoStatus>())
            .IsRequired();

        builder.Property(x => x.Priority)
            .HasConversion(new EnumerationConverter<TodoPriority>())
            .IsRequired();

        builder.Property(x => x.DueDate);
            //.HasColumnType("datetimeoffset");

        builder.Property(x => x.OrderIndex)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(x => x.UserId)
            .IsRequired();

        builder.Property(x => x.Assignee)
            .HasConversion(email => email.Value, value => EmailAddress.Create(value))
            .HasMaxLength(256);

        builder.OwnsMany(x => x.Steps, sb =>
        {
            sb.Property(s => s.Id)
                .ValueGeneratedOnAdd()
                .HasConversion(id => id.Value, value => TodoStepId.Create(value));

            sb.Property(s => s.Description)
                .IsRequired()
                .HasMaxLength(1000);

            sb.Property(s => s.Status)
                .HasConversion(new EnumerationConverter<TodoStatus>())
                .IsRequired();

            sb.Property(s => s.OrderIndex)
                .IsRequired()
                .HasDefaultValue(0);

            sb.WithOwner()
                .HasForeignKey(s => s.TodoItemId);

            sb.HasIndex(s => s.OrderIndex);
            sb.HasIndex(s => s.Status);
        });

        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.Assignee);

        builder.OwnsOneAuditState(); // TODO: use ToJson variant
        //builder.OwnsOne(e => e.AuditState, b => b.ToJson());
    }
}
