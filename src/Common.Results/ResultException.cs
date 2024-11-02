// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

public class ResultException(string message)
    : Exception(message)
{
    public ResultException(Result result) : this(result.ToString())
    {
    }
}