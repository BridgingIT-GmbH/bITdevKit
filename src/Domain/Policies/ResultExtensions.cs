// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

/// <summary>
///     Provides value access for results returned by domain policies.
/// </summary>
public static class ResultExtensions
{
    /// <summary>
    ///     Retrieves a scalar or paged result value through its result interface.
    /// </summary>
    /// <param name="source">The policy result to read.</param>
    /// <returns>
    ///     The value exposed by the result, or <see langword="null"/> if the result has no value interface.
    /// </returns>
    public static object GetValue(this IResult source)
    {
        var resultInterface = source?.GetType().GetInterfaces().FirstOrDefault(type =>
            type.IsGenericType &&
            (type.GetGenericTypeDefinition() == typeof(IResult<>) ||
             type.GetGenericTypeDefinition() == typeof(IResultPaged<>)));

        return resultInterface?.GetProperty(nameof(IResult<object>.Value))?.GetValue(source);
    }
}
