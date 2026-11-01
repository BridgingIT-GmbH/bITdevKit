// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Examples.DoFiesta.IntegrationTests;

/// <summary>Shares one sample host and serializes access to its isolated SQLite connection.</summary>
/// <example><code>[Collection("DoFiesta")] public class HostTests { }</code></example>
[CollectionDefinition("DoFiesta", DisableParallelization = true)]
public sealed class DoFiestaTestCollection : ICollectionFixture<DoFiestaApplicationFactory>;
