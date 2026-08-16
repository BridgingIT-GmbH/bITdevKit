// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

/// <summary>
/// Represents strings semicolon converter.
/// </summary>
public class StringsSemicolonConverter : ValueConverter<IEnumerable<string>, string>
{
    /// <summary>
    /// Initializes a new instance of the <c>StringsSemicolonConverter</c> class.
    /// </summary>
    public StringsSemicolonConverter()
        : base(v => string.Join(";", v), v => v.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)) { }
}
