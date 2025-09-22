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
        var stub = new CloneStub { FirstName = "John", LastName = "Doe", BirthYear = 1980, Status = ActiveStatus.Unknown }; // status 1
        stub.SetAge(25);

        var sut = stub.Clone();

        sut.ShouldNotBeNull();
        sut.FirstName.ShouldBe("John");
        sut.LastName.ShouldBe("Doe");
        sut.Age.ShouldBe(25); // private setters are not set with System.Text.Json when not using the JsonInclude attribute
        sut.Status.ShouldBe(ActiveStatus.Unknown);
        sut.BirthYear.ShouldBe(1980);
    }

    public class CloneStub
    {
        public CloneStub() { }

        public CloneStub(string firstName) => this.FirstName = firstName;

        public string FirstName { get; init; }

        public string LastName { get; set; }

        public int Age { get; private set; }

        public int BirthYear { get; set; }

        public ActiveStatus Status { get; set; }

        public void SetAge(int age)
        {
            this.Age = age;
        }
    }
}