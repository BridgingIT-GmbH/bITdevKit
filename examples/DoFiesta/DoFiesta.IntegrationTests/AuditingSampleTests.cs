// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Examples.DoFiesta.IntegrationTests;

using System.Net;
using System.Net.Http.Json;
using BridgingIT.DevKit.Application.Auditing;
using BridgingIT.DevKit.Infrastructure.EntityFramework;

/// <summary>Proves sample business capture, committed ChangeHistory evidence and dashboard authorization.</summary>
/// <example><code>dotnet test --filter FullyQualifiedName~AuditingSampleTests</code></example>
[Collection("DoFiesta")]
public sealed class AuditingSampleTests(DoFiestaApplicationFactory factory)
{
    [Fact]
    public async Task CreateTodo_RecordsBusinessResourcesAndCommittedChangeHistory()
    {
        var userId = "auditing-" + Guid.NewGuid().ToString("N");
        await using (var permissions = factory.Services.CreateAsyncScope())
        {
            var context = permissions.ServiceProvider.GetRequiredService<CoreDbContext>();
            context.EntityPermissions.Add(new EntityPermission { Id = Guid.NewGuid(), UserId = userId,
                EntityType = typeof(TodoItem).FullName, Permission = Permission.Write, Module = "Core" });
            await context.SaveChangesAsync();
        }

        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-User", userId);
        using var response = await client.PostAsJsonAsync("/api/core/todoitems", new TodoItemModel
        {
            Title = "Audit sample " + Guid.NewGuid().ToString("N"), Status = 1, Priority = 1, Assignee = "fictional@example.test"
        });
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var model = await response.Content.ReadFromJsonAsync<TodoItemModel>();
        var id = Guid.Parse(response.Headers.GetValues("X-Audit-Id").Single());
        await using var reading = factory.Services.CreateAsyncScope();
        var queries = reading.ServiceProvider.GetRequiredService<IAuditQueryService>();
        var found = await queries.FindAsync(id);
        found.IsSuccess.ShouldBeTrue(string.Join(",", found.Errors.OfType<AuditError>().Select(error => error.Code)) +
            "; buffered=" + reading.ServiceProvider.GetRequiredService<IAuditingStorageProvider>().Capabilities.Buffered +
            "; failures=" + string.Join(",", reading.ServiceProvider.GetRequiredService<AuditRuntime>().SnapshotFailures().Select(pair => pair.Key + ":" + pair.Value)));
        found.Value.Record.Outcome.ShouldBe(AuditOutcome.Succeeded);
        found.Value.Record.Actions.Any(action => action.Details.ContainsKey("todo.number")).ShouldBeTrue();
        var changes = found.Value.Record.EntityChanges.Where(change => change.EntityId == model.Id).ToArray();
        changes.ShouldNotBeEmpty();
        changes.All(change => change.PersistenceState == "Committed").ShouldBeTrue();
        changes.SelectMany(change => change.Properties).ShouldNotBeEmpty();
        var contextRead = reading.ServiceProvider.GetRequiredService<CoreDbContext>();
        (await contextRead.ChangeHistory.AnyAsync(row => row.EntityId == model.Id)).ShouldBeTrue();
        (await contextRead.AuditRecords.AnyAsync(row => row.AuditId == id)).ShouldBeTrue();
    }

    [Fact]
    public async Task AuditingDashboard_RequiresAdministratorAndRendersForAdministrator()
    {
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-User", factory.UserId);
        using var denied = await client.GetAsync("/_bdk/dashboard/auditing");
        denied.StatusCode.ShouldBe(HttpStatusCode.Found);
        denied.Headers.Location.OriginalString.ShouldContain("access-denied");
        client.DefaultRequestHeaders.Add("X-Test-Admin", "true");
        using var allowed = await client.GetAsync("/_bdk/dashboard/auditing");
        allowed.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await allowed.Content.ReadAsStringAsync()).ShouldContain("Auditing");
    }
}
