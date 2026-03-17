// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

/// <summary>
/// Provides helper methods for applying <see cref="ImportConfiguration.MaxErrors"/> consistently.
/// </summary>
public static class ImportErrorLimit
{
    /// <summary>
    /// Determines whether the configured import error limit has been reached.
    /// </summary>
    /// <param name="configuration">The import configuration.</param>
    /// <param name="errorCount">The number of collected errors.</param>
    /// <returns><c>true</c> if the error limit is enabled and has been reached; otherwise <c>false</c>.</returns>
    public static bool IsReached(ImportConfiguration configuration, int errorCount)
    {
        return configuration?.MaxErrors is > 0 && errorCount >= configuration.MaxErrors.Value;
    }

    /// <summary>
    /// Adds an error while respecting the configured maximum number of collected errors.
    /// </summary>
    /// <param name="errors">The collected errors.</param>
    /// <param name="error">The error to add.</param>
    /// <param name="configuration">The import configuration.</param>
    /// <returns><c>true</c> if the limit has been reached after the add attempt; otherwise <c>false</c>.</returns>
    public static bool TryAdd(
        ICollection<ImportRowError> errors,
        ImportRowError error,
        ImportConfiguration configuration)
    {
        if (IsReached(configuration, errors.Count))
        {
            return true;
        }

        errors.Add(error);
        return IsReached(configuration, errors.Count);
    }

    /// <summary>
    /// Adds multiple errors while respecting the configured maximum number of collected errors.
    /// </summary>
    /// <param name="errors">The collected errors.</param>
    /// <param name="newErrors">The errors to add.</param>
    /// <param name="configuration">The import configuration.</param>
    /// <returns><c>true</c> if the limit has been reached after the add attempt; otherwise <c>false</c>.</returns>
    public static bool TryAddRange(
        ICollection<ImportRowError> errors,
        IEnumerable<ImportRowError> newErrors,
        ImportConfiguration configuration)
    {
        foreach (var error in newErrors)
        {
            if (TryAdd(errors, error, configuration))
            {
                return true;
            }
        }

        return IsReached(configuration, errors.Count);
    }
}
