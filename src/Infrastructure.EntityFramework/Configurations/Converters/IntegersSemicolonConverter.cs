// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

public class IntegersSemicolonConverter
    : ValueConverter<IEnumerable<int>, string>
{
    public IntegersSemicolonConverter()
        : base(
            v => string.Join(";", v),
            v => v.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => Convert.ToInt32(s)))
    {
    }
}

// TODO: as from C# 11 the generic math interface INumber<T> can be used to convert all number types (int/double/...) with a single converter
//public class NumbersSemicolonConverter
//    : ValueConverter<IEnumerable<INumber<T>>, string>
//{
//    public NumbersSemicolonConverter()
//        : base(
//            v => string.Join(";", v),
//            v => v.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.To<T>()))
//    {
//    }
//}