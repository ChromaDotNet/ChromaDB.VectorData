// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using ChromaDB.Client;
using Microsoft.Extensions.VectorData;
using Testcontainers.Chroma;
using VectorData.ConformanceTests.Support;

namespace ChromaDB.VectorData.ConformanceTests.Support;

#pragma warning disable CA1001 // Type owns disposable fields but is not disposable

internal sealed class ChromaTestStore : TestStore
{
    public static ChromaTestStore Instance { get; } = new();

    // Chroma indexes vectors with HNSW only
    public override string DefaultIndexKind => IndexKind.Hnsw;

    // CHROMA_IMAGE runs the tests against another Chroma release, e.g. chromadb/chroma:1.5.0.
    private readonly ChromaContainer _container = new ChromaBuilder(Environment.GetEnvironmentVariable("CHROMA_IMAGE") ?? "chromadb/chroma:1.5.9").Build();

    private HttpClient? _httpClient;

    /// <summary>
    /// Chroma normalizes the vectors of cosine collections, so the vectors it returns
    /// can differ from the upserted ones in the last digits; we can only check that
    /// a vector was returned.
    /// </summary>
    public override bool VectorsComparable => false;

    public ChromaConfigurationOptions ChromaOptions { get; private set; } = null!;

    public HttpClient HttpClient => this._httpClient ?? throw new InvalidOperationException("Not initialized");

    public ChromaVectorStore GetVectorStore(ChromaVectorStoreOptions options)
        => new(this.ChromaOptions,
            this.HttpClient,
            ownsClient: false, // The client is shared, it's not owned by the vector store.
            new()
            {
                EmbeddingGenerator = options.EmbeddingGenerator
            });

    private ChromaTestStore()
    {
    }

    protected override async Task StartAsync()
    {
        await this._container.StartAsync();
        this.ChromaOptions = new ChromaConfigurationOptions(this._container.GetConnectionString());
        this._httpClient = new HttpClient();
        // The vector store does not own a ChromaClient it is given; GetVectorStore covers the constructor with options.
        this.DefaultVectorStore = new ChromaVectorStore(new ChromaClient(this.ChromaOptions, this._httpClient));
    }

    protected override async Task StopAsync()
    {
        this._httpClient?.Dispose();
        await this._container.StopAsync();
    }
}
