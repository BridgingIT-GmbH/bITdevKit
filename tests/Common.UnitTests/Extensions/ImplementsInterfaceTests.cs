// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests;

[UnitTest("Common")]
public class ImplementsInterfaceTests
{
    [Fact]
    public void ImplementsInterface_Tests()
    {
        Assert.True(typeof(Foo).ImplementsInterface(typeof(IFoo)));
        Assert.True(typeof(FooInt).ImplementsInterface(typeof(IFoo)));
        Assert.True(typeof(FooInt).ImplementsInterface(typeof(IFoo<>)));
        Assert.True(typeof(FooInt).ImplementsInterface(typeof(IFoo<int>)));
        Assert.False(typeof(FooInt).ImplementsInterface(typeof(IFoo<string>)));
        Assert.False(typeof(FooInt).ImplementsInterface(typeof(IFoo<,>)));
        Assert.True(typeof(FooStringInt).ImplementsInterface(typeof(IFoo<,>)));
        Assert.True(typeof(FooStringInt).ImplementsInterface(typeof(IFoo<string, int>)));
        Assert.False(typeof(Foo<int, string>).ImplementsInterface(typeof(IFoo<string>)));
    }

#pragma warning disable SA1201 // Elements should appear in the correct order
    public interface IFoo
    {
    }

    public interface IFoo<T> : IFoo
    {
    }

    public interface IFoo<T, TM> : IFoo<T>
    {
    }

    public class Foo : IFoo
    {
    }

    public class Foo<T> : IFoo
    {
    }

    public class Foo<T, TM> : IFoo<T>
    {
    }

    public class FooInt : IFoo<int>
    {
    }

    public class FooStringInt : IFoo<string, int>
    {
    }

    public class Foo2 : Foo
    {
    }
}
#pragma warning restore SA1201 // Elements should appear in the correct order
