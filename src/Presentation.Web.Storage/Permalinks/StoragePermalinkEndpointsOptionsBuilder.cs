// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Presentation.Web.Storage;

using BridgingIT.DevKit.Presentation.Web;

/// <summary>
/// Builds <see cref="StoragePermalinkEndpointsOptions" /> using standard endpoint authorization settings.
/// </summary>
/// <example>
/// <code>
/// var options = new StoragePermalinkEndpointsOptionsBuilder().RequireAuthorization().Build();
/// </code>
/// </example>
public class StoragePermalinkEndpointsOptionsBuilder : EndpointsOptionsBuilderBase<StoragePermalinkEndpointsOptions, StoragePermalinkEndpointsOptionsBuilder>;
