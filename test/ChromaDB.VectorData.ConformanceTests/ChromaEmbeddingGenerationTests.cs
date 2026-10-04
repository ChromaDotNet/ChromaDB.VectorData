// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.VectorData;
using ChromaDB.VectorData.ConformanceTests.Support;
using VectorData.ConformanceTests;
using VectorData.ConformanceTests.Support;
using Xunit;

namespace ChromaDB.VectorData.ConformanceTests;

public class ChromaEmbeddingGenerationTests(ChromaEmbeddingGenerationTests.StringVectorFixture stringVectorFixture, ChromaEmbeddingGenerationTests.RomOfFloatVectorFixture romOfFloatVectorFixture)
    : EmbeddingGenerationTests<Guid>(stringVectorFixture, romOfFloatVectorFixture), IClassFixture<ChromaEmbeddingGenerationTests.StringVectorFixture>, IClassFixture<ChromaEmbeddingGenerationTests.RomOfFloatVectorFixture>
{
    public new class StringVectorFixture : EmbeddingGenerationTests<Guid>.StringVectorFixture
    {
        public override TestStore TestStore => ChromaTestStore.Instance;

        public override VectorStore CreateVectorStore(IEmbeddingGenerator? embeddingGenerator)
            => ChromaTestStore.Instance.GetVectorStore(new() { EmbeddingGenerator = embeddingGenerator });

        public override Func<IServiceCollection, IServiceCollection>[] DependencyInjectionStoreRegistrationDelegates =>
        [
            services => services
                .AddSingleton(ChromaTestStore.Instance.ChromaOptions)
                .AddChromaVectorStore()
        ];

        public override Func<IServiceCollection, IServiceCollection>[] DependencyInjectionCollectionRegistrationDelegates =>
        [
            services => services
                .AddSingleton(ChromaTestStore.Instance.ChromaOptions)
                .AddChromaCollection<Guid, RecordWithAttributes>(this.CollectionName)
        ];
    }

    public new class RomOfFloatVectorFixture : EmbeddingGenerationTests<Guid>.RomOfFloatVectorFixture
    {
        public override TestStore TestStore => ChromaTestStore.Instance;

        public override VectorStore CreateVectorStore(IEmbeddingGenerator? embeddingGenerator)
            => ChromaTestStore.Instance.GetVectorStore(new() { EmbeddingGenerator = embeddingGenerator });

        public override Func<IServiceCollection, IServiceCollection>[] DependencyInjectionStoreRegistrationDelegates =>
        [
            services => services
                .AddSingleton(ChromaTestStore.Instance.ChromaOptions)
                .AddChromaVectorStore()
        ];

        public override Func<IServiceCollection, IServiceCollection>[] DependencyInjectionCollectionRegistrationDelegates =>
        [
            services => services
                .AddSingleton(ChromaTestStore.Instance.ChromaOptions)
                .AddChromaCollection<Guid, RecordWithAttributes>(this.CollectionName)
        ];
    }
}
