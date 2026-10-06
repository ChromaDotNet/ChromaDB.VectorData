// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using ChromaDB.Client;
using ChromaDB.VectorData.ConformanceTests.Support;
using Microsoft.Extensions.VectorData;
using VectorData.ConformanceTests;
using VectorData.ConformanceTests.Support;
using Xunit;

namespace ChromaDB.VectorData.ConformanceTests;

/// <summary>
/// Hybrid search needs the Search API and the sparse vector indexes of Chroma, which only Chroma Cloud has:
/// these tests run only against Chroma Cloud, with CHROMA_TEST_URI, and are skipped otherwise.
/// </summary>
public class ChromaHybridSearchTests(ChromaHybridSearchTests.VectorAndStringFixture vectorAndStringFixture, ChromaHybridSearchTests.MultiTextFixture multiTextFixture)
    : HybridSearchTests<string>(vectorAndStringFixture, multiTextFixture),
        IClassFixture<ChromaHybridSearchTests.VectorAndStringFixture>,
        IClassFixture<ChromaHybridSearchTests.MultiTextFixture>
{
    private const string SkipReason = "Hybrid search needs Chroma Cloud.";

    public override Task HybridSearchAsync()
    {
        Assert.SkipUnless(ChromaTestStore.IsChromaCloud, SkipReason);
        return base.HybridSearchAsync();
    }

    public override Task HybridSearchAsync_with_filter()
    {
        Assert.SkipUnless(ChromaTestStore.IsChromaCloud, SkipReason);
        return base.HybridSearchAsync_with_filter();
    }

    public override Task HybridSearchAsync_with_top()
    {
        Assert.SkipUnless(ChromaTestStore.IsChromaCloud, SkipReason);
        return base.HybridSearchAsync_with_top();
    }

    public override Task HybridSearchAsync_with_Skip()
    {
        Assert.SkipUnless(ChromaTestStore.IsChromaCloud, SkipReason);
        return base.HybridSearchAsync_with_Skip();
    }

    public override Task HybridSearchAsync_with_multiple_keywords_ranks_matched_keywords_higher()
    {
        Assert.SkipUnless(ChromaTestStore.IsChromaCloud, SkipReason);
        return base.HybridSearchAsync_with_multiple_keywords_ranks_matched_keywords_higher();
    }

    public override Task HybridSearchAsync_with_multiple_text_properties()
    {
        Assert.SkipUnless(ChromaTestStore.IsChromaCloud, SkipReason);
        return base.HybridSearchAsync_with_multiple_text_properties();
    }

    public override Task HybridSearchAsync_without_explicitly_specified_property_fails()
    {
        Assert.SkipUnless(ChromaTestStore.IsChromaCloud, SkipReason);
        return base.HybridSearchAsync_without_explicitly_specified_property_fails();
    }

    public new class VectorAndStringFixture : HybridSearchTests<string>.VectorAndStringFixture
    {
        public override TestStore TestStore => ChromaTestStore.Instance;

        protected override VectorStoreCollection<string, VectorAndStringRecord<string>> GetCollection()
            => ChromaTestStore.Instance.CreateCollectionWithBm25Indexes<VectorAndStringRecord<string>>(this.CollectionName, this.CreateRecordDefinition());

        public override ValueTask InitializeAsync()
            => ChromaTestStore.IsChromaCloud ? base.InitializeAsync() : default;

        public override ValueTask DisposeAsync()
            => ChromaTestStore.IsChromaCloud ? base.DisposeAsync() : default;
    }

    public new class MultiTextFixture : HybridSearchTests<string>.MultiTextFixture
    {
        public override TestStore TestStore => ChromaTestStore.Instance;

        protected override VectorStoreCollection<string, MultiTextStringRecord<string>> GetCollection()
            => ChromaTestStore.Instance.CreateCollectionWithBm25Indexes<MultiTextStringRecord<string>>(this.CollectionName, this.CreateRecordDefinition());

        public override ValueTask InitializeAsync()
            => ChromaTestStore.IsChromaCloud ? base.InitializeAsync() : default;

        public override ValueTask DisposeAsync()
            => ChromaTestStore.IsChromaCloud ? base.DisposeAsync() : default;
    }
}
