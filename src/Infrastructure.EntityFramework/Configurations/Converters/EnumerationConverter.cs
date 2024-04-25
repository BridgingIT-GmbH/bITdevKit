// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using BridgingIT.DevKit.Domain.Model;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

public class EnumerationConverter<TEnumeration>
    : ValueConverter<TEnumeration, int>
    where TEnumeration : Enumeration
{
    public EnumerationConverter()
        : base(
            v => v.Id,
            v => Enumeration.From<TEnumeration>(v))
    {
    }
}