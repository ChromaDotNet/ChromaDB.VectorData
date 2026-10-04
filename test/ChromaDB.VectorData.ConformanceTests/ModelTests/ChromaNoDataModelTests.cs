// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using ChromaDB.VectorData.ConformanceTests.Support;
using VectorData.ConformanceTests.ModelTests;
using VectorData.ConformanceTests.Support;
using Xunit;

namespace ChromaDB.VectorData.ConformanceTests.ModelTests;

public class ChromaNoDataModelTests_NamedVectors(ChromaNoDataModelTests_NamedVectors.Fixture fixture)
    : NoDataModelTests<ulong>(fixture), IClassFixture<ChromaNoDataModelTests_NamedVectors.Fixture>
{
    public new class Fixture : NoDataModelTests<ulong>.Fixture
    {
        public override TestStore TestStore => ChromaTestStore.NamedVectorsInstance;
    }
}

public class ChromaNoDataModelTests_UnnamedVectors(ChromaNoDataModelTests_UnnamedVectors.Fixture fixture)
    : NoDataModelTests<ulong>(fixture), IClassFixture<ChromaNoDataModelTests_UnnamedVectors.Fixture>
{
    public new class Fixture : NoDataModelTests<ulong>.Fixture
    {
        public override TestStore TestStore => ChromaTestStore.UnnamedVectorInstance;
    }
}
