// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.Options;

using Microsoft.Extensions.Logging;

public class LoggerOptions : OptionsBase
{
    public LoggerOptions()
    {
    }

    public LoggerOptions(ILoggerFactory loggerFactory)
        : base(loggerFactory)
    {
    }
}