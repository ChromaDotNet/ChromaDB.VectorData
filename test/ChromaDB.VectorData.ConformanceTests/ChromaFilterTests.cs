// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using ChromaDB.VectorData.ConformanceTests.Support;
using VectorData.ConformanceTests;
using VectorData.ConformanceTests.Support;
using Xunit;

namespace ChromaDB.VectorData.ConformanceTests;

public class ChromaFilterTests(ChromaFilterTests.Fixture fixture)
    : FilterTests<string>(fixture), IClassFixture<ChromaFilterTests.Fixture>
{
    public new class Fixture : FilterTests<string>.Fixture
    {
        public override TestStore TestStore => ChromaTestStore.Instance;
    }
}
