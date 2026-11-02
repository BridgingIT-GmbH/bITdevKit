// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Entities;

using System.Text.Json;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Presentation.Web.Entities.Dashboard;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

[UnitTest("Presentation")]
public class EntitiesDashboardWriteServiceTests
{
    private static readonly Guid Id1 = EntitiesDashboardTestData.CustomerGuid1;

    private static readonly Guid Id2 = EntitiesDashboardTestData.CustomerGuid2;

    private static Fixture CreateFixture(Action<EntitiesDashboardOptions> configure = null, params Customer[] seed)
    {
        var provider = EntitiesDashboardTestData.CreateProvider(
            o =>
            {
                o.Entity<Customer>(e => e.Mask(c => c.ApiToken, "***").Hide(c => c.InternalNote).Mask(c => c.Address.TaxNumber, "<tax>"));
                configure?.Invoke(o);
            },
            s => s.AddRepository(seed).AddRepository<LockedProduct>(new LockedProduct("SKU-1", "Widget")).AddRepository<ArchivedNote>());

        return new Fixture(provider);
    }

    private static Customer[] TwoCustomers() =>
        [EntitiesDashboardTestData.NewCustomer(Id1, "Ann", 41), EntitiesDashboardTestData.NewCustomer(Id2, "Bob", 29)];

    private static JsonElement Payload(EntitiesDashboardOutcome outcome) =>
        JsonSerializer.SerializeToElement(outcome.Payload, EntitiesDashboardJson.Options);

    [Fact]
    public async Task CreateAsync_ValidValues_Returns201AndPersistsTheRecord()
    {
        // Arrange
        using var fixture = CreateFixture(null, TwoCustomers());
        var body = EntitiesDashboardTestData.Json(new { entity = fixture.Key, values = new { Name = "Zed", Age = "5", IsVip = "true", Status = "Suspended" } });

        // Act
        var outcome = await fixture.Sut.CreateAsync(fixture.Http, body, CancellationToken.None);

        // Assert
        outcome.Success.ShouldBeTrue(outcome.Message);
        outcome.StatusCode.ShouldBe(201);
        var id = Guid.Parse(Payload(outcome).GetProperty("id").GetString());
        id.ShouldNotBe(Guid.Empty);
        var stored = await fixture.Repository.FindOneAsync(CustomerId.Create(id));
        stored.Name.ShouldBe("Zed");
        stored.Age.ShouldBe(5);
        stored.IsVip.ShouldBeTrue();
        stored.Status.ShouldBe(CustomerStatus.Suspended);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Materializer_Create_BlankId_IsGeneratedOnlyWhenRequested(bool generateId)
    {
        // Arrange
        using var fixture = CreateFixture();
        var entity = EntitiesDashboardTestData.Catalog(fixture.Provider).Find(fixture.Key);
        var body = EntitiesDashboardTestData.Json(new { entity = fixture.Key, values = new { Name = "Zed", Age = "5", IsVip = "false", Status = "Suspended" } });

        // Act
        var result = EntitiesDashboardEntityMaterializer.Create(entity, EntitiesDashboardWriteRequest.From(body), generateId);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var id = ((Customer)result.Value).Id;
        (id != null && id.Value != Guid.Empty).ShouldBe(generateId);
    }

    [Fact]
    public async Task CreateAsync_InvalidValue_Returns422WithFieldError()
    {
        // Arrange
        using var fixture = CreateFixture(null, TwoCustomers());
        var body = EntitiesDashboardTestData.Json(new { entity = fixture.Key, values = new { Name = "Zed", Age = "not-a-number" } });

        // Act
        var outcome = await fixture.Sut.CreateAsync(fixture.Http, body, CancellationToken.None);

        // Assert
        outcome.Success.ShouldBeFalse();
        outcome.StatusCode.ShouldBe(422);
        outcome.FieldErrors.ShouldContain(e => e.Field == "Age");
        (await fixture.Repository.CountAsync()).ShouldBe(2);
    }

    [Fact]
    public async Task CreateAsync_EntityWithoutParameterlessConstructor_Returns400WithCapabilityNote()
    {
        // Arrange
        using var fixture = CreateFixture();
        var key = typeof(LockedProduct).FullName;
        var body = EntitiesDashboardTestData.Json(new { entity = key, values = new { Name = "x" } });

        // Act
        var outcome = await fixture.Sut.CreateAsync(fixture.Http, body, CancellationToken.None);

        // Assert
        outcome.StatusCode.ShouldBe(400);
        outcome.Message.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CreateAsync_UnknownEntity_Returns404()
    {
        // Arrange
        using var fixture = CreateFixture();
        var body = EntitiesDashboardTestData.Json(new { entity = "Nope", values = new { } });

        // Act
        var outcome = await fixture.Sut.CreateAsync(fixture.Http, body, CancellationToken.None);

        // Assert
        outcome.StatusCode.ShouldBe(404);
    }

    [Fact]
    public async Task CreateAsync_BodyIsNotAnObject_Returns400()
    {
        // Arrange
        using var fixture = CreateFixture();
        var body = EntitiesDashboardTestData.Json(new[] { 1, 2 });

        // Act
        var outcome = await fixture.Sut.CreateAsync(fixture.Http, body, CancellationToken.None);

        // Assert
        outcome.StatusCode.ShouldBe(400);
        outcome.Message.ShouldBe("The request body is not valid.");
    }

    [Fact]
    public async Task UpdateAsync_ChangedValue_SavesAndReportsTheDiff()
    {
        // Arrange
        using var fixture = CreateFixture(null, TwoCustomers());
        var body = await fixture.UpdateBodyAsync(Id1, new { Name = "Anna" });

        // Act
        var outcome = await fixture.Sut.UpdateAsync(fixture.Http, body, false, CancellationToken.None);

        // Assert
        outcome.Success.ShouldBeTrue(outcome.Message);
        outcome.Message.ShouldBe("The record was saved.");
        Payload(outcome).GetProperty("changed").GetBoolean().ShouldBeTrue();
        Payload(outcome).GetProperty("diff").GetArrayLength().ShouldBe(1);
        (await fixture.FindAsync(Id1)).Name.ShouldBe("Anna");
    }

    [Fact]
    public async Task UpdateAsync_NoChanges_ReturnsOkWithoutWriting()
    {
        // Arrange
        using var fixture = CreateFixture(null, TwoCustomers());
        var before = await fixture.FindAsync(Id1);
        var body = await fixture.UpdateBodyAsync(Id1, new { Name = "Ann" });

        // Act
        var outcome = await fixture.Sut.UpdateAsync(fixture.Http, body, false, CancellationToken.None);

        // Assert
        outcome.Success.ShouldBeTrue();
        outcome.Message.ShouldBe("There are no changes to save.");
        Payload(outcome).GetProperty("changed").GetBoolean().ShouldBeFalse();
        (await fixture.FindAsync(Id1)).ConcurrencyVersion.ShouldBe(before.ConcurrencyVersion);
    }

    [Fact]
    public async Task UpdateAsync_Preview_DoesNotWrite()
    {
        // Arrange
        using var fixture = CreateFixture(null, TwoCustomers());
        var body = await fixture.UpdateBodyAsync(Id1, new { Name = "Previewed" });

        // Act
        var outcome = await fixture.Sut.UpdateAsync(fixture.Http, body, true, CancellationToken.None);

        // Assert
        outcome.Success.ShouldBeTrue();
        outcome.Message.ShouldBe("Review the changes before saving.");
        Payload(outcome).GetProperty("changed").GetBoolean().ShouldBeTrue();
        (await fixture.FindAsync(Id1)).Name.ShouldBe("Ann");
    }

    [Fact]
    public async Task UpdateAsync_StaleConcurrencyVersion_Returns409()
    {
        // Arrange
        using var fixture = CreateFixture(null, TwoCustomers());
        var body = EntitiesDashboardTestData.Json(new
        {
            entity = fixture.Key,
            id = Id1.ToString(),
            concurrencyVersion = Guid.NewGuid().ToString(),
            values = new { Name = "Late" }
        });

        // Act
        var outcome = await fixture.Sut.UpdateAsync(fixture.Http, body, false, CancellationToken.None);

        // Assert
        outcome.StatusCode.ShouldBe(409);
        (await fixture.FindAsync(Id1)).Name.ShouldBe("Ann");
    }

    [Fact]
    public async Task UpdateAsync_MissingConcurrencyVersion_Returns400()
    {
        // Arrange
        using var fixture = CreateFixture(null, TwoCustomers());
        var body = EntitiesDashboardTestData.Json(new { entity = fixture.Key, id = Id1.ToString(), values = new { Name = "x" } });

        // Act
        var outcome = await fixture.Sut.UpdateAsync(fixture.Http, body, false, CancellationToken.None);

        // Assert
        outcome.StatusCode.ShouldBe(400);
        outcome.Message.ShouldBe("The concurrency version of the loaded record is required.");
    }

    [Fact]
    public async Task UpdateAsync_UnknownRecord_Returns404()
    {
        // Arrange
        using var fixture = CreateFixture(null, TwoCustomers());
        var body = EntitiesDashboardTestData.Json(new
        {
            entity = fixture.Key,
            id = Guid.NewGuid().ToString(),
            concurrencyVersion = Guid.NewGuid().ToString(),
            values = new { Name = "x" }
        });

        // Act
        var outcome = await fixture.Sut.UpdateAsync(fixture.Http, body, false, CancellationToken.None);

        // Assert
        outcome.StatusCode.ShouldBe(404);
    }

    [Fact]
    public async Task UpdateAsync_InvalidId_Returns400()
    {
        // Arrange
        using var fixture = CreateFixture(null, TwoCustomers());
        var body = EntitiesDashboardTestData.Json(new { entity = fixture.Key, id = "not-a-guid", values = new { Name = "x" } });

        // Act
        var outcome = await fixture.Sut.UpdateAsync(fixture.Http, body, false, CancellationToken.None);

        // Assert
        outcome.StatusCode.ShouldBe(400);
        outcome.Message.ShouldBe("The id is not valid.");
    }

    [Fact]
    public async Task UpdateAsync_InvalidValue_Returns422WithFieldError()
    {
        // Arrange
        using var fixture = CreateFixture(null, TwoCustomers());
        var body = await fixture.UpdateBodyAsync(Id1, new { Age = "abc" });

        // Act
        var outcome = await fixture.Sut.UpdateAsync(fixture.Http, body, false, CancellationToken.None);

        // Assert
        outcome.StatusCode.ShouldBe(422);
        outcome.FieldErrors.ShouldContain(e => e.Field == "Age");
        (await fixture.FindAsync(Id1)).Age.ShouldBe(41);
    }

    [Fact]
    public async Task UpdateAsync_SoftDeletedRecord_Returns409()
    {
        // Arrange
        var note = new ArchivedNote { Id = Guid.NewGuid(), Title = "Old" };
        note.SetDeleted();
        var provider = EntitiesDashboardTestData.CreateProvider(services: s => s.AddRepository(note));
        using var _ = provider;
        var http = EntitiesDashboardTestData.CreateHttpContext(provider);
        var body = EntitiesDashboardTestData.Json(new { entity = typeof(ArchivedNote).FullName, id = note.Id.ToString(), values = new { Title = "New" } });

        // Act
        var outcome = await provider.GetRequiredService<EntitiesDashboardWriteService>().UpdateAsync(http, body, false, CancellationToken.None);

        // Assert
        outcome.StatusCode.ShouldBe(409);
        outcome.Message.ShouldBe("The record is deleted and cannot be changed.");
    }

    [Fact]
    public async Task UpdateAsync_MaskedValueWithoutReplaceFlag_KeepsTheStoredValue()
    {
        // Arrange
        using var fixture = CreateFixture(null, TwoCustomers());
        var body = await fixture.UpdateBodyAsync(Id1, new { ApiToken = "attempted-overwrite", Name = "Anna" });

        // Act
        var outcome = await fixture.Sut.UpdateAsync(fixture.Http, body, false, CancellationToken.None);

        // Assert
        outcome.Success.ShouldBeTrue(outcome.Message);
        var stored = await fixture.FindAsync(Id1);
        stored.ApiToken.ShouldBe("secret-token");
        stored.Name.ShouldBe("Anna");
    }

    [Fact]
    public async Task UpdateAsync_MaskedValueWithReplaceFlag_ReplacesTheStoredValueAndNeverEchoesIt()
    {
        // Arrange
        using var fixture = CreateFixture(null, TwoCustomers());
        var body = await fixture.UpdateBodyAsync(Id1, new { ApiToken = "rotated-token" }, replace: ["ApiToken"]);

        // Act
        var outcome = await fixture.Sut.UpdateAsync(fixture.Http, body, false, CancellationToken.None);

        // Assert
        outcome.Success.ShouldBeTrue(outcome.Message);
        (await fixture.FindAsync(Id1)).ApiToken.ShouldBe("rotated-token");
        JsonSerializer.Serialize(outcome).ShouldNotContain("rotated-token");
        JsonSerializer.Serialize(outcome).ShouldNotContain("secret-token");
    }

    [Fact]
    public async Task UpdateAsync_ValueObjectWithMaskedMember_KeepsTheStoredMaskedMember()
    {
        // Arrange
        using var fixture = CreateFixture(null, TwoCustomers());
        var address = """{"city":"Shelbyville","taxNumber":"<tax>"}""";
        var body = await fixture.UpdateBodyAsync(Id1, new { Address = address });

        // Act
        var outcome = await fixture.Sut.UpdateAsync(fixture.Http, body, false, CancellationToken.None);

        // Assert
        outcome.Success.ShouldBeTrue(outcome.Message);
        var stored = await fixture.FindAsync(Id1);
        stored.Address.City.ShouldBe("Shelbyville");
        stored.Address.TaxNumber.ShouldBe("TAX-1");
    }

    [Fact]
    public async Task UpdateAsync_EntityWithoutWritableProperties_Returns400()
    {
        // Arrange
        using var fixture = CreateFixture();
        var body = EntitiesDashboardTestData.Json(new { entity = typeof(LockedProduct).FullName, id = Guid.NewGuid().ToString(), values = new { Name = "x" } });

        // Act
        var outcome = await fixture.Sut.UpdateAsync(fixture.Http, body, false, CancellationToken.None);

        // Assert
        outcome.StatusCode.ShouldBe(400);
    }

    [Fact]
    public async Task DeleteAsync_ExistingRecord_RemovesIt()
    {
        // Arrange
        using var fixture = CreateFixture(null, TwoCustomers());
        var body = EntitiesDashboardTestData.Json(new { entity = fixture.Key, id = Id1.ToString() });

        // Act
        var outcome = await fixture.Sut.DeleteAsync(fixture.Http, body, CancellationToken.None);

        // Assert
        outcome.Success.ShouldBeTrue(outcome.Message);
        outcome.Message.ShouldBe("The record was deleted.");
        (await fixture.FindAsync(Id1)).ShouldBeNull();
        (await fixture.FindAsync(Id2)).ShouldNotBeNull();
    }

    [Fact]
    public async Task DeleteAsync_RepositoryReportsNotFound_Returns404()
    {
        // Arrange
        var repository = Substitute.For<IGenericRepository<Customer>>();
        repository.DeleteAsync(Arg.Any<object>(), Arg.Any<CancellationToken>()).Returns(RepositoryActionResult.NotFound);
        using var provider = EntitiesDashboardTestData.CreateProvider(services: s => s.AddSingleton(repository));
        var sut = provider.GetRequiredService<EntitiesDashboardWriteService>();
        var body = EntitiesDashboardTestData.Json(new { entity = typeof(Customer).FullName, id = Guid.NewGuid().ToString() });

        // Act
        var outcome = await sut.DeleteAsync(EntitiesDashboardTestData.CreateHttpContext(provider), body, CancellationToken.None);

        // Assert
        outcome.StatusCode.ShouldBe(404);
        outcome.Message.ShouldBe("The record was not found.");
    }

    [Fact]
    public async Task DeleteAsync_InvalidId_Returns400()
    {
        // Arrange
        using var fixture = CreateFixture(null, TwoCustomers());
        var body = EntitiesDashboardTestData.Json(new { entity = fixture.Key, id = "x" });

        // Act
        var outcome = await fixture.Sut.DeleteAsync(fixture.Http, body, CancellationToken.None);

        // Assert
        outcome.StatusCode.ShouldBe(400);
    }

    [Fact]
    public async Task BulkDeleteAsync_AllExisting_DeletesEveryRecordAndIgnoresDuplicates()
    {
        // Arrange
        using var fixture = CreateFixture(null, TwoCustomers());
        var body = EntitiesDashboardTestData.Json(new { entity = fixture.Key, ids = new[] { Id1.ToString(), Id2.ToString(), Id1.ToString() } });

        // Act
        var outcome = await fixture.Sut.BulkDeleteAsync(fixture.Http, body, CancellationToken.None);

        // Assert
        outcome.Success.ShouldBeTrue(outcome.Message);
        outcome.Message.ShouldBe("Deleted 2 record(s).");
        Payload(outcome).GetProperty("deleted").GetArrayLength().ShouldBe(2);
        Payload(outcome).GetProperty("failed").GetArrayLength().ShouldBe(0);
        (await fixture.Repository.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task BulkDeleteAsync_PartialFailure_ReportsFailedIdsWithMessages()
    {
        // Arrange
        using var fixture = CreateFixture(null, TwoCustomers());
        var missing = Guid.NewGuid().ToString();
        var body = EntitiesDashboardTestData.Json(new { entity = fixture.Key, ids = new[] { Id1.ToString(), missing, "bad-id" } });

        // Act
        var outcome = await fixture.Sut.BulkDeleteAsync(fixture.Http, body, CancellationToken.None);

        // Assert
        outcome.Message.ShouldBe("Deleted 1 of 3 record(s); 2 failed.");
        var failed = Payload(outcome).GetProperty("failed").EnumerateArray().Select(f => f.GetProperty("id").GetString()).ToList();
        failed.ShouldBe([missing, "bad-id"], ignoreOrder: true);
        (await fixture.FindAsync(Id2)).ShouldNotBeNull();
        (await fixture.FindAsync(Id1)).ShouldBeNull();
    }

    [Fact]
    public async Task BulkDeleteAsync_NoIds_Returns400()
    {
        // Arrange
        using var fixture = CreateFixture(null, TwoCustomers());
        var body = EntitiesDashboardTestData.Json(new { entity = fixture.Key, ids = Array.Empty<string>() });

        // Act
        var outcome = await fixture.Sut.BulkDeleteAsync(fixture.Http, body, CancellationToken.None);

        // Assert
        outcome.StatusCode.ShouldBe(400);
        outcome.Message.ShouldBe("Select at least one record.");
    }

    [Fact]
    public async Task BulkDeleteAsync_MoreThanTheCap_Returns400AndDeletesNothing()
    {
        // Arrange
        using var fixture = CreateFixture(o => o.MaxBulkSelection = 1, TwoCustomers());
        var body = EntitiesDashboardTestData.Json(new { entity = fixture.Key, ids = new[] { Id1.ToString(), Id2.ToString() } });

        // Act
        var outcome = await fixture.Sut.BulkDeleteAsync(fixture.Http, body, CancellationToken.None);

        // Assert
        outcome.StatusCode.ShouldBe(400);
        outcome.Message.ShouldBe("At most 1 records can be deleted at once.");
        (await fixture.Repository.CountAsync()).ShouldBe(2);
    }

    [Theory]
    [InlineData(typeof(ConcurrencyError), 409)]
    [InlineData(typeof(NotFoundError), 404)]
    [InlineData(typeof(ValidationError), 422)]
    [InlineData(typeof(ExceptionError), 500)]
    public void FromErrors_MapsErrorTypesToStatusCodes(Type errorType, int expected)
    {
        // Arrange
        IResultError error = errorType == typeof(ConcurrencyError) ? new ConcurrencyError("c")
            : errorType == typeof(NotFoundError) ? new NotFoundError("n")
            : errorType == typeof(ValidationError) ? new ValidationError("v", "Name", null)
            : new ExceptionError(new InvalidOperationException("boom"));

        // Act
        var outcome = EntitiesDashboardOutcome.FromErrors([error], "corr-1");

        // Assert
        outcome.StatusCode.ShouldBe(expected);
        outcome.Success.ShouldBeFalse();
    }

    [Fact]
    public void FromErrors_DatabaseRejectionNamingAColumn_Returns422WithFieldErrorAndCorrelationId()
    {
        // Arrange
        var database = new FakeDbException("String or binary data would be truncated in table 'x.Cities', column 'CountryCode'. Truncated value: 'aa'.\r\nThe statement has been terminated.");
        var error = new ExceptionError(new InvalidOperationException("saving failed", database));

        // Act
        var outcome = EntitiesDashboardOutcome.FromErrors([error], "corr-1", ["Name", "CountryCode"]);

        // Assert
        outcome.StatusCode.ShouldBe(422);
        outcome.CorrelationId.ShouldBe("corr-1");
        var field = outcome.FieldErrors.ShouldHaveSingleItem();
        field.Field.ShouldBe("CountryCode");
        field.Message.ShouldContain("Truncated value: 'aa'.");
        field.Message.ShouldNotContain("terminated");
    }

    [Fact]
    public void FromDatabaseException_NoDatabaseFailure_ReturnsNull()
    {
        // Act
        var outcome = EntitiesDashboardOutcome.FromDatabaseException(new InvalidOperationException("boom"), "corr-1", ["Name"]);

        // Assert
        outcome.ShouldBeNull();
    }

    [Fact]
    public void IsSoftDeleted_SoftDeletableAndAuditable_AreDetected()
    {
        // Arrange
        var note = new ArchivedNote();
        var customer = EntitiesDashboardTestData.NewCustomer(Id1, "Ann");

        // Act & Assert
        EntitiesDashboardWriteService.IsSoftDeleted(note).ShouldBeFalse();
        note.SetDeleted();
        EntitiesDashboardWriteService.IsSoftDeleted(note).ShouldBeTrue();
        EntitiesDashboardWriteService.IsSoftDeleted(customer).ShouldBeFalse();
        customer.AuditState.SetDeleted("tester", "gone");
        EntitiesDashboardWriteService.IsSoftDeleted(customer).ShouldBeTrue();
    }

    private sealed class FakeDbException(string message) : System.Data.Common.DbException(message);

    private sealed class Fixture(ServiceProvider provider) : IDisposable
    {
        public ServiceProvider Provider { get; } = provider;

        public EntitiesDashboardWriteService Sut { get; } = provider.GetRequiredService<EntitiesDashboardWriteService>();

        public HttpContext Http { get; } = EntitiesDashboardTestData.CreateHttpContext(provider);

        public string Key { get; } = typeof(Customer).FullName;

        public IGenericRepository<Customer> Repository { get; } = provider.GetRequiredService<IGenericRepository<Customer>>();

        public async Task<Customer> FindAsync(Guid id) => await this.Repository.FindOneAsync(CustomerId.Create(id));

        public async Task<JsonElement> UpdateBodyAsync(Guid id, object values, string[] replace = null)
        {
            var current = await this.FindAsync(id);

            return EntitiesDashboardTestData.Json(new
            {
                entity = this.Key,
                id = id.ToString(),
                concurrencyVersion = current.ConcurrencyVersion.ToString(),
                values,
                replace = replace ?? []
            });
        }

        public void Dispose() => this.Provider.Dispose();
    }
}
