// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

using BridgingIT.DevKit.Presentation.Web;
using BridgingIT.DevKit.Presentation.Web.Client;
using Presentation.TestHost;
using Presentation.TestHost.Components;
using Presentation.TestHost.ProblemDetails;

var builder = DevKitWebApplication.CreateBuilder(args).AddConfiguration().AddLogging();
builder.WebApplicationBuilder.WebHost.UseStaticWebAssets();
builder.Services.ConfigureJson();
var problemDetails = new ProblemDetailsScenario(builder.Configuration);
problemDetails.AddServices(builder.Services);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddAppState().WithApplicationId("catalog-server-probe").AddState<StateProbeState>()
    .AsLocalStorageScoped().WithJsonTypeInfo(StateProbeJson.Default.StateProbeModel).Done();
builder.Services.AddAppState().WithApplicationId("catalog-server-probe").AddState<LifecycleProbeState>()
    .WithIdentity("reports", "filter").AsLocalStorageScoped()
    .WithJsonTypeInfo(LifecycleProbeJson.Default.LifecycleProbeModel).WithHistory(3).Done();
var app = builder.Build();
app.UseRequestCorrelation();
problemDetails.UseMiddleware(app);
app.UseRouting();
app.UseAntiforgery();
problemDetails.MapEndpoints(app);
app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();
