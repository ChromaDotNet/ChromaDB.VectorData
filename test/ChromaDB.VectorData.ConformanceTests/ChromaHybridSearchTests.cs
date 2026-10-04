// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using ChromaDB.VectorData.ConformanceTests.Support;
using VectorData.ConformanceTests;
using VectorData.ConformanceTests.Support;
using Xunit;

namespace ChromaDB.VectorData.ConformanceTests;

public class ChromaHybridSearchTests_NamedVectors(
    ChromaHybridSearchTests_NamedVectors.VectorAndStringFixture vectorAndStringFixture,
    ChromaHybridSearchTests_NamedVectors.MultiTextFixture multiTextFixture)
    : HybridSearchTests<ulong>(vectorAndStringFixture, multiTextFixture),
        IClassFixture<ChromaHybridSearchTests_NamedVectors.VectorAndStringFixture>,
        IClassFixture<ChromaHybridSearchTests_NamedVectors.MultiTextFixture>
{
    public new class VectorAndStringFixture : HybridSearchTests<ulong>.VectorAndStringFixture
    {
        public override TestStore TestStore => ChromaTestStore.NamedVectorsInstance;
    }

    public new class MultiTextFixture : HybridSearchTests<ulong>.MultiTextFixture
    {
        public override TestStore TestStore => ChromaTestStore.NamedVectorsInstance;
    }
}

public class ChromaHybridSearchTests_UnnamedVectors(
    ChromaHybridSearchTests_UnnamedVectors.VectorAndStringFixture vectorAndStringFixture,
    ChromaHybridSearchTests_UnnamedVectors.MultiTextFixture multiTextFixture)
    : HybridSearchTests<ulong>(vectorAndStringFixture, multiTextFixture),
        IClassFixture<ChromaHybridSearchTests_UnnamedVectors.VectorAndStringFixture>,
        IClassFixture<ChromaHybridSearchTests_UnnamedVectors.MultiTextFixture>
{
    public new class VectorAndStringFixture : HybridSearchTests<ulong>.VectorAndStringFixture
    {
        public override TestStore TestStore => ChromaTestStore.UnnamedVectorInstance;
    }

    public new class MultiTextFixture : HybridSearchTests<ulong>.MultiTextFixture
    {
        public override TestStore TestStore => ChromaTestStore.UnnamedVectorInstance;
    }
}
