// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework;

using System;
using BridgingIT.DevKit.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

public class DateOnlyComparerTests
{
    [Fact]
    public void Equals_SameDate_ShouldBeTrue()
    {
        // Arrange
        var sut = new DateOnlyComparer();
        var d1 = new DateOnly(2025, 10, 13);
        var d2 = new DateOnly(2025, 10, 13);

        // Act
        var result = sut.Equals(d1, d2);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void Equals_DifferentDate_ShouldBeFalse()
    {
        // Arrange
        var sut = new DateOnlyComparer();
        var d1 = new DateOnly(2025, 10, 13);
        var d2 = new DateOnly(2025, 10, 14);

        // Act
        var result = sut.Equals(d1, d2);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void HashCode_SameDate_ShouldMatch()
    {
        // Arrange
        var sut = new DateOnlyComparer();
        var d1 = new DateOnly(2024, 2, 29);
        var d2 = new DateOnly(2024, 2, 29);

        // Act
        var h1 = sut.GetHashCode(d1);
        var h2 = sut.GetHashCode(d2);

        // Assert
        h1.ShouldBe(h2);
    }

    [Fact]
    public void HashCode_DifferentDate_ShouldDiffer()
    {
        // Arrange
        var sut = new DateOnlyComparer();
        var d1 = new DateOnly(2024, 2, 29);
        var d2 = new DateOnly(2024, 3, 1);

        // Act
        var h1 = sut.GetHashCode(d1);
        var h2 = sut.GetHashCode(d2);

        // Assert
        h1.ShouldNotBe(h2);
    }

    [Fact]
    public void Snapshot_ShouldReturnSameValue()
    {
        // Arrange
        var sut = new DateOnlyComparer();
        var original = new DateOnly(2030, 1, 1);

        // Act
        var snapshot = sut.Snapshot(original);
        var mutated = original.AddDays(5); // value type, original unchanged

        // Assert
        snapshot.ShouldBe(new DateOnly(2030, 1, 1));
        mutated.ShouldBe(new DateOnly(2030, 1, 6));
        sut.Equals(snapshot, mutated).ShouldBeFalse();
    }

    [Fact]
    public void ExtremeValues_ShouldBehaveConsistently()
    {
        // Arrange
        var sut = new DateOnlyComparer();
        var min = DateOnly.MinValue;
        var max = DateOnly.MaxValue;

        // Act & Assert
        sut.Equals(min, min).ShouldBeTrue();
        sut.Equals(max, max).ShouldBeTrue();
        sut.Equals(min, max).ShouldBeFalse();
        sut.GetHashCode(min).ShouldNotBe(sut.GetHashCode(max));
    }

    [Fact]
    public void InEfCore_ChangeTracking_ShouldDetectModification()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TestContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var context = new TestContext(options);
        var person = new Person { Id = 1, BirthDate = new DateOnly(2000, 1, 1) };
        context.Add(person);
        context.SaveChanges();

        // Act
        person.BirthDate = new DateOnly(2000, 1, 2);
        context.ChangeTracker.DetectChanges();
        var isModified = context.Entry(person).Property(p => p.BirthDate).IsModified;

        // Assert
        isModified.ShouldBeTrue();
    }

    [Fact]
    public void InEfCore_ChangeTracking_UnchangedDate_ShouldNotBeModified()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TestContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var context = new TestContext(options);
        var person = new Person { Id = 2, BirthDate = new DateOnly(1999, 12, 31) };
        context.Add(person);
        context.SaveChanges();

        // Act
        person.BirthDate = new DateOnly(1999, 12, 31); // same value
        context.ChangeTracker.DetectChanges();
        var isModified = context.Entry(person).Property(p => p.BirthDate).IsModified;

        // Assert
        isModified.ShouldBeFalse();
    }

    private sealed class TestContext(DbContextOptions<TestContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Person>(b =>
            {
                b.HasKey(e => e.Id);
                b.Property(e => e.BirthDate)
                    .HasConversion(new DateOnlyConverter())
                    .Metadata.SetValueComparer(new DateOnlyComparer());
            });
        }
    }

    private sealed class Person
    {
        public int Id { get; set; }
        public DateOnly BirthDate { get; set; }
    }
}