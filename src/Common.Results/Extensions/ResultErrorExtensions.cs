// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

public static class ResultErrorExtensions
{
    /// <summary>
    /// Checks if the list of errors contains at least one error of the specified type.
    /// </summary>
    /// <typeparam name="TError">The type of error to check for, which must implement IResultError.</typeparam>
    /// <param name="errors">The IReadOnlyList of IResultError to check.</param>
    /// <returns>True if an error of type T exists in the list; otherwise, false.</returns>
    public static bool Has<TError>(this IReadOnlyList<IResultError> errors)
         where TError : class, IResultError
    {
        if (errors?.Any() != true)
        {
            return false;
        }

        return errors.Any(e => e is TError);
    }

    /// <summary>
    /// Gets the first error of the specified type from the list, or null if no such error exists.
    /// </summary>
    /// <typeparam name="TError">The type of error to retrieve, which must implement IResultError.</typeparam>
    /// <param name="errors">The IReadOnlyList of IResultError to search.</param>
    /// <returns>The first IResultError of type T if found; otherwise, null.</returns>
    public static TError Get<TError>(this IReadOnlyList<IResultError> errors)
        where TError : class, IResultError
    {
        if (errors?.Any() != true)
        {
            return default;
        }

        return errors.FirstOrDefault(e => e is TError) as TError;
    }
}