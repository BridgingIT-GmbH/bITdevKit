// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.UnitTests;

using BridgingIT.DevKit.Domain.Model;

public class PersonStub : Entity<Guid>
{
    public string FirstName { get; set; }

    public string LastName { get; set; }

    public int Age { get; set; }
}

public class PersonDtoStub : AggregateRoot<string>
{
    public Guid Identifier { get; set; }

    public string Nationality { get; set; }

    public string FullName { get; set; }

    public int Age { get; set; }
}