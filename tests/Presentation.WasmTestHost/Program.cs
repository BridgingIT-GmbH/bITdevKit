// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

using BridgingIT.DevKit.Presentation.Web.Client;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Presentation.WasmTestHost;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
builder.Services.AddAppState().WithApplicationId("catalog-wasm-probe").AddState<StateProbeState>()
    .AsLocalStorageScoped().WithJsonTypeInfo(StateProbeJson.Default.StateProbeModel).Done();
await builder.Build().RunAsync();
