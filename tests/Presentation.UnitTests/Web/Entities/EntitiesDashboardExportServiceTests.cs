// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Entities;

using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using BridgingIT.DevKit.Presentation.Web.Entities.Dashboard;
using Microsoft.Extensions.DependencyInjection;

[UnitTest("Presentation")]
public class EntitiesDashboardExportServiceTests
{
    private static readonly Action<EntitiesDashboardOptions> Configure = o =>
        o.Entity<Customer>(e => e.Mask(c => c.ApiToken, "***").Hide(c => c.InternalNote));

    private static (EntitiesDashboardExportService Sut, EntitiesDashboardEntity Entity, Microsoft.AspNetCore.Http.HttpContext Http, ServiceProvider Provider)
        Create(Action<EntitiesDashboardOptions> configure = null, params Customer[] seed)
    {
        var provider = EntitiesDashboardTestData.CreateProvider(
            o =>
            {
                Configure(o);
                configure?.Invoke(o);
            },
            s => s.AddRepository(seed));
        var entity = EntitiesDashboardTestData.Catalog(provider).Entities.Single();

        return (provider.GetRequiredService<EntitiesDashboardExportService>(), entity, EntitiesDashboardTestData.CreateHttpContext(provider), provider);
    }

    private static Customer[] Seed(int count = 3) =>
        Enumerable.Range(1, count)
            .Select(i => EntitiesDashboardTestData.NewCustomer(new Guid($"00000000-0000-0000-0000-{i:D12}"), $"Customer{i}", 20 + i))
            .ToArray();

    [Fact]
    public async Task ExportAsync_Json_ReturnsAnArrayWithMaskedAndHiddenValuesProtected()
    {
        // Arrange
        var (sut, entity, http, provider) = Create(null, Seed(2));
        using var _ = provider;

        // Act
        var result = await sut.ExportAsync(http, entity, EntitiesDashboardTestData.Query(entity), "json", CancellationToken.None);

        // Assert
        result.StatusCode.ShouldBe(200);
        result.ContentType.ShouldBe("application/json; charset=utf-8");
        var text = Encoding.UTF8.GetString(result.Content);
        var array = JsonNode.Parse(text).AsArray();
        array.Count.ShouldBe(2);
        text.ShouldNotContain("secret-token");
        text.ShouldNotContain("internal");
        text.ShouldNotContain("InternalNote");
        array[0]["ApiToken"].ToString().ShouldBe("***");
        result.RowCount.ShouldBe(2);
        result.Truncated.ShouldBeFalse();
    }

    [Fact]
    public async Task ExportAsync_Json_WritesNativeBooleansAndNumbersAndParsesValueObjects()
    {
        // Arrange
        var (sut, entity, http, provider) = Create(null, Seed(1));
        using var _ = provider;

        // Act
        var result = await sut.ExportAsync(http, entity, EntitiesDashboardTestData.Query(entity), "json", CancellationToken.None);

        // Assert
        var row = JsonNode.Parse(Encoding.UTF8.GetString(result.Content)).AsArray()[0].AsObject();
        row["IsVip"].GetValueKind().ShouldBe(System.Text.Json.JsonValueKind.False);
        row["Age"].GetValueKind().ShouldBe(System.Text.Json.JsonValueKind.Number);
        row["Address"].ShouldBeOfType<JsonObject>()["city"].ToString().ShouldBe("Springfield");
    }

    [Fact]
    public async Task ExportAsync_Csv_StartsWithBomAndHasHeaderAndCrLfRows()
    {
        // Arrange
        var (sut, entity, http, provider) = Create(null, Seed(2));
        using var _ = provider;
        var query = EntitiesDashboardTestData.Query(entity, columns: ["Name", "Age"]);

        // Act
        var result = await sut.ExportAsync(http, entity, query, "csv", CancellationToken.None);

        // Assert
        result.ContentType.ShouldBe("text/csv; charset=utf-8");
        result.Content.Take(3).ShouldBe([(byte)0xEF, (byte)0xBB, (byte)0xBF]);
        var text = Encoding.UTF8.GetString(result.Content).TrimStart('\uFEFF');
        var lines = text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        lines[0].ShouldBe("Id,Name,Age");
        lines.Length.ShouldBe(3);
        lines.Skip(1).ShouldAllBe(l => l.Contains("Customer"));
    }

    [Fact]
    public async Task ExportAsync_Csv_EscapesFormulaInjectionInNames()
    {
        // Arrange
        var seed = Seed(1);
        seed[0].Name = "=HYPERLINK(\"http://x\")";
        var (sut, entity, http, provider) = Create(null, seed);
        using var _ = provider;
        var query = EntitiesDashboardTestData.Query(entity, columns: ["Name"]);

        // Act
        var result = await sut.ExportAsync(http, entity, query, "csv", CancellationToken.None);

        // Assert
        var text = Encoding.UTF8.GetString(result.Content);
        text.ShouldContain("'=HYPERLINK");
        text.ShouldNotContain(",=HYPERLINK");
    }

    [Fact]
    public async Task ExportAsync_MaxExportRows_TruncatesAndFlagsTheResult()
    {
        // Arrange
        var (sut, entity, http, provider) = Create(o => o.MaxExportRows = 2, Seed(3));
        using var _ = provider;

        // Act
        var result = await sut.ExportAsync(http, entity, EntitiesDashboardTestData.Query(entity), "json", CancellationToken.None);

        // Assert
        result.Truncated.ShouldBeTrue();
        result.RowCount.ShouldBe(2);
        JsonNode.Parse(Encoding.UTF8.GetString(result.Content)).AsArray().Count.ShouldBe(2);
    }

    [Fact]
    public async Task ExportAsync_ExactlyMaxExportRows_IsNotTruncated()
    {
        // Arrange
        var (sut, entity, http, provider) = Create(o => o.MaxExportRows = 3, Seed(3));
        using var _ = provider;

        // Act
        var result = await sut.ExportAsync(http, entity, EntitiesDashboardTestData.Query(entity), "csv", CancellationToken.None);

        // Assert
        result.Truncated.ShouldBeFalse();
        result.RowCount.ShouldBe(3);
    }

    [Fact]
    public async Task ExportAsync_WithFilter_ExportsOnlyMatchingRows()
    {
        // Arrange
        var (sut, entity, http, provider) = Create(null, Seed(3));
        using var _ = provider;
        var query = EntitiesDashboardTestData.Query(entity, filters: ["Name~eq~Customer2"]);

        // Act
        var result = await sut.ExportAsync(http, entity, query, "json", CancellationToken.None);

        // Assert
        result.RowCount.ShouldBe(1);
        JsonNode.Parse(Encoding.UTF8.GetString(result.Content)).AsArray()[0]["Name"].ToString().ShouldBe("Customer2");
    }

    [Theory]
    [InlineData("json", ".json")]
    [InlineData("csv", ".csv")]
    public async Task ExportAsync_FileName_UsesEntityNameTimestampAndExtension(string format, string extension)
    {
        // Arrange
        var (sut, entity, http, provider) = Create(null, Seed(1));
        using var _ = provider;

        // Act
        var result = await sut.ExportAsync(http, entity, EntitiesDashboardTestData.Query(entity), format, CancellationToken.None);

        // Assert
        Regex.IsMatch(result.FileName, @"^Customer-\d{14}" + Regex.Escape(extension) + "$").ShouldBeTrue(result.FileName);
    }

    [Theory]
    [InlineData("xml")]
    [InlineData("")]
    [InlineData(null)]
    public async Task ExportAsync_UnsupportedFormat_ReturnsBadRequest(string format)
    {
        // Arrange
        var (sut, entity, http, provider) = Create(null, Seed(1));
        using var _ = provider;

        // Act
        var result = await sut.ExportAsync(http, entity, EntitiesDashboardTestData.Query(entity), format, CancellationToken.None);

        // Assert
        result.StatusCode.ShouldBe(400);
        result.Error.ShouldBe("The export format must be json or csv.");
    }

    [Fact]
    public async Task ExportAsync_UnknownColumn_ReturnsBadRequest()
    {
        // Arrange
        var (sut, entity, http, provider) = Create(null, Seed(1));
        using var _ = provider;
        var query = EntitiesDashboardTestData.Query(entity, columns: ["Name", "Bogus"]);

        // Act
        var result = await sut.ExportAsync(http, entity, query, "csv", CancellationToken.None);

        // Assert
        result.StatusCode.ShouldBe(400);
    }

    [Fact]
    public async Task ExportAsync_HiddenColumnRequested_ReturnsBadRequest()
    {
        // Arrange
        var (sut, entity, http, provider) = Create(null, Seed(1));
        using var _ = provider;
        var query = EntitiesDashboardTestData.Query(entity, columns: ["InternalNote"]);

        // Act
        var result = await sut.ExportAsync(http, entity, query, "csv", CancellationToken.None);

        // Assert
        result.StatusCode.ShouldBe(400);
    }

    [Fact]
    public async Task ExportAsync_InvalidFilter_ReturnsBadRequest()
    {
        // Arrange
        var (sut, entity, http, provider) = Create(null, Seed(1));
        using var _ = provider;
        var query = EntitiesDashboardTestData.Query(entity, filters: ["Nope~eq~x"]);

        // Act
        var result = await sut.ExportAsync(http, entity, query, "json", CancellationToken.None);

        // Assert
        result.StatusCode.ShouldBe(400);
    }

    [Theory]
    [InlineData("plain", "plain")]
    [InlineData("=1+1", "'=1+1")]
    [InlineData("+1", "'+1")]
    [InlineData("-1", "'-1")]
    [InlineData("@SUM", "'@SUM")]
    [InlineData("\tcmd", "'\tcmd")]
    [InlineData("a,b", "\"a,b\"")]
    [InlineData("say \"hi\"", "\"say \"\"hi\"\"\"")]
    [InlineData("line1\nline2", "\"line1\nline2\"")]
    [InlineData("", "")]
    public void EscapeCsv_AppliesFormulaPrefixAndQuoting(string input, string expected)
    {
        // Arrange & Act
        var result = EntitiesDashboardExportService.EscapeCsv(input);

        // Assert
        result.ShouldBe(expected);
    }

    [Fact]
    public void EscapeCsv_NumericCell_KeepsTheLeadingSign()
    {
        // Arrange & Act
        var result = EntitiesDashboardExportService.EscapeCsv("-5", true);

        // Assert
        result.ShouldBe("-5");
    }

    [Fact]
    public async Task ExportAsync_Csv_NegativeNumbersAreNotPrefixed()
    {
        // Arrange
        var seed = Seed(1);
        seed[0].Age = -5;
        var (sut, entity, http, provider) = Create(null, seed);
        using var _ = provider;
        var query = EntitiesDashboardTestData.Query(entity, columns: ["Name", "Age"]);

        // Act
        var result = await sut.ExportAsync(http, entity, query, "csv", CancellationToken.None);

        // Assert
        var text = Encoding.UTF8.GetString(result.Content);
        text.ShouldContain(",Customer1,-5");
        text.ShouldNotContain("'-5");
    }
}
