// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests;

// https://xunit.net/docs/shared-context#collection-fixture
[CollectionDefinition(nameof(TestEnvironmentCollection), DisableParallelization = true)]
public class TestEnvironmentCollection : ICollectionFixture<TestEnvironmentFixture>;

[CollectionDefinition(nameof(EntityFrameworkCosmosTypedIdTestEnvironmentCollection), DisableParallelization = true)]
public class EntityFrameworkCosmosTypedIdTestEnvironmentCollection : ICollectionFixture<TestEnvironmentFixture>;

[CollectionDefinition(nameof(EntityFrameworkCosmosDocumentStoreTestEnvironmentCollection), DisableParallelization = true)]
public class EntityFrameworkCosmosDocumentStoreTestEnvironmentCollection : ICollectionFixture<TestEnvironmentFixture>;

[CollectionDefinition(nameof(EntityFrameworkCosmosRepositoryTestEnvironmentCollection), DisableParallelization = true)]
public class EntityFrameworkCosmosRepositoryTestEnvironmentCollection : ICollectionFixture<TestEnvironmentFixture>;

[CollectionDefinition(nameof(ActiveEntityTestEnvironmentCollection), DisableParallelization = true)]
public class ActiveEntityTestEnvironmentCollection : ICollectionFixture<TestEnvironmentFixture>;

[CollectionDefinition(nameof(IsolatedSqliteTestEnvironmentCollection), DisableParallelization = true)]
public class IsolatedSqliteTestEnvironmentCollection;
