// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Serialization;

using System.Runtime.Serialization;

[UnitTest("Common")]
public class CborContractTests
{
    [Fact]
    public void Serialize_DataContract_UsesOptInMembersAndWireNames()
    {
        var sut = new CborSerializer();
        var value = new ContractModel { Name = "fixture", Excluded = "exclude", Unmarked = "unmarked" };
        value.Update(42, "private");

        var bytes = sut.SerializeToBytes(value);
        var members = sut.Deserialize<Dictionary<string, object>>(bytes);
        var actual = sut.Deserialize<ContractModel>(bytes);

        members.Keys.Order().ShouldBe(new[] { "base", "secret", "wire_name" });
        members["base"].ShouldBe(42L);
        actual.Name.ShouldBe("fixture");
        actual.ReadBase().ShouldBe(42);
        actual.ReadSecret().ShouldBe("private");
        actual.Excluded.ShouldBe("default");
        actual.Unmarked.ShouldBe("default");
    }

    [Fact]
    public void Deserialize_IgnoredMembers_DoesNotAssignPayloadValues()
    {
        var sut = new CborSerializer();
        var bytes = sut.SerializeToBytes(new Dictionary<string, string>
        {
            ["Excluded"] = "payload", ["Unmarked"] = "payload", ["Name"] = "wrong-name", ["wire_name"] = "correct-name"
        });

        var actual = sut.Deserialize<ContractModel>(bytes);

        actual.Excluded.ShouldBe("default");
        actual.Unmarked.ShouldBe("default");
        actual.Name.ShouldBe("correct-name");
    }

    [Fact]
    public void Serialize_ConventionModel_AllowsAnnotatedPrivateMembers()
    {
        var sut = new CborSerializer();
        var value = new ConventionModel { Value = 42, Excluded = "exclude" };
        value.Update("private", "not-included");

        var bytes = sut.SerializeToBytes(value);
        var members = sut.Deserialize<Dictionary<string, object>>(bytes);
        var actual = sut.Deserialize<ConventionModel>(bytes);

        members.Keys.Order().ShouldBe(new[] { "private", "value" });
        actual.Value.ShouldBe(42);
        actual.ReadPrivate().ShouldBe("private");
        actual.ReadUnmarked().ShouldBe("default");
    }

    [Fact]
    public void Serialize_IgnoredHiddenMember_DoesNotExposeBaseMember()
    {
        var sut = new CborSerializer();
        var value = new IgnoredHiddenModel();
        ((HiddenBaseModel)value).Value = "base";

        var members = sut.Deserialize<Dictionary<string, object>>(sut.SerializeToBytes(value));

        members.ShouldBeEmpty();
    }

    [Fact]
    public void Serialize_DuplicateWireNames_ThrowsNotSupportedException()
    {
        var sut = new CborSerializer();

        Should.Throw<NotSupportedException>(() => sut.SerializeToBytes(new DuplicateNamesModel()));
    }

    [Fact]
    public void Serialize_SelectedConstructor_BindsClrNamesAndOverridesDefaultConstructor()
    {
        var sut = new CborSerializer();
        var value = new SelectedConstructorModel(42) { Extra = "set-after-construction" };

        var bytes = sut.SerializeToBytes(value);
        var actual = sut.Deserialize<SelectedConstructorModel>(bytes);
        var members = sut.Deserialize<Dictionary<string, object>>(bytes);

        actual.Value.ShouldBe(42);
        actual.UsedSelected.ShouldBeTrue();
        actual.Extra.ShouldBe("set-after-construction");
        members.ShouldContainKey("wire_value");
        members.ShouldNotContainKey("Value");
    }

    [Fact]
    public void Deserialize_SelectedPrivateConstructor_UsesOptionalParameterDefault()
    {
        var sut = new CborSerializer();
        var missing = sut.SerializeToBytes(new Dictionary<string, object>());
        var supplied = sut.SerializeToBytes(new Dictionary<string, object> { ["Value"] = 9 });

        sut.Deserialize<PrivateSelectedConstructorModel>(missing).Value.ShouldBe(42);
        sut.Deserialize<PrivateSelectedConstructorModel>(supplied).Value.ShouldBe(9);
    }

    [Fact]
    public void Serialize_MultipleSelectedConstructors_ThrowsNotSupportedException()
    {
        var sut = new CborSerializer();

        Should.Throw<NotSupportedException>(() => sut.SerializeToBytes(new MultipleConstructorsModel()));
    }

    [Fact]
    public void Serialize_UnbindableSelectedConstructor_ThrowsNotSupportedException()
    {
        var sut = new CborSerializer();

        Should.Throw<NotSupportedException>(() => sut.SerializeToBytes(new UnbindableConstructorModel()));
    }

    [Theory]
    [InlineData("https://example.com/a%2Fb?q=a%20b")]
    [InlineData("../relative?q=1")]
    [InlineData("")]
    public void Serialize_Uri_PreservesOriginalText(string text)
    {
        var sut = new CborSerializer();
        var value = new Uri(text, UriKind.RelativeOrAbsolute);

        var bytes = sut.SerializeToBytes(value);
        var actual = sut.Deserialize<Uri>(bytes);

        actual.OriginalString.ShouldBe(text);
        actual.IsAbsoluteUri.ShouldBe(value.IsAbsoluteUri);
        sut.Deserialize<string>(bytes).ShouldBe(text);
    }

    [Theory]
    [InlineData("1.2")]
    [InlineData("1.2.3")]
    [InlineData("1.2.3.4")]
    public void Serialize_Version_PreservesComponents(string text)
    {
        var sut = new CborSerializer();
        var value = new System.Version(text);

        var bytes = sut.SerializeToBytes(value);

        sut.Deserialize<System.Version>(bytes).ShouldBe(value);
        sut.Deserialize<string>(bytes).ShouldBe(text);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Serialize_ByteArraySegment_EncodesOnlySelectedBytes(bool empty)
    {
        var sut = new CborSerializer();
        var value = empty ? default : new ArraySegment<byte>([9, 1, 2, 3, 9], 1, 3);

        var bytes = sut.SerializeToBytes(value);
        var actual = sut.Deserialize<ArraySegment<byte>>(bytes);

        Convert.ToHexString(bytes).ShouldBe(empty ? "40" : "43010203");
        actual.ToArray().ShouldBe(empty ? [] : new byte[] { 1, 2, 3 });
    }

    [Fact]
    public void Serialize_NewScalarCodecs_RoundTripsNestedMembers()
    {
        var sut = new CborSerializer();
        var value = new CodecModel { Location = new Uri("relative", UriKind.Relative), Version = new System.Version(1, 2), Bytes = new ArraySegment<byte>([1, 2]) };

        var actual = sut.Deserialize<CodecModel>(sut.SerializeToBytes(value));

        actual.Location.ShouldBe(value.Location);
        actual.Version.ShouldBe(value.Version);
        actual.Bytes.ToArray().ShouldBe(value.Bytes.ToArray());
    }

    private class ContractBase
    {
        [DataMember(Name = "base")]
        private int baseValue;

        public int ReadBase() => this.baseValue;

        protected void SetBase(int value) => this.baseValue = value;
    }

    [DataContract]
    private sealed class ContractModel : ContractBase
    {
        [DataMember(Name = "wire_name")]
        public string Name { get; set; }

        [DataMember(Name = "secret")]
        private string Secret { get; set; }

        [DataMember, IgnoreDataMember]
        public string Excluded { get; set; } = "default";

        public string Unmarked { get; set; } = "default";

        public void Update(int value, string secret)
        {
            this.SetBase(value);
            this.Secret = secret;
        }

        public string ReadSecret() => this.Secret;
    }

    private sealed class ConventionModel
    {
        private string unmarked = "default";

        [DataMember(Name = "private")]
        private string Private { get; set; }

        [DataMember(Name = "value")]
        public int Value { get; set; }

        [IgnoreDataMember]
        public string Excluded { get; set; }

        public string ReadPrivate() => this.Private;
        public string ReadUnmarked() => this.unmarked;
        public void Update(string value, string hidden)
        {
            this.Private = value;
            this.unmarked = hidden;
        }
    }

    private class HiddenBaseModel
    {
        public string Value { get; set; }
    }

    private sealed class IgnoredHiddenModel : HiddenBaseModel
    {
        [IgnoreDataMember]
        public new int Value { get; set; }
    }

    private sealed class DuplicateNamesModel
    {
        [DataMember(Name = "same")]
        public int First { get; set; }
        [DataMember(Name = "same")]
        public int Second { get; set; }
    }

    private sealed class SelectedConstructorModel
    {
        public SelectedConstructorModel() { }
        [CborConstructor]
        public SelectedConstructorModel(int value)
        {
            this.Value = value;
            this.UsedSelected = true;
        }
        [DataMember(Name = "wire_value")]
        public int Value { get; }
        public string Extra { get; set; }
        [IgnoreDataMember]
        public bool UsedSelected { get; }
    }

    private sealed class PrivateSelectedConstructorModel
    {
        [CborConstructor]
        private PrivateSelectedConstructorModel(int value = 42) => this.Value = value;
        public int Value { get; }
    }

    private sealed class MultipleConstructorsModel
    {
        [CborConstructor]
        public MultipleConstructorsModel() { }
        [CborConstructor]
        public MultipleConstructorsModel(int value) => this.Value = value;
        public int Value { get; }
    }

    private sealed class UnbindableConstructorModel
    {
        public UnbindableConstructorModel() { }
        [CborConstructor]
        public UnbindableConstructorModel(int missing) { }
        public int Value { get; }
    }

    private sealed class CodecModel
    {
        public Uri Location { get; set; }
        public System.Version Version { get; set; }
        public ArraySegment<byte> Bytes { get; set; }
    }
}