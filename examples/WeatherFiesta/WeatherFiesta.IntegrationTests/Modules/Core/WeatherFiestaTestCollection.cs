// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.IntegrationTests;

/// <summary>
/// Shared integration test collection for WeatherFiesta tests using WebApplicationFactory.
/// Keeps one factory instance so module discovery/registration and ActiveEntity global provider remain stable.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class WeatherFiestaTestCollection : ICollectionFixture<WeatherFiestaApplicationFactory>
{
    /// <summary>The xUnit collection name.</summary>
    public const string Name = "WeatherFiesta";
}
