// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using ChromaDB.VectorData.ConformanceTests.Support;
using VectorData.ConformanceTests;
using Xunit;

namespace ChromaDB.VectorData.ConformanceTests;

public class ChromaCollectionManagementTests_NamedVectors(ChromaNamedVectorsFixture fixture)
    : CollectionManagementTests<ulong>(fixture), IClassFixture<ChromaNamedVectorsFixture>
{
}

public class ChromaCollectionManagementTests_UnnamedVector(ChromaUnnamedVectorFixture fixture)
    : CollectionManagementTests<ulong>(fixture), IClassFixture<ChromaUnnamedVectorFixture>
{
}
