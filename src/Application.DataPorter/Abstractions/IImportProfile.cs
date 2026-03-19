// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.DataPorter;

/// <summary>
/// Marker interface for import profiles.
/// </summary>
public interface IImportProfile
{
    /// <summary>
    /// Gets the target type this profile is configured for.
    /// </summary>
    Type TargetType { get; }

    /// <summary>
    /// Gets the column configurations for this profile.
    /// </summary>
    IReadOnlyList<ImportColumnConfiguration> Columns { get; }

    /// <summary>
    /// Gets the sheet/section name to import from.
    /// </summary>
    string SheetName { get; }

    /// <summary>
    /// Gets the sheet index to import from (0-based). -1 means use SheetName.
    /// </summary>
    int SheetIndex { get; }

    /// <summary>
    /// Gets the row index containing headers (0-based).
    /// </summary>
    int HeaderRowIndex { get; }

    /// <summary>
    /// Gets the number of rows to skip after the header.
    /// </summary>
    int SkipRows { get; }

    /// <summary>
    /// Gets the behavior when validation fails.
    /// </summary>
    ImportValidationBehavior ValidationBehavior { get; }

    /// <summary>
    /// Gets the factory function for creating target instances.
    /// </summary>
    Func<object> Factory { get; }
}

/// <summary>
/// Generic interface for typed import profiles.
/// </summary>
/// <typeparam name="TTarget">The target type to import into.</typeparam>
public interface IImportProfile<TTarget> : IImportProfile
    where TTarget : class, new()
{
    /// <summary>
    /// Gets the typed factory function for creating target instances.
    /// </summary>
    new Func<TTarget> Factory { get; }
}

/// <summary>
/// Specifies the behavior when import validation fails.
/// </summary>
public enum ImportValidationBehavior
{
    /// <summary>
    /// Skip the invalid row and continue importing.
    /// </summary>
    SkipRow,

    /// <summary>
    /// Stop the import process on first validation error.
    /// </summary>
    StopImport,

    /// <summary>
    /// Collect all errors and continue importing valid rows.
    /// </summary>
    CollectErrors
}
