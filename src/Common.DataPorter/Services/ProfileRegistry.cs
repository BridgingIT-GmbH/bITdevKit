// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.DataPorter;

using System.Collections.Concurrent;

/// <summary>
/// Registry for managing export and import profiles.
/// </summary>
public sealed class ProfileRegistry : IProfileRegistry
{
    private readonly ConcurrentDictionary<Type, IExportProfile> exportProfiles = new();
    private readonly ConcurrentDictionary<Type, IImportProfile> importProfiles = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileRegistry"/> class.
    /// </summary>
    /// <param name="exportProfiles">The export profiles to register.</param>
    /// <param name="importProfiles">The import profiles to register.</param>
    public ProfileRegistry(
        IEnumerable<IExportProfile> exportProfiles = null,
        IEnumerable<IImportProfile> importProfiles = null)
    {
        if (exportProfiles is not null)
        {
            foreach (var profile in exportProfiles)
            {
                this.RegisterExportProfile(profile);
            }
        }

        if (importProfiles is not null)
        {
            foreach (var profile in importProfiles)
            {
                this.RegisterImportProfile(profile);
            }
        }
    }

    /// <inheritdoc/>
    public void RegisterExportProfile(IExportProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        this.exportProfiles[profile.SourceType] = profile;
    }

    /// <inheritdoc/>
    public void RegisterImportProfile(IImportProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        this.importProfiles[profile.TargetType] = profile;
    }

    /// <inheritdoc/>
    public IExportProfile<TSource> GetExportProfile<TSource>()
        where TSource : class
    {
        return this.exportProfiles.TryGetValue(typeof(TSource), out var profile)
            ? profile as IExportProfile<TSource>
            : null;
    }

    /// <inheritdoc/>
    public IExportProfile GetExportProfile(Type sourceType)
    {
        ArgumentNullException.ThrowIfNull(sourceType);
        return this.exportProfiles.TryGetValue(sourceType, out var profile) ? profile : null;
    }

    /// <inheritdoc/>
    public IImportProfile<TTarget> GetImportProfile<TTarget>()
        where TTarget : class, new()
    {
        return this.importProfiles.TryGetValue(typeof(TTarget), out var profile)
            ? profile as IImportProfile<TTarget>
            : null;
    }

    /// <inheritdoc/>
    public IImportProfile GetImportProfile(Type targetType)
    {
        ArgumentNullException.ThrowIfNull(targetType);
        return this.importProfiles.TryGetValue(targetType, out var profile) ? profile : null;
    }

    /// <inheritdoc/>
    public bool TryGetExportProfile<TSource>(out IExportProfile<TSource> profile)
        where TSource : class
    {
        profile = this.GetExportProfile<TSource>();
        return profile is not null;
    }

    /// <inheritdoc/>
    public bool TryGetImportProfile<TTarget>(out IImportProfile<TTarget> profile)
        where TTarget : class, new()
    {
        profile = this.GetImportProfile<TTarget>();
        return profile is not null;
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<IExportProfile> GetAllExportProfiles()
    {
        return this.exportProfiles.Values.ToList();
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<IImportProfile> GetAllImportProfiles()
    {
        return this.importProfiles.Values.ToList();
    }
}
