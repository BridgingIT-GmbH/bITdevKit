// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities;

[UnitTest("Common")]
public class CloneTests
{
    [Fact]
    public void CanCloneInstance()
    {
        var stub = new CloneStub { FirstName = "John", LastName = "Doe", BirthYear = 1980 };
        stub.SetAge(25);

        stub.Clone()
            .ShouldNotBeNull();
        stub.Clone()
            .FirstName.ShouldBe("John");
        stub.Clone()
            .LastName.ShouldBe("Doe");
        stub.Clone()
            .Age.ShouldBe(25); // private setters are not set with System.Text.Json when not using the JsonInclude attribute
        stub.Clone()
            .BirthYear.ShouldBe(1980);
    }

    public class CloneStub
    {
        public CloneStub() { }

        public CloneStub(string firstName) => this.FirstName = firstName;

        public string FirstName { get; init; }

        public string LastName { get; set; }

        public int Age { get; private set; }

        public int BirthYear { get; set; }

        public void SetAge(int age)
        {
            this.Age = age;
        }
    }
}