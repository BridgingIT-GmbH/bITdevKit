// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Examples.DoFiesta.IntegrationTests;

/// <summary>Shares one sample host and its independently owned SQL Server catalog across related integration tests.</summary>
/// <example><code>[Collection("DoFiesta")] public class HostTests { }</code></example>
[CollectionDefinition("DoFiesta", DisableParallelization = true)]
public sealed class DoFiestaTestCollection : ICollectionFixture<DoFiestaApplicationFactory>;