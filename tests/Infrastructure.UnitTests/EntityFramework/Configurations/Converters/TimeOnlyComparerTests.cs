// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework;

using BridgingIT.DevKit.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using System;
using Xunit;

public class TimeOnlyComparerTests
{
    [Fact]
    public void Equals_SameTime_ShouldBeTrue()
    {
        // Arrange
        var sut = new TimeOnlyComparer();
        var t1 = new TimeOnly(12, 30, 15, 250);
        var t2 = new TimeOnly(12, 30, 15, 250);

        // Act
        var result = sut.Equals(t1, t2);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void Equals_DifferentTime_ShouldBeFalse()
    {
        // Arrange
        var sut = new TimeOnlyComparer();
        var t1 = new TimeOnly(12, 30, 15, 250);
        var t2 = new TimeOnly(12, 30, 16, 250);

        // Act
        var result = sut.Equals(t1, t2);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void HashCode_SameTime_ShouldMatch()
    {
        // Arrange
        var sut = new TimeOnlyComparer();
        var t1 = new TimeOnly(0, 0, 0);
        var t2 = new TimeOnly(0, 0, 0);

        // Act
        var h1 = sut.GetHashCode(t1);
        var h2 = sut.GetHashCode(t2);

        // Assert
        h1.ShouldBe(h2);
    }

    [Fact]
    public void HashCode_DifferentTime_ShouldDiffer()
    {
        // Arrange
        var sut = new TimeOnlyComparer();
        var t1 = new TimeOnly(0, 0, 0);
        var t2 = new TimeOnly(0, 0, 1);

        // Act
        var h1 = sut.GetHashCode(t1);
        var h2 = sut.GetHashCode(t2);

        // Assert
        h1.ShouldNotBe(h2);
    }

    [Fact]
    public void Snapshot_ShouldReturnSameValue()
    {
        // Arrange
        var sut = new TimeOnlyComparer();
        var original = new TimeOnly(8, 15, 0);

        // Act
        var snapshot = sut.Snapshot(original);
        var mutated = original.AddHours(1); // value type reassignment

        // Assert
        snapshot.ShouldBe(new TimeOnly(8, 15, 0));
        mutated.ShouldBe(new TimeOnly(9, 15, 0));
        sut.Equals(snapshot, mutated).ShouldBeFalse();
    }

    [Fact]
    public void ExtremeValues_ShouldBehaveConsistently()
    {
        // Arrange
        var sut = new TimeOnlyComparer();
        var min = TimeOnly.MinValue;
        var max = TimeOnly.MaxValue;

        // Act & Assert
        sut.Equals(min, min).ShouldBeTrue();
        sut.Equals(max, max).ShouldBeTrue();
        sut.Equals(min, max).ShouldBeFalse();
        sut.GetHashCode(min).ShouldNotBe(sut.GetHashCode(max));
    }

    [Fact]
    public void Converter_RoundTrip_ShouldPreserveTicks()
    {
        // Arrange
        var converter = new TimeOnlyConverter();
        var times = new[]
        {
            new TimeOnly(0,0,0),
            new TimeOnly(12,30,15,500),
            new TimeOnly(23,59,59,999),
            TimeOnly.MaxValue
        };

        foreach (var modelValue in times)
        {
            // Act
            var providerValue = converter.ConvertToProvider(modelValue);
            var roundTripped = converter.ConvertFromProvider((TimeSpan)providerValue);

            // Assert
            providerValue.ShouldBeOfType<TimeSpan>();
            ((TimeSpan)providerValue).Ticks.ShouldBe(modelValue.Ticks);
            roundTripped.ShouldBe(modelValue);
        }
    }

    [Fact]
    public void InEfCore_ChangeTracking_ModifiedTime_ShouldBeDetected()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TestContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var context = new TestContext(options);
        var entity = new Schedule { Id = 1, StartTime = new TimeOnly(8, 0, 0) };
        context.Add(entity);
        context.SaveChanges();

        // Act
        entity.StartTime = new TimeOnly(9, 0, 0);
        context.ChangeTracker.DetectChanges();
        var isModified = context.Entry(entity).Property(e => e.StartTime).IsModified;

        // Assert
        isModified.ShouldBeTrue();
    }

    [Fact]
    public void InEfCore_ChangeTracking_UnchangedTime_ShouldNotBeModified()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TestContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var context = new TestContext(options);
        var entity = new Schedule { Id = 2, StartTime = new TimeOnly(8, 0, 0) };
        context.Add(entity);
        context.SaveChanges();

        // Act
        entity.StartTime = new TimeOnly(8, 0, 0);
        context.ChangeTracker.DetectChanges();
        var isModified = context.Entry(entity).Property(e => e.StartTime).IsModified;

        // Assert
        isModified.ShouldBeFalse();
    }

    private sealed class TestContext(DbContextOptions<TestContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Schedule>(b =>
            {
                b.HasKey(e => e.Id);
                b.Property(e => e.StartTime)
                    .HasConversion(new TimeOnlyConverter())
                    .Metadata.SetValueComparer(new TimeOnlyComparer());
            });
        }
    }

    private sealed class Schedule
    {
        public int Id { get; set; }
        public TimeOnly StartTime { get; set; }
    }
}