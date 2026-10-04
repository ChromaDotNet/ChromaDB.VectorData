// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using ChromaDB.VectorData.ConformanceTests.Support;
using VectorData.ConformanceTests.ModelTests;
using VectorData.ConformanceTests.Support;
using Xunit;

namespace ChromaDB.VectorData.ConformanceTests.ModelTests;

public class ChromaMultiVectorModelTests(ChromaMultiVectorModelTests.Fixture fixture)
    : MultiVectorModelTests<ulong>(fixture), IClassFixture<ChromaMultiVectorModelTests.Fixture>
{
    public new class Fixture : MultiVectorModelTests<ulong>.Fixture
    {
        public override TestStore TestStore => ChromaTestStore.NamedVectorsInstance;
    }
}
