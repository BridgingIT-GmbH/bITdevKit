// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Mapping;

[UnitTest("Common")]
public class MapperExtensionsTests
{
    [Fact]
    public void MapNull_ToNull_Mapped()
    {
        PersonStub source = null;
        var mapper = new StubMapper();
        var target = mapper.Map(source);

        Assert.Null(target);
    }

    [Fact]
    public void MapNull_ToNewObject_Mapped()
    {
        PersonStub source = null;
        var mapper = new StubMapper();
        var target = mapper.Map(source, true);

        Assert.NotNull(target);
    }

    [Fact]
    public void Map_ToNewObject_Mapped()
    {
        var mapper = new StubMapper();
        var target = mapper.Map(new PersonStub { Age = 1 });

        Assert.Equal(1, target.Age);
    }

    [Fact]
    public void MapMany_ToMany_Mapped()
    {
        var mapper = new StubMapper();
        var targets = mapper.Map(new List<PersonStub> { new() { Age = 1 }, new() { Age = 2 } });

        Assert.Equal(2, targets.Count());
        Assert.Equal(1,
            targets.FirstOrDefault()
                ?.Age);
        Assert.Equal(2,
            targets.LastOrDefault()
                ?.Age);
    }

    [Fact]
    public void MapArray_Empty_Mapped()
    {
        var mapper = new StubMapper();

        var targets = mapper.Map([]);

        Assert.Empty(targets);
    }

    [Fact]
    public void MapArray_Mapped()
    {
        var mapper = new StubMapper();
        var targets = mapper.Map([new PersonStub { Age = 1 }, new PersonStub { Age = 2 }]);

        Assert.Equal(2, targets.Count());
        Assert.Equal(1,
            targets.FirstOrDefault()
                ?.Age);
        Assert.Equal(2,
            targets.LastOrDefault()
                ?.Age);
    }
}