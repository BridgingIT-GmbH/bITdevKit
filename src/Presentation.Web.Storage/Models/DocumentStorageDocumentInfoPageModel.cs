// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Presentation.Web.Storage.Models;

/// <summary>Represents one filtered page of document metadata.</summary>
public sealed class DocumentStorageDocumentInfoPageModel
{
    /// <summary>Gets the metadata returned on this page.</summary>
    public DocumentStorageDocumentInfoModel[] Items { get; init; } = [];

    /// <summary>Gets the token for the next page, or null when no additional match was found.</summary>
    public string ContinuationToken { get; init; }

    /// <summary>Gets whether another page is available.</summary>
    public bool HasMore => this.ContinuationToken is not null;
}
