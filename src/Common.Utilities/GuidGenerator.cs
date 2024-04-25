// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System;
using System.Security.Cryptography;

public static class GuidGenerator
{
    public static Guid Create(string value)
    {
        if (value is null)
        {
            return Guid.Empty;
        }

        using var md5 = MD5.Create();
        return new Guid(md5.ComputeHash(Encoding.Default.GetBytes(value)));
    }

    public static Guid CreateSequential()
    {
        return MassTransit.NewId.Next().ToGuid();
    }
}