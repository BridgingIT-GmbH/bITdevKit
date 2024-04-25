// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application;

public struct CronExpressions
// https://www.quartz-scheduler.net/documentation/quartz-3.x/tutorial/crontriggers.html#example-cron-expressions
// http://www.cronmaker.com/?1
{
    public const string Every5Seconds = "0/5 * * * * ?";

    public const string EveryMinute = "0 0/1 * * * ?";

    public const string Every5Minutes = "0 0/5 * * * ?";

    public const string Every10Minutes = "0 0/10 * * * ?";

    public const string Every15Minutes = "0 0/15 * * * ?";

    public const string Every30Minutes = "0 0/30 * * * ?";

    public const string Every60Minutes = "0 0 0/1 1/1 * ?";
}