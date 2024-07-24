// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests;

using System;
using Microsoft.Extensions.Primitives;
using System.Globalization;
using Xunit;
using Shouldly;

[UnitTest("Common")]
public class ToTests
{
    [Fact]
    public void To_Tests()
    {
        const string s = null;
        s.To<int>().ShouldBeOfType<int>().ShouldBe(0);
        "42".To<int>().ShouldBeOfType<int>().ShouldBe(42);
        "42".To(typeof(int)).ShouldBeOfType<int>().ShouldBe(42);
        "ABC".To(defaultValue: 42).ShouldBeOfType<int>().ShouldBe(42);
        "28173829281734".To<long>().ShouldBeOfType<long>().ShouldBe(28173829281734);
        "28173829281734".To(typeof(long)).ShouldBeOfType<long>().ShouldBe(28173829281734);
        DateTime.Now.ToString("o").To<DateTime>().ShouldBeOfType<DateTime>().Year.ShouldBe(DateTime.UtcNow.Year);
        DateTime.Now.ToString("o").To(typeof(DateTime)).ShouldBeOfType<DateTime>().Year.ShouldBe(DateTime.UtcNow.Year);
        "2.0".To<double>().ShouldBe(2.0);
        "2.0".To(typeof(double)).ShouldBe(2.0);
        "0.2".To<double>().ShouldBe(0.2);
        2.0.To<int>().ShouldBe(2);
        "false".To<bool>().ShouldBeOfType<bool>().ShouldBe(false);
        "True".To<bool>().ShouldBeOfType<bool>().ShouldBe(true);
        "True".To(typeof(bool)).ShouldBeOfType<bool>().ShouldBe(true);
        "ABC".To(defaultValue: true).ShouldBeOfType<bool>().ShouldBe(true);
        "2260afec-bbfd-42d4-a91a-dcb11e09b17f".To<Guid>().ShouldBeOfType<Guid>().ShouldBe(new Guid("2260afec-bbfd-42d4-a91a-dcb11e09b17f"));
        "2260afec-bbfd-42d4-a91a-dcb11e09b17f".To(typeof(Guid)).ShouldBeOfType<Guid>().ShouldBe(new Guid("2260afec-bbfd-42d4-a91a-dcb11e09b17f"));
        "ABC".To<Guid>().ShouldBeOfType<Guid>().ShouldBe(Guid.Empty);
        s.To<Guid>().ShouldBeOfType<Guid>().ShouldBe(Guid.Empty);
        s.To(typeof(Guid)).ShouldBeOfType<Guid>().ShouldBe(Guid.Empty);
        "Reptile".To<StubEnums>().ShouldBe(StubEnums.Reptile);
        "Reptile".To(typeof(StubEnums)).ShouldBe(StubEnums.Reptile);
        16.To<StubEnums>().ShouldBe(StubEnums.Reptile);
        new StringValues("abc").To(typeof(string)).ShouldBeOfType<string>().ShouldBe("abc");
        new StringValues("abc").To<string>().ShouldBeOfType<string>().ShouldBe("abc");
        new StringValues("42").To(typeof(int)).ShouldBeOfType<int>().ShouldBe(42);
        new StringValues("42").To<int>().ShouldBeOfType<int>().ShouldBe(42);
        "Abc".To(defaultValue: StubEnums.Dog).ShouldBe(StubEnums.None); // defaultvalue ignored with enums
        13.To<StubEnums>().ShouldBe(StubEnums.Dog | StubEnums.Fish | StubEnums.Bird); // dog 1 |fish 4 |bird 8 = 13
        Assert.Throws<FormatException>(() => "test".To<bool>(true));
        Assert.Throws<FormatException>(() => "test".To(true, defaultValue: false));
        Assert.Throws<FormatException>(() => "test".To<int>(true));
    }

    [Fact]
    public void TryTo_Tests()
    {
        const string s = null;
        s.TryTo<int>(out var nullResult).ShouldBeFalse();
        nullResult.ShouldBe(0);

        "42".TryTo<int>(out var intResult).ShouldBeTrue();
        intResult.ShouldBe(42);

        "42".TryTo(typeof(int), out var objIntResult).ShouldBeTrue();
        objIntResult.ShouldBeOfType<int>().ShouldBe(42);

        "ABC".TryTo<int>(out var invalidIntResult).ShouldBeFalse();
        invalidIntResult.ShouldBe(0);

        "28173829281734".TryTo<long>(out var longResult).ShouldBeTrue();
        longResult.ShouldBe(28173829281734);

        "28173829281734".TryTo(typeof(long), out var objLongResult).ShouldBeTrue();
        objLongResult.ShouldBeOfType<long>().ShouldBe(28173829281734);

        var now = DateTime.Now;
        now.ToString("o").TryTo<DateTime>(out var dateTimeResult).ShouldBeTrue();
        dateTimeResult.ShouldBeOfType<DateTime>().Year.ShouldBe(now.Year);

        now.ToString("o").TryTo(typeof(DateTime), out var objDateTimeResult).ShouldBeTrue();
        objDateTimeResult.ShouldBeOfType<DateTime>().Year.ShouldBe(now.Year);

        "2.0".TryTo<double>(out var doubleResult).ShouldBeTrue();
        doubleResult.ShouldBe(2.0);

        "2.0".TryTo(typeof(double), out var objDoubleResult).ShouldBeTrue();
        objDoubleResult.ShouldBeOfType<double>().ShouldBe(2.0);

        "0.2".TryTo<double>(out var decimalDoubleResult).ShouldBeTrue();
        decimalDoubleResult.ShouldBe(0.2);

        2.0.TryTo<int>(out var doubleToIntResult).ShouldBeTrue();
        doubleToIntResult.ShouldBe(2);

        "false".TryTo<bool>(out var boolResult).ShouldBeTrue();
        boolResult.ShouldBeFalse();

        "True".TryTo<bool>(out var trueBoolResult).ShouldBeTrue();
        trueBoolResult.ShouldBeTrue();

        "True".TryTo(typeof(bool), out var objBoolResult).ShouldBeTrue();
        objBoolResult.ShouldBeOfType<bool>().ShouldBeTrue();

        "ABC".TryTo<bool>(out var invalidBoolResult).ShouldBeFalse();
        invalidBoolResult.ShouldBeFalse();

        var guid = new Guid("2260afec-bbfd-42d4-a91a-dcb11e09b17f");
        guid.ToString().TryTo<Guid>(out var guidResult).ShouldBeTrue();
        guidResult.ShouldBe(guid);

        guid.ToString().TryTo(typeof(Guid), out var objGuidResult).ShouldBeTrue();
        objGuidResult.ShouldBeOfType<Guid>().ShouldBe(guid);

        "ABC".TryTo<Guid>(out var invalidGuidResult).ShouldBeFalse();
        invalidGuidResult.ShouldBe(Guid.Empty);

        s.TryTo<Guid>(out var nullGuidResult).ShouldBeFalse();
        nullGuidResult.ShouldBe(Guid.Empty);

        //s.TryTo(typeof(Guid), out var objNullGuidResult).ShouldBeFalse();
        //objNullGuidResult.ShouldBeOfType<Guid>().ShouldBe(Guid.Empty);

        "Reptile".TryTo<StubEnums>(out var enumResult).ShouldBeTrue();
        enumResult.ShouldBe(StubEnums.Reptile);

        "Reptile".TryTo(typeof(StubEnums), out var objEnumResult).ShouldBeTrue();
        objEnumResult.ShouldBeOfType<StubEnums>().ShouldBe(StubEnums.Reptile);

        16.TryTo<StubEnums>(out var intEnumResult).ShouldBeTrue();
        intEnumResult.ShouldBe(StubEnums.Reptile);

        //new StringValues("abc").TryTo(typeof(string), out var stringValuesResult).ShouldBeTrue();
        //stringValuesResult.ShouldBeOfType<string>().ShouldBe("abc");

        //new StringValues("abc").TryTo<string>(out var genericStringValuesResult).ShouldBeTrue();
        //genericStringValuesResult.ShouldBeOfType<string>().ShouldBe("abc");

        //new StringValues("42").TryTo(typeof(int), out var stringValuesIntResult).ShouldBeTrue();
        //stringValuesIntResult.ShouldBeOfType<int>().ShouldBe(42);

        //new StringValues("42").TryTo<int>(out var genericStringValuesIntResult).ShouldBeTrue();
        //genericStringValuesIntResult.ShouldBeOfType<int>().ShouldBe(42);

        "Abc".TryTo<StubEnums>(out var invalidEnumResult).ShouldBeFalse();
        invalidEnumResult.ShouldBe(StubEnums.None);

        13.TryTo<StubEnums>(out var combinedEnumResult).ShouldBeTrue();
        combinedEnumResult.ShouldBe(StubEnums.Dog | StubEnums.Fish | StubEnums.Bird);
    }

    [Fact]
    public void TryTo_DateTime_Tests()
    {
        "2023-01-01".TryTo<DateTime>(out var result).ShouldBeTrue();
        result.ShouldBe(new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        "invalid date".TryTo<DateTime>(out var invalidResult).ShouldBeFalse();
        invalidResult.ShouldBe(default(DateTime));

        "2023-01-01".TryTo(typeof(DateTime), out var objResult).ShouldBeTrue();
        objResult.ShouldBeOfType<DateTime>().ShouldBe(new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        "01/01/2023".TryTo<DateTime>(out var usResult, new CultureInfo("en-US")).ShouldBeTrue();
        usResult.ShouldBe(new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        "01.01.2023".TryTo<DateTime>(out var deResult, new CultureInfo("de-DE")).ShouldBeTrue();
        deResult.ShouldBe(new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        "2023-01-01T12:30:45".TryTo<DateTime>(out var dateTimeWithTimeResult).ShouldBeTrue();
        dateTimeWithTimeResult.ShouldBe(new DateTime(2023, 1, 1, 12, 30, 45, DateTimeKind.Utc));

        "2023-01-01Z".TryTo<DateTime>(out var utcDateTimeResult).ShouldBeTrue();
        utcDateTimeResult.ShouldBe(new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        "31/12/2023".TryTo<DateTime>(out var ukResult, new CultureInfo("en-GB")).ShouldBeTrue();
        ukResult.ShouldBe(new DateTime(2023, 12, 31, 0, 0, 0, DateTimeKind.Utc));

        DateTime.Now.ToString("o").TryTo<DateTime>(out var roundTripResult).ShouldBeTrue();
        roundTripResult.Kind.ShouldBe(DateTimeKind.Utc);

        "9999-12-31T23:59:59.9999999".TryTo<DateTime>(out var maxValueResult).ShouldBeTrue();
        maxValueResult.ShouldBe(DateTime.MaxValue);

        "0001-01-01T00:00:00".TryTo<DateTime>(out var minValueResult).ShouldBeTrue();
        minValueResult.ShouldBe(DateTime.MinValue);

        // Test for local time conversion
        var localTime = new DateTime(2023, 1, 1, 12, 0, 0, DateTimeKind.Local);
        localTime.ToString("o").TryTo<DateTime>(out var localTimeResult).ShouldBeTrue();
        localTimeResult.ShouldBe(localTime.ToUniversalTime());
        localTimeResult.Kind.ShouldBe(DateTimeKind.Utc);
    }

    [Flags]
#pragma warning disable SA1201 // Elements should appear in the correct order
    public enum StubEnums
#pragma warning restore SA1201 // Elements should appear in the correct order
    {
        None = 0,
        Dog = 1,
        Cat = 2,
        Fish = 4,
        Bird = 8,
        Reptile = 16,
        Other = 32
    }
}