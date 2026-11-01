// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Auditing;

using System.Collections.Immutable;
using System.Net.Http.Json;
using BridgingIT.DevKit.Application.Auditing;
using BridgingIT.DevKit.Presentation;
using BridgingIT.DevKit.Presentation.Web.Auditing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using AuditEndpoints = BridgingIT.DevKit.Presentation.Web.Auditing.Dashboard.DashboardEndpoints;

public class AuditExportTransportTests
{
    [Theory]
    [InlineData("csv")]
    [InlineData("ndjson")]
    public async Task DownloadAsync_RealHttpResponse_CompletesComplexRecord(string format)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        builder.Services.AddAuditing();
        builder.Services.AddDashboard(options => options.AllowAnonymous().WithGroupPath("/ops").WithPluginAssemblyContaining<AuditEndpoints>());
        await using var app = builder.Build();
        app.MapEndpoints();
        await app.StartAsync();
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var record = new AuditRecord
        {
            AuditId = id, OriginAuditId = id, Key = "customers.import", StartedUtc = now.AddMinutes(-1), CompletedUtc = now,
            Actions = Enumerable.Range(0, 80).Select(index => new AuditAction
            {
                Id = Guid.NewGuid(), Sequence = index, Key = "customers.save", DisplayName = new string('x', 500),
                Details = ImmutableDictionary<string, AuditValue>.Empty.Add("description", AuditValue.From(new string('y', 500)))
            }).ToImmutableArray()
        };
        (await app.Services.GetRequiredService<IAuditingStorageProvider>().AppendAsync(record)).IsSuccess.ShouldBeTrue();
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        var ticket = await client.GetFromJsonAsync<AuditDownloadTicket>("/ops/auditing/download/ticket");
        using var form = new FormUrlEncodedContent(new Dictionary<string, string> { ["ticket"] = ticket.Ticket, ["format"] = format, ["filter"] = "{}" });

        using var response = await client.PostAsync("/ops/auditing/download", form);
        response.EnsureSuccessStatusCode();
        var text = await response.Content.ReadAsStringAsync();

        text.ShouldContain("customers.import");
        if (format == "csv")
        {
            using var csv = new Microsoft.VisualBasic.FileIO.TextFieldParser(new StringReader(text)) { HasFieldsEnclosedInQuotes = true };
            csv.SetDelimiters(",");
            csv.ReadFields().Length.ShouldBe(17);
            var cells = csv.ReadFields();
            using var actions = System.Text.Json.JsonDocument.Parse(cells[14]);
            actions.RootElement.GetArrayLength().ShouldBe(80);
        }

        var completion = await client.GetFromJsonAsync<AuditExportCompletion>($"/ops/auditing/exports/{ticket.ExportId}");
        completion.OutputConfirmed.ShouldBeTrue();
        completion.Status.ShouldBe("Completed");
        completion.SourceRecordCount.ShouldBe(1);
    }
}
