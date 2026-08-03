// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Presentation.Web.Storage;

using BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Builds stable public Storage Permalink routes.
/// </summary>
/// <example>
/// <code>
/// var path = StoragePermalinkRoutes.Download(entry.Id);
/// </code>
/// </example>
public static class StoragePermalinkRoutes
{
    /// <summary>
    /// Gets the default permalink endpoint group path.
    /// </summary>
    public const string GroupPath = "/_bdk/api/storage/permalinks";

    /// <summary>
    /// Builds the direct download path for a permalink identifier.
    /// </summary>
    public static string Download(StoragePermalinkId id) => $"{GroupPath}/{id.Value}";
}
