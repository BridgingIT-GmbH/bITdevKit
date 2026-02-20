// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public static partial class Extensions
{
    public static object[] ConcatArgs(this object[] args0, object[] args1)
    {
        var a0 = args0 ?? [];
        var a1 = args1 ?? [];

        if (a0.Length == 0)
        {
            return a1;
        }

        if (a1.Length == 0)
        {
            return a0;
        }

        var result = new object[a0.Length + a1.Length];
        Array.Copy(a0, 0, result, 0, a0.Length);
        Array.Copy(a1, 0, result, a0.Length, a1.Length);

        return result;
    }
}