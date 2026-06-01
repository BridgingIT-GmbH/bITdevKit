// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using BridgingIT.DevKit.Common;

internal static class JobDataContractResolver
{
    public static Type Resolve(Type jobType)
    {
        ArgumentNullException.ThrowIfNull(jobType);

        var jobInterface = jobType.GetInterfaces()
            .FirstOrDefault(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IJob<>));

        return jobInterface?.GetGenericArguments()[0] ?? typeof(Unit);
    }
}
