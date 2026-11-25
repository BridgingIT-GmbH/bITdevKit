// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework;

using Application.Storage;

/// <summary>Runs the EF blob provider contract against SQL Server.</summary>
/// <example><code>var tests = new EntityFrameworkSqlServerBlobStoreProviderContractTests(output, fixture);</code></example>
[IntegrationTest("Infrastructure")]
[Collection(nameof(TestEnvironmentCollection))]
public sealed class EntityFrameworkSqlServerBlobStoreProviderContractTests(
    ITestOutputHelper output,
    TestEnvironmentFixture fixture)
    : EntityFrameworkBlobStoreProviderContractTestsBase(output, fixture)
{
    protected override StubDbContext CreateDbContext(bool forceNew = false) =>
        this.Fixture.EnsureSqlServerDbContext(this.Output, forceNew);

    /// <summary>Verifies ordinal container order when the database collation sorts the names differently.</summary>
    /// <example><code>await tests.ListContainersAsync_WithMixedCaseAndPunctuation_ReturnsOrdinalOrder();</code></example>
    [Fact]
    public async Task ListContainersAsync_WithMixedCaseAndPunctuation_ReturnsOrdinalOrder()
    {
        var sut = this.CreateClient();
        foreach (var container in new[] { "a", "B", "_x" })
        {
            var uploaded = await sut.UploadAsync(CreateUpload(new BlobKey(container, "report.txt"), "report"));
            uploaded.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, uploaded.Errors.Select(error => error.Message)));
        }

        var catalog = sut.ShouldBeAssignableTo<IBlobStoreContainerCatalog>();
        var result = await catalog.ListContainersAsync();

        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        result.Value.ShouldBe(["B", "_x", "a"]);
    }
}

