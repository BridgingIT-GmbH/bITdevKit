// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Serialization;

using System.Text.Json.Serialization;

public abstract class SerializerTestsBase(ITestOutputHelper output) : TestsBase(output)
{
    public virtual void CanRoundTripStream_Test()
    {
        // Arrange
        var sut = this.GetSerializer();
        var value = new StubModel
            {
                IntProperty = 1,
                StringProperty =
                    "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.",
                InitProperty =
                    "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.",
                ListProperty = [1],
                ObjectProperty = new StubModel { IntProperty = 1 }
            }.AddItem(5)
            .AddItem(9);

        // Act
        using var stream = new MemoryStream();
        sut.Serialize(value, stream);
        var newModel = sut.Deserialize<StubModel>(stream);

        // Assert
        stream.ShouldNotBeNull();
        stream.Length.ShouldBeGreaterThan(0);
        newModel.ShouldNotBeNull();
        newModel.IntProperty.ShouldBe(value.IntProperty);
        newModel.StringProperty.ShouldBe(value.StringProperty);
        newModel.InitProperty.ShouldBe(value.InitProperty);
        newModel.Items.ShouldContain(5);
        newModel.Items.ShouldContain(9);
    }

    public virtual void CanRoundTripPrivateConstructorStream_Test()
    {
        // Arrange
        var sut = this.GetSerializer();
        var value = PrivateCtorStubModel.Create(1,
            "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.",
            "Lorem");

        // Act
        using var stream = new MemoryStream();
        sut.Serialize(value, stream);
        var newModel = sut.Deserialize<PrivateCtorStubModel>(stream);

        // Assert
        stream.ShouldNotBeNull();
        stream.Length.ShouldBeGreaterThan(0);
        newModel.ShouldNotBeNull();
        newModel.IntProperty.ShouldBe(value.IntProperty);
        newModel.StringProperty.ShouldBe(value.StringProperty);
        newModel.NoSetProperty.ShouldBeNullOrEmpty(); // properties with no setter are not deserialized (only private or init setters are)
        newModel.InitProperty.ShouldBe(value.InitProperty);
    }

    public virtual void CanRoundTripStream_Benchmark()
    {
        var sut = this.GetSerializer();
        var value = new StubModel
            {
                IntProperty = 1,
                StringProperty =
                    "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.",
                InitProperty =
                    "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.",
                ListProperty = [1],
                ObjectProperty = new StubModel { IntProperty = 1 }
            }.AddItem(5)
            .AddItem(9);

        this.Benchmark(() =>
            {
                using var stream = new MemoryStream();
                sut.Serialize(value, stream);
                sut.Deserialize<StubModel>(stream);
            },
            1000);
    }

    public virtual void CanRoundTripEmptyStream_Test()
    {
        // Arrange
        var sut = this.GetSerializer();
        StubModel value = null;

        // Act
        using var stream = new MemoryStream();
        sut.Serialize(value, stream);
        var newModel = sut.Deserialize<StubModel>(stream);

        // Assert
        stream.ShouldNotBeNull();
        stream.Length.ShouldBe(0);
        newModel.ShouldBeNull();
    }

    public virtual void CanRoundTripBytes_Test()
    {
        var serializer = this.GetSerializer();
        if (serializer is null)
        {
            return;
        }

        var value = new StubModel
            {
                IntProperty = 1,
                StringProperty =
                    "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.",
                InitProperty =
                    "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.",
                ListProperty = [1],
                ObjectProperty = new StubModel { IntProperty = 1 }
            }.AddItem(5)
            .AddItem(9);

        var bytes = serializer.SerializeToBytes(value);
        var actual = serializer.Deserialize<StubModel>(bytes);
        actual.IntProperty.ShouldBe(value.IntProperty);
        actual.StringProperty.ShouldBe(value.StringProperty);
        actual.InitProperty.ShouldBe(value.InitProperty);
        actual.ListProperty.ShouldBe(value.ListProperty);
        actual.ObjectProperty.ShouldNotBeNull();
        actual.Items.ShouldContain(5);
        actual.Items.ShouldContain(9);
        ((int)((dynamic)value.ObjectProperty).IntProperty).ShouldBe(1);

        var text = serializer.SerializeToString(value);
        actual = serializer.Deserialize<StubModel>(text);
        actual.IntProperty.ShouldBe(value.IntProperty);
        actual.StringProperty.ShouldBe(value.StringProperty);
        actual.InitProperty.ShouldBe(value.InitProperty);
        actual.ListProperty.ShouldBe(value.ListProperty);
        actual.ObjectProperty.ShouldNotBeNull();
        actual.Items.ShouldContain(5);
        actual.Items.ShouldContain(9);
        ((int)((dynamic)value.ObjectProperty).IntProperty).ShouldBe(1);
    }

    public virtual void CanRoundTripString_Test()
    {
        var serializer = this.GetSerializer();
        if (serializer is null)
        {
            return;
        }

        var value = new StubModel
            {
                IntProperty = 1,
                StringProperty =
                    "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.",
                InitProperty =
                    "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.",
                ListProperty = [1],
                ObjectProperty = new StubModel { IntProperty = 1 }
            }.AddItem(5)
            .AddItem(9);

        var text = serializer.SerializeToString(value);
        if (serializer is ITextSerializer)
        {
            Assert.Contains("Lorem ipsum dolor sit amet", text);
        }

        var actual = serializer.Deserialize<StubModel>(text);
        actual.IntProperty.ShouldBe(value.IntProperty);
        actual.StringProperty.ShouldBe(value.StringProperty);
        actual.InitProperty.ShouldBe(value.InitProperty);
        actual.ListProperty.ShouldBe(value.ListProperty);
        actual.ObjectProperty.ShouldNotBeNull();
        actual.Items.ShouldContain(5);
        actual.Items.ShouldContain(9);
        ((int)((dynamic)value.ObjectProperty).IntProperty).ShouldBe(1);
    }

    protected virtual ISerializer GetSerializer()
    {
        return null;
    }
}

public class StubModel
{
    private readonly List<int> items = [];

    public int IntProperty { get; set; }

    public string StringProperty { get; set; }

    public string InitProperty { get; init; }

    public List<int> ListProperty { get; set; }

    //public IReadOnlyList<int> Items => this.items.AsReadOnly();

    public IReadOnlyList<int> Items
    {
        get => this.items;
        init => this.items = [..value]; // init needed for systemtextjson deserialization
    }

    public object ObjectProperty { get; set; }

    public StubModel AddItem(int value)
    {
        this.items.Add(value);

        return this;
    }
}

public class PrivateCtorStubModel
{
    private PrivateCtorStubModel() { }

    private PrivateCtorStubModel(int intPropery, string stringProperty)
    {
        this.IntProperty = intPropery;
        this.StringProperty = stringProperty;
        this.NoSetProperty = stringProperty;
    }

    public int IntProperty { get; set; }

    [JsonInclude] // needed for SystemTextJson Deserializer to use private setters, MS prefers init setters
    public string StringProperty { get; private set; }

    public string NoSetProperty { get; } // never deserialize as there is no setter here;

    public string InitProperty { get; init; }

    public static PrivateCtorStubModel Create(int intPropery, string stringProperty, string initProperty)
    {
        return new PrivateCtorStubModel(intPropery, stringProperty) { InitProperty = initProperty };
    }
}