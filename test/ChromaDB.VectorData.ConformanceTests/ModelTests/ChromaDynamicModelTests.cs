// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using ChromaDB.VectorData.ConformanceTests.Support;
using VectorData.ConformanceTests.ModelTests;
using VectorData.ConformanceTests.Support;
using Xunit;

namespace ChromaDB.VectorData.ConformanceTests.ModelTests;

public class ChromaDynamicModelTests_NamedVectors(ChromaDynamicModelTests_NamedVectors.Fixture fixture)
    : DynamicModelTests<ulong>(fixture), IClassFixture<ChromaDynamicModelTests_NamedVectors.Fixture>
{
    public override async Task GetAsync_with_filter_and_multiple_OrderBys()
    {
        var exception = await Assert.ThrowsAsync<NotSupportedException>(base.GetAsync_with_filter_and_multiple_OrderBys);

        Assert.Equal("Qdrant does not support ordering by more than one property.", exception.Message);
    }

    public new class Fixture : DynamicModelTests<ulong>.Fixture
    {
        public override TestStore TestStore => ChromaTestStore.NamedVectorsInstance;
    }
}

public class ChromaDynamicModelTests_UnnamedVector(ChromaDynamicModelTests_UnnamedVector.Fixture fixture)
    : DynamicModelTests<ulong>(fixture), IClassFixture<ChromaDynamicModelTests_UnnamedVector.Fixture>
{
    public override async Task GetAsync_with_filter_and_multiple_OrderBys()
    {
        var exception = await Assert.ThrowsAsync<NotSupportedException>(base.GetAsync_with_filter_and_multiple_OrderBys);

        Assert.Equal("Qdrant does not support ordering by more than one property.", exception.Message);
    }

    public new class Fixture : DynamicModelTests<ulong>.Fixture
    {
        public override TestStore TestStore => ChromaTestStore.UnnamedVectorInstance;
    }
}
