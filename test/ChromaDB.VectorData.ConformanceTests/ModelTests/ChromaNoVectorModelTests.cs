// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using ChromaDB.VectorData.ConformanceTests.Support;
using VectorData.ConformanceTests.ModelTests;
using VectorData.ConformanceTests.Support;
using Xunit;

namespace ChromaDB.VectorData.ConformanceTests.ModelTests;

public class ChromaNoVectorModelTests(ChromaNoVectorModelTests.Fixture fixture)
    : NoVectorModelTests<string>(fixture), IClassFixture<ChromaNoVectorModelTests.Fixture>
{
    public new class Fixture : NoVectorModelTests<string>.Fixture
    {
        public override TestStore TestStore => ChromaTestStore.Instance;
    }
}
