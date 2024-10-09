// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

/// <summary>
///     Provides extension methods for handling instances of the <see cref="Result" /> class.
/// </summary>
public static class ResultExtensions
{
    /// <summary>
    ///     Retrieves the value contained in the given <see cref="Result" /> instance.
    /// </summary>
    /// <param name="source">The source <see cref="Result" /> instance from which the value is to be retrieved.</param>
    /// <returns>
    ///     The value contained in the given <see cref="Result" /> instance if it is of type
    ///     <see cref="DomainPolicyResult{T}" /> or <see cref="Result{T}" />;
    ///     otherwise, returns the default value for the type.
    /// </returns>
    public static object GetValue(this Result source)
    {
        if (source == null)
        {
            return default;
        }

        var type = source.GetType();
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(DomainPolicyResult<>))
        {
            var property = type.GetProperty("Value");
            if (property != null)
            {
                return property.GetValue(source);
            }
        }
        else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Result<>))
        {
            var property = type.GetProperty("Value");
            if (property != null)
            {
                return property.GetValue(source);
            }
        }

        return default;
    }
}