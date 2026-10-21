// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Dashboard;

using System.Xml.Linq;
using BridgingIT.DevKit.Presentation.Web.Dashboard;

/// <summary>Verifies feature selection, resource packaging, and decorative SVG markup.</summary>
/// <example><code>dotnet test --filter FullyQualifiedName~DashboardFeatureIconTests</code></example>
[UnitTest("Presentation")]
public sealed class DashboardFeatureIconTests
{
    /// <summary>Checks that each built-in page resolves its own embedded feature asset.</summary>
    /// <example><code>GetSvg_KnownPage_ReturnsDecorativeFeatureIcon("jobs", "jobs");</code></example>
    [Theory]
    [InlineData("dashboard.overview", "dashboard")]
    [InlineData("dashboard.system", "system")]
    [InlineData("metrics", "metrics")]
    [InlineData("profiling", "profiling")]
    [InlineData("profiling.operations", "profiling-operations")]
    [InlineData("profiling.requests", "profiling-requests")]
    [InlineData("health", "health")]
    [InlineData("identity", "identity")]
    [InlineData("logging.logs", "log-entries")]
    [InlineData("logging.errors", "errors")]
    [InlineData("logging.stream", "streams")]
    [InlineData("console", "console-commands")]
    [InlineData("mcp", "mcp")]
    [InlineData("broadcasting", "broadcasting")]
    [InlineData("jobs", "jobs")]
    [InlineData("orchestrations", "orchestrations")]
    [InlineData("messaging", "messaging")]
    [InlineData("queueing", "queueing")]
    [InlineData("storage.files", "storage-files")]
    [InlineData("storage.documents", "storage-documents")]
    [InlineData("storage.blobs", "storage-blobs")]
    [InlineData("storage.permalinks", "storage-permalinks")]
    [InlineData("change-history", "change-history")]
    public void GetSvg_KnownPage_ReturnsDecorativeFeatureIcon(string key, string featureId)
    {
        // Act
        var markup = DashboardFeatureIcons.GetSvg(key);

        // Assert
        markup.ShouldNotBeNull();
        var svg = XElement.Parse(markup.Value);
        svg.Attribute("data-dashboard-icon")?.Value.ShouldBe(featureId);
        svg.Attribute("viewBox")?.Value.ShouldBe("0 0 32 32");
        svg.Attribute("aria-hidden")?.Value.ShouldBe("true");
        svg.Attribute("focusable")?.Value.ShouldBe("false");
        svg.Attribute("aria-labelledby").ShouldBeNull();
        svg.Elements(svg.Name.Namespace + "title").ShouldBeEmpty();
    }

    /// <summary>Leaves custom and legacy items on their configured Bootstrap icon.</summary>
    /// <example><code>GetSvg_CustomOrLegacyPage_ReturnsNull("weatherfiesta.cities");</code></example>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("weatherfiesta.cities")]
    public void GetSvg_CustomOrLegacyPage_ReturnsNull(string key)
    {
        // Act
        var markup = DashboardFeatureIcons.GetSvg(key);

        // Assert
        markup.ShouldBeNull();
    }
}
