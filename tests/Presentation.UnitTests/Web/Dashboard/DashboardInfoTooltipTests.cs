// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Dashboard;

using System.Text.Encodings.Web;
using BridgingIT.DevKit.Presentation.Web.Dashboard.Pages;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using RazorSlices;

/// <summary>Checks shared help controls encode descriptions and expose keyboard-accessible text.</summary>
public sealed class DashboardInfoTooltipTests
{
    [Theory]
    [InlineData("Owned memory in bytes")]
    [InlineData("A \"quoted\" <script>alert('x')</script> & value")]
    public async Task Render_Description_EncodesTooltipAndAccessibleName(string description)
    {
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var body = new MemoryStream();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Response.Body = body;

        var sut = Results.RazorSlice<InfoTooltip, string>(description);
        await sut.ExecuteAsync(context);

        body.Position = 0;
        using var reader = new StreamReader(body);
        var html = await reader.ReadToEndAsync();
        var encoded = HtmlEncoder.Default.Encode(description);
        html.ShouldContain("type=\"button\"");
        html.ShouldContain("data-dashboard-help");
        html.ShouldContain("data-bs-html=\"false\"");
        html.ShouldContain("data-bs-trigger=\"hover focus\"");
        html.ShouldContain("title=\"" + encoded + "\"");
        html.ShouldContain("aria-label=\"" + encoded + "\"");
        html.ShouldContain("aria-hidden=\"true\"");
        html.ShouldNotContain("<script>");
    }
}