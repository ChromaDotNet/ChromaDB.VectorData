// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using ChromaDB.VectorData.ConformanceTests.Support;
using VectorData.ConformanceTests.Support;
using VectorData.ConformanceTests.TypeTests;
using Xunit;

namespace ChromaDB.VectorData.ConformanceTests.TypeTests;

public class ChromaKeyTypeTests(ChromaKeyTypeTests.Fixture fixture)
    : KeyTypeTests(fixture), IClassFixture<ChromaKeyTypeTests.Fixture>
{
    [Fact]
    public virtual Task ULong() => this.Test<ulong>(8UL);

    public new class Fixture : KeyTypeTests.Fixture
    {
        public override TestStore TestStore => ChromaTestStore.NamedVectorsInstance;
    }
}
