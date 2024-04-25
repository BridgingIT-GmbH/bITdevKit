﻿// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests;

using System;
using Microsoft.Extensions.Primitives;

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
        DateTime.Now.ToString("o").To<DateTime>().ShouldBeOfType<DateTime>().Year.ShouldBe(DateTime.Now.Year);
        DateTime.Now.ToString("o").To(typeof(DateTime)).ShouldBeOfType<DateTime>().Year.ShouldBe(DateTime.Now.Year);
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
        //16.To(typeof(StubEnums)).ShouldBe(StubEnums.Reptile);
        //99.To<StubEnum>().ShouldBe(StubEnum.Unk);
        new StringValues("abc").To(typeof(string)).ShouldBeOfType<string>().ShouldBe("abc");
        new StringValues("abc").To<string>().ShouldBeOfType<string>().ShouldBe("abc");
        new StringValues("42").To(typeof(int)).ShouldBeOfType<int>().ShouldBe(42);
        new StringValues("42").To<int>().ShouldBeOfType<int>().ShouldBe(42);
        "Abc".To(defaultValue: StubEnums.Dog).ShouldBe(StubEnums.None); // defaultvalue ignored with enums
        13.To<StubEnums>().ShouldBe(StubEnums.Dog | StubEnums.Fish | StubEnums.Bird); // dog 1 |fish 4 |bird 8 = 13
        Assert.Throws<FormatException>(() => "test".To<bool>(true));
        Assert.Throws<FormatException>(() => "test".To(true, defaultValue: false));
        Assert.Throws<FormatException>(() => "test".To<int>(true));
        // Assert.Throws<FormatException>(() => "abc".To<StubEnum>(true));
    }

#pragma warning disable SA1201 // Elements should appear in the correct order
    [Flags]
    public enum StubEnums
    {
        None = 0,
        Dog = 1,
        Cat = 2,
        Fish = 4,
        Bird = 8,
        Reptile = 16,
        Other = 32
#pragma warning restore SA1201 // Elements should appear in the correct order
    }
}
