namespace BridgingIT.DevKit.Application.UnitTests.Storage;

using Application.Storage;

public abstract class DocumentPropertiesContractTests
{
    protected abstract IDocumentStoreProvider CreateProvider();

    [Fact]
    public async Task Properties_WithScalarsAndMutations_PreserveTypesAndSnapshots()
    {
        var sut = new DocumentStoreClient<PropertyDocument>(this.CreateProvider());
        var key = new DocumentKey("people", "properties");
        var input = StoragePropertySamples.Create();
        var expected = StoragePropertySamples.Create();
        var write = await sut.UpsertAsync(key, new() { FirstName = "Ada" }, new() { Properties = input });
        write.IsSuccess.ShouldBeTrue(string.Join("; ", write.Errors.Select(error => error.Message)));
        ((byte[])input.Get("Bytes"))[0] = 99;
        StoragePropertySamples.AssertEqual(write.Value.Properties, expected);
        ((byte[])write.Value.Properties.Get("Bytes"))[0] = 98;
        StoragePropertySamples.AssertEqual((await sut.GetAsync(key)).Value.Properties, expected);
        var replace = await sut.UpsertAsync(key, new() { FirstName = "Grace" });
        replace.IsSuccess.ShouldBeTrue();
        StoragePropertySamples.AssertEqual(replace.Value.Properties, expected);
        StoragePropertySamples.AssertEqual((await sut.UpdatePropertiesAsync(new(key))).Value.Properties, expected);
        var conflict = await sut.UpdatePropertiesAsync(new(key) { Properties = new(), IfMatchETag = write.Value.ETag });
        conflict.HasError<DocumentStoreConflictError>().ShouldBeTrue();
        StoragePropertySamples.AssertEqual((await sut.GetAsync(key)).Value.Properties, expected);
        (await sut.UpdatePropertiesAsync(new(key) { Properties = new() })).Value.Properties.Count.ShouldBe(0);
        var explicitNull = await sut.UpdatePropertiesAsync(new(key) { Properties = new() { ["present"] = null } });
        explicitNull.Value.Properties.Contains("present").ShouldBeTrue();
        (await sut.UpsertAsync(key, new(), new() { Properties = new() })).Value.Properties.Count.ShouldBe(0);
    }
}

public sealed class PropertyDocument { public string FirstName { get; set; } }
