// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Security.Cryptography;
using MassTransit;

/// <summary>
///     Provides methods to generate GUIDs based on specific values or using sequential creation logic.
/// </summary>
public static class GuidGenerator
{
    /// <summary>
    ///     Generates a GUID based on the provided string value.
    /// </summary>
    /// <param name="value">The input string used to generate the GUID. If the value is null, an empty GUID is returned.</param>
    /// <returns>A GUID generated from the input string. If the value is null, returns Guid.Empty.</returns>
    public static Guid Create(string value)
    {
        return value is null ? Guid.Empty : new Guid(MD5.HashData(Encoding.Default.GetBytes(value)));
    }

    /// <summary>
    ///     Creates a new GUID in a sequential order.
    /// </summary>
    /// <returns>
    ///     A <see cref="Guid" /> representing the sequentially generated GUID.
    /// </returns>
    public static Guid CreateSequential()
    {
        //TODO: use new dotnet 9 Guid version 7 which are sequential
        return NewId.Next().ToGuid();
    }
}