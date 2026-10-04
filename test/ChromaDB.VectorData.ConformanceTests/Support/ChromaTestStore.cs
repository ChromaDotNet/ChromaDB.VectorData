// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using ChromaDB.Client;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.VectorData;
using VectorData.ConformanceTests.Support;

namespace ChromaDB.VectorData.ConformanceTests.Support;

#pragma warning disable CA1001 // Type owns disposable fields but is not disposable

internal sealed class ChromaTestStore : TestStore
{
    private const ushort ChromaPort = 8000;

    public static ChromaTestStore Instance { get; } = new();

    // Chroma indexes vectors with HNSW only
    public override string DefaultIndexKind => IndexKind.Hnsw;

    // CHROMA_IMAGE runs the tests against another Chroma release, e.g. chromadb/chroma:1.5.0.
    private readonly IContainer _container = new ContainerBuilder(Environment.GetEnvironmentVariable("CHROMA_IMAGE") ?? "chromadb/chroma:1.5.9")
        .WithPortBinding(ChromaPort, assignRandomHostPort: true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request.ForPath("/api/v2/heartbeat").ForPort(ChromaPort)))
        .Build();

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
        this.ChromaOptions = new ChromaConfigurationOptions($"http://{this._container.Hostname}:{this._container.GetMappedPublicPort(ChromaPort)}");
        this._httpClient = new HttpClient();
        // The client is shared, it's not owned by the vector store.
        this.DefaultVectorStore = new ChromaVectorStore(this.ChromaOptions, this._httpClient, ownsClient: false);
    }

    protected override async Task StopAsync()
    {
        this._httpClient?.Dispose();
        await this._container.StopAsync();
    }
}
