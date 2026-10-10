// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Dashboard;

using System.Globalization;
using System.Text.Encodings.Web;
using BridgingIT.DevKit.Presentation.Web.Dashboard;

/// <summary>Verifies shared correlation links preserve full identifiers and safely encode dashboard markup.</summary>
/// <example><code>dotnet test --filter FullyQualifiedName~DashboardCorrelationLinkTests</code></example>
public class DashboardCorrelationLinkTests
{
    /// <summary>Checks shortened labels retain the complete correlation identifier for logs and copying.</summary>
    /// <example><code>test.Render_ShortLabel_PreservesCompleteIdentifier();</code></example>
    [Fact]
    public void Render_ShortLabel_PreservesCompleteIdentifier()
    {
        // Arrange
        const string identifier = "bb911967768142bf9b717966977da958";
        var options = new DashboardEndpointsOptions { GroupPath = "/admin" };
        using var writer = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        DashboardCorrelationLink
            .Render(options, identifier, shorten: true)
            .WriteTo(writer, HtmlEncoder.Default);

        // Assert
        var html = writer.ToString();
        html.ShouldContain("/admin/logentries?");
        html.ShouldContain("correlationId=" + identifier);
        html.ShouldContain("data-dashboard-copy=\"" + identifier + "\"");
        html.ShouldContain("Copy correlation ID");
        html.ShouldContain("bi bi-copy");
        html.ShouldContain("bb9119677681");
    }

    /// <summary>Checks identifier characters cannot escape text or attribute contexts.</summary>
    /// <example><code>test.Render_UntrustedIdentifier_EncodesTextAndAttributes();</code></example>
    [Fact]
    public void Render_UntrustedIdentifier_EncodesTextAndAttributes()
    {
        // Arrange
        const string identifier = "\"><script>alert('id')</script>";
        using var writer = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        DashboardCorrelationLink.Render(null, identifier).WriteTo(writer, HtmlEncoder.Default);

        // Assert
        var html = writer.ToString();
        html.ShouldNotContain("<script>");
        html.ShouldContain("&lt;script&gt;");
        html.ShouldContain("data-dashboard-copy=\"&quot;&gt;");
    }

    /// <summary>Checks empty identifiers render without a link or clipboard control.</summary>
    /// <example><code>test.Render_MissingIdentifier_ShowsPlaceholder(null);</code></example>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Render_MissingIdentifier_ShowsPlaceholder(string identifier)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);

        DashboardCorrelationLink.Render(null, identifier).WriteTo(writer, HtmlEncoder.Default);

        writer.ToString().ShouldBe("<span class=\"text-muted\">—</span>");
    }

    /// <summary>Checks error views can preserve their selected time range in related logs links.</summary>
    /// <example><code>test.Render_CustomLogsUrl_PreservesFilter();</code></example>
    [Fact]
    public void Render_CustomLogsUrl_PreservesFilter()
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);

        DashboardCorrelationLink
            .Render(null, "origin", href: "/admin/logentries?correlationId=origin&from=2026-10-01")
            .WriteTo(writer, HtmlEncoder.Default);

        writer
            .ToString()
            .ShouldContain("href=\"/admin/logentries?correlationId=origin&amp;from=2026-10-01\"");
    }
}
