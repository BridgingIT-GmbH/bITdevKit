// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

/// <summary>
/// Registry for managing export and import profiles.
/// </summary>
public interface IProfileRegistry
{
    /// <summary>
    /// Registers an export profile.
    /// </summary>
    /// <param name="profile">The export profile to register.</param>
    void RegisterExportProfile(IExportProfile profile);

    /// <summary>
    /// Registers an import profile.
    /// </summary>
    /// <param name="profile">The import profile to register.</param>
    void RegisterImportProfile(IImportProfile profile);

    /// <summary>
    /// Gets an export profile for the specified type.
    /// </summary>
    /// <typeparam name="TSource">The source type.</typeparam>
    /// <returns>The export profile if found, null otherwise.</returns>
    IExportProfile<TSource> GetExportProfile<TSource>() where TSource : class;

    /// <summary>
    /// Gets an export profile for the specified type.
    /// </summary>
    /// <param name="sourceType">The source type.</param>
    /// <returns>The export profile if found, null otherwise.</returns>
    IExportProfile GetExportProfile(Type sourceType);

    /// <summary>
    /// Gets an import profile for the specified type.
    /// </summary>
    /// <typeparam name="TTarget">The target type.</typeparam>
    /// <returns>The import profile if found, null otherwise.</returns>
    IImportProfile<TTarget> GetImportProfile<TTarget>() where TTarget : class, new();

    /// <summary>
    /// Gets an import profile for the specified type.
    /// </summary>
    /// <param name="targetType">The target type.</param>
    /// <returns>The import profile if found, null otherwise.</returns>
    IImportProfile GetImportProfile(Type targetType);

    /// <summary>
    /// Tries to get an export profile for the specified type.
    /// </summary>
    /// <typeparam name="TSource">The source type.</typeparam>
    /// <param name="profile">The export profile if found.</param>
    /// <returns>True if a profile was found, false otherwise.</returns>
    bool TryGetExportProfile<TSource>(out IExportProfile<TSource> profile) where TSource : class;

    /// <summary>
    /// Tries to get an import profile for the specified type.
    /// </summary>
    /// <typeparam name="TTarget">The target type.</typeparam>
    /// <param name="profile">The import profile if found.</param>
    /// <returns>True if a profile was found, false otherwise.</returns>
    bool TryGetImportProfile<TTarget>(out IImportProfile<TTarget> profile) where TTarget : class, new();

    /// <summary>
    /// Gets all registered export profiles.
    /// </summary>
    /// <returns>A collection of all export profiles.</returns>
    IReadOnlyCollection<IExportProfile> GetAllExportProfiles();

    /// <summary>
    /// Gets all registered import profiles.
    /// </summary>
    /// <returns>A collection of all import profiles.</returns>
    IReadOnlyCollection<IImportProfile> GetAllImportProfiles();
}
