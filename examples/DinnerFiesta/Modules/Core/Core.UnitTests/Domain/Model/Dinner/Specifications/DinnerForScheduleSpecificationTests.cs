// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.UnitTests.Domain;

using Core.Domain;

public class DinnerForScheduleSpecificationTests
{
    [Fact]
    public void Test1()
    {
        // Arrange
        var ticks = DateTime.UtcNow.Ticks;
        var dinner = Stubs.Dinners(ticks).FirstOrDefault();
        var dinners = new List<Dinner>();

        var otherDinner0 = dinner.Clone(); // before
        otherDinner0.Id = DinnerId.Create();
        otherDinner0.ChangeSchedule(DinnerSchedule.Create(dinner.Schedule.StartDateTime.AddHours(-5),
            dinner.Schedule.EndDateTime.AddHours(-6)));

        var otherDinner1 = dinner.Clone(); // before and within
        otherDinner1.Id = DinnerId.Create();
        otherDinner1.ChangeSchedule(DinnerSchedule.Create(dinner.Schedule.StartDateTime.AddHours(-2),
            dinner.Schedule.EndDateTime.AddHours(-1)));

        var otherDinner2 = dinner.Clone(); // within and after
        otherDinner2.Id = DinnerId.Create();
        otherDinner2.ChangeSchedule(DinnerSchedule.Create(dinner.Schedule.StartDateTime.AddHours(1),
            dinner.Schedule.EndDateTime.AddHours(5)));

        var otherDinner3 = dinner.Clone(); // within
        otherDinner3.Id = DinnerId.Create();
        otherDinner3.ChangeSchedule(DinnerSchedule.Create(dinner.Schedule.StartDateTime.AddHours(1),
            dinner.Schedule.EndDateTime.AddHours(-1)));

        var otherDinner4 = dinner.Clone(); // after
        otherDinner4.Id = DinnerId.Create();
        otherDinner4.ChangeSchedule(DinnerSchedule.Create(dinner.Schedule.StartDateTime.AddHours(5),
            dinner.Schedule.EndDateTime.AddHours(6)));

        dinners.AddRange([otherDinner0, otherDinner1, otherDinner2, otherDinner3, otherDinner4]);

        // Act
        var sut = new DinnerForScheduleSpecification(dinner.HostId, dinner.Schedule);
        var result = dinners.Where(sut.ToExpression().Compile()).ToArray();

        // Assert
        result.Contains(otherDinner0).ShouldBeFalse();
        result.Contains(otherDinner1).ShouldBeTrue();
        result.Contains(otherDinner2).ShouldBeTrue();
        result.Contains(otherDinner3).ShouldBeTrue();
        result.Contains(otherDinner4).ShouldBeFalse();
    }
}