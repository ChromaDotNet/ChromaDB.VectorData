// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using VectorData.ConformanceTests;
using VectorData.ConformanceTests.ModelTests;

namespace ChromaDB.VectorData.ConformanceTests;

public class ChromaTestSuiteImplementationTests : TestSuiteImplementationTests
{
    protected override ICollection<Type> IgnoredTestBases { get; } =
    [
        // Hybrid search not supported
        typeof(HybridSearchTests<>),

        // A Chroma record has exactly one vector
        typeof(MultiVectorModelTests<>),
        typeof(NoVectorModelTests<>),
    ];
}
