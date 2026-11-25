// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Filtering;

using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

public class FilterModelBindingTests
{
    [Fact]
    public async Task Search_WithPlaceholderEntries_BindsFilterModel()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.ConfigureJson();
        await using var app = builder.Build();

        FilterModel captured = null;
        app.MapPost("/search", ([FromBody] FilterModel filter) => { captured = filter; });
        await app.StartAsync();
        using var sut = app.GetTestClient();
        using var body = new StringContent(
            """
            {
              "page": 1,
              "pageSize": 1,
              "noTracking": true,
              "orderings": [{ "field": null, "direction": null }],
              "filters": null,
              "includes": [""],
              "hierarchy": null,
              "hierarchyMaxDepth": 1,
              "_FilterCriteriaSchema": null,
              "_CompositeSpecificationSchema": { "nodes": null },
              "_SpecificationNodeSchema": null
            }
            """, Encoding.UTF8, "application/json");

        // Act
        using var response = await sut.PostAsync("/search", body);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        captured.ShouldNotBeNull();
        captured.Page.ShouldBe(1);
        captured.PageSize.ShouldBe(1);
        captured.HierarchyMaxDepth.ShouldBe(1);
        captured.Orderings.ShouldBeEmpty();
        captured.Includes.ShouldBeEmpty();
    }
}
