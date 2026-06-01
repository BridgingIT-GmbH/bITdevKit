// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Jobs;

using BridgingIT.DevKit.Application.Jobs;
using System.Reflection;

public class CronosJobCronEngineTests
{
    [Theory]
    [MemberData(nameof(CronExpressionConstants))]
    public void Validate_CronExpressionsConstant_ReturnsSuccess(string name, string expression)
    {
        var sut = new CronosJobCronEngine();

        var result = sut.Validate(expression);

        result.IsSuccess.ShouldBeTrue(name);
    }

    [Fact]
    public void Build_EveryMinutes_ReturnsStandardExpression()
    {
        var result = new CronExpressionBuilder()
            .EveryMinutes(30)
            .Build();

        result.ShouldBe("*/30 * * * *");
    }

    [Fact]
    public void Build_DailyAt_ReturnsStandardExpression()
    {
        var result = new CronExpressionBuilder()
            .DailyAt(2, 15)
            .Build();

        result.ShouldBe("15 2 * * *");
    }

    [Fact]
    public void Build_EverySeconds_ReturnsSixFieldExpression()
    {
        var result = new CronExpressionBuilder()
            .EverySeconds(10)
            .Build();

        result.ShouldBe("*/10 * * * * *");
    }

    [Fact]
    public void Build_Ranges_ReturnsStandardExpression()
    {
        var result = new CronExpressionBuilder()
            .MinutesRange(0, 30)
            .HoursRange(8, 17)
            .DayOfMonthRange(1, 10)
            .Build();

        result.ShouldBe("0-30 8-17 1-10 * *");
    }

    [Fact]
    public void Build_AtDateTimeWithoutSeconds_ReturnsStandardExpression()
    {
        var result = new CronExpressionBuilder()
            .AtDateTime(new DateTimeOffset(2026, 6, 2, 9, 30, 0, TimeSpan.Zero))
            .Build();

        result.ShouldBe("30 9 2 6 *");
    }

    [Fact]
    public void Build_AtTimeWithSeconds_ReturnsSixFieldExpression()
    {
        var result = new CronExpressionBuilder()
            .AtTime(9, 30, 15)
            .Build();

        result.ShouldBe("15 30 9 * * *");
    }

    public static TheoryData<string, string> CronExpressionConstants()
    {
        var result = new TheoryData<string, string>();
        var fields = typeof(CronExpressions).GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(field => field is { IsLiteral: true, IsInitOnly: false } && field.FieldType == typeof(string));

        foreach (var field in fields)
        {
            result.Add(field.Name, (string)field.GetRawConstantValue());
        }

        return result;
    }
}
