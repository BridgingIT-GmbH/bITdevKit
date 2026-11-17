// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

using BridgingIT.DevKit.Presentation.Web.Client;
using Presentation.TestHost;
using Presentation.TestHost.Components;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseStaticWebAssets();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddAppState().WithApplicationId("catalog-server-probe").AddState<StateProbeState>()
    .AsLocalStorageScoped().WithJsonTypeInfo(StateProbeJson.Default.StateProbeModel).Done();
builder.Services.AddAppState().WithApplicationId("catalog-server-probe").AddState<LifecycleProbeState>()
    .WithIdentity("reports", "filter").AsLocalStorageScoped()
    .WithJsonTypeInfo(LifecycleProbeJson.Default.LifecycleProbeModel).WithHistory(3).Done();
var app = builder.Build();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();
