// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System;

public class ChaosException : Exception
{
    public ChaosException()
        : base()
    {
    }

    public ChaosException(string message)
        : base(message)
    {
    }

    public ChaosException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
