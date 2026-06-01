// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using System.Security.Cryptography;
using System.Text;
using BridgingIT.DevKit.Common;

internal static class JobOccurrenceKeyFactory
{
    public static string Create(
        string jobName,
        string triggerName,
        JobTriggerType triggerType,
        DateTimeOffset dueUtc,
        DateTimeOffset? scheduledUtc,
        string identity = null)
    {
        var payload = $"{jobName}|{triggerName}|{triggerType}|{dueUtc.UtcDateTime:O}|{scheduledUtc?.UtcDateTime:O}|{identity}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }
}