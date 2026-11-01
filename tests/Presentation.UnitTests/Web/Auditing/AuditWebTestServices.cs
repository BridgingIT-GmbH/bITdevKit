// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Auditing;

using System.Security.Claims;
using BridgingIT.DevKit.Application.Auditing;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

public static class AuditWebTestServices
{
    public static ServiceProvider Create(Action<AuditingBuilder> configure = null, Action<IServiceCollection> services = null)
    {
        var collection = new ServiceCollection();
        collection.AddLogging();
        collection.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        services?.Invoke(collection);
        var auditing = collection.AddAuditing().WithEndpoints(options => options.RequireAuthorization = false);
        configure?.Invoke(auditing);
        return collection.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    public static DefaultHttpContext Context(IServiceProvider services, string query = null, string user = "reviewer")
    {
        var context = new DefaultHttpContext
        {
            RequestServices = services,
            User = new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user)], "fixture"))
        };
        context.Request.QueryString = new(query ?? "");
        context.Response.Body = new MemoryStream();
        return context;
    }

    public static async Task<Guid> CaptureAsync(IServiceProvider services)
    {
        var audit = services.GetRequiredService<IAuditContext>();
        await using var root = audit.BeginScope(options => options.Key("customers.rename").DisplayName("Rename customer"));
        root.AddResource(new("Customer", "C-42"));
        await using (var action = audit.BeginScope(options => options.Key("customers.save").DisplayName("Save customer")))
        {
            action.Complete();
        }

        root.Complete();
        return root.AuditId;
    }
}
