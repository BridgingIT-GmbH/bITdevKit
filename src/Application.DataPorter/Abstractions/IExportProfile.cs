// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

/// <summary>
/// Marker interface for export profiles.
/// </summary>
public interface IExportProfile
{
    /// <summary>
    /// Gets the source type this profile is configured for.
    /// </summary>
    Type SourceType { get; }

    /// <summary>
    /// Gets the column configurations for this profile.
    /// </summary>
    IReadOnlyList<ColumnConfiguration> Columns { get; }

    /// <summary>
    /// Gets the sheet/section name for this export.
    /// </summary>
    string SheetName { get; }

    /// <summary>
    /// Gets the header rows configuration.
    /// </summary>
    IReadOnlyList<HeaderRowConfiguration> HeaderRows { get; }

    /// <summary>
    /// Gets the footer rows configuration.
    /// </summary>
    IReadOnlyList<FooterRowConfiguration> FooterRows { get; }
}

/// <summary>
/// Generic interface for typed export profiles.
/// </summary>
/// <typeparam name="TSource">The source type to export.</typeparam>
public interface IExportProfile<TSource> : IExportProfile
    where TSource : class
{
}
