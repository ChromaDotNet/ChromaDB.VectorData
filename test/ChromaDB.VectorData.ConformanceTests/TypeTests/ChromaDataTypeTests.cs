// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using ChromaDB.VectorData.ConformanceTests.Support;
using VectorData.ConformanceTests.Support;
using VectorData.ConformanceTests.TypeTests;
using Xunit;

namespace ChromaDB.VectorData.ConformanceTests.TypeTests;

public class ChromaDataTypeTests(ChromaDataTypeTests.Fixture fixture)
    : DataTypeTests<Guid, DataTypeTests<Guid>.DefaultRecord>(fixture), IClassFixture<ChromaDataTypeTests.Fixture>
{
    public new class Fixture : DataTypeTests<Guid, DataTypeTests<Guid>.DefaultRecord>.Fixture
    {
        public override TestStore TestStore => ChromaTestStore.Instance;

        public override Type[] UnsupportedDefaultTypes { get; } =
        [
            typeof(byte),
            typeof(short),
            typeof(decimal),
            typeof(Guid),
#if NET
            typeof(TimeOnly)
#endif
        ];
    }
}
