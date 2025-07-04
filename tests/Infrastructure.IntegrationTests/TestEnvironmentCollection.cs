﻿// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests;

[CollectionDefinition(nameof(TestEnvironmentCollection))]
public class TestEnvironmentCollection : ICollectionFixture<TestEnvironmentFixture>;

[CollectionDefinition(nameof(TestEnvironmentCollection2))]
public class TestEnvironmentCollection2 : ICollectionFixture<TestEnvironmentFixture>;

[CollectionDefinition(nameof(TestEnvironmentCollection3))]
public class TestEnvironmentCollection3 : ICollectionFixture<TestEnvironmentFixture>;

[CollectionDefinition(nameof(TestEnvironmentCollection4))]
public class TestEnvironmentCollection4 : ICollectionFixture<TestEnvironmentFixture>;
// https://xunit.net/docs/shared-context#collection-fixture