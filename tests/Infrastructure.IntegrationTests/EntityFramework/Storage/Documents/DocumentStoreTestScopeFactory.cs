namespace BridgingIT.DevKit.Infrastructure.IntegrationTests;

using Microsoft.Extensions.DependencyInjection;

public static class DocumentStoreTestScopeFactory
{
    public static IServiceScopeFactory Create(Func<StubDbContext> contextFactory)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => contextFactory());
        return services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }
}
