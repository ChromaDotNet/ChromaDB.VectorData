// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using ChromaDB.Client;
using ChromaDB.VectorData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.VectorData;

// The dynamic collection is the way to use the provider with NativeAOT: it takes the schema as a definition,
// without reflection over a record type.
var uri = Environment.GetEnvironmentVariable("CHROMA_URI") ?? "http://localhost:8000";
var failures = 0;

void Check(string name, bool passed)
{
    Console.WriteLine($"{(passed ? "Passed" : "Failed")}: {name}");
    failures += passed ? 0 : 1;
}

var definition = new VectorStoreCollectionDefinition
{
    Properties =
    [
        new VectorStoreKeyProperty("Key", typeof(string)),
        new VectorStoreDataProperty("Text", typeof(string)),
        new VectorStoreDataProperty("Opened", typeof(DateTimeOffset)),
        new VectorStoreDataProperty("Rating", typeof(int)),
        new VectorStoreDataProperty("Tags", typeof(string[])),
        new VectorStoreVectorProperty("Embedding", typeof(ReadOnlyMemory<float>), 4),
    ]
};

using var httpClient = new HttpClient();
using var vectorStore = new ChromaVectorStore(new ChromaClient(new ChromaConfigurationOptions(uri), httpClient), ownsClient: false);
using var collection = vectorStore.GetDynamicCollection("native-aot", definition);

await collection.EnsureCollectionDeletedAsync();
await collection.EnsureCollectionExistsAsync();

try
{
    var opened = new DateTimeOffset(2026, 10, 4, 12, 30, 0, TimeSpan.FromHours(2));
    await collection.UpsertAsync(
    [
        new Dictionary<string, object?> { ["Key"] = "a", ["Text"] = "2026-10-04", ["Opened"] = opened, ["Rating"] = 5, ["Tags"] = new[] { "spa", "pool" }, ["Embedding"] = new ReadOnlyMemory<float>([1, 0, 0, 0]) },
        new Dictionary<string, object?> { ["Key"] = "b", ["Text"] = "b", ["Opened"] = opened, ["Rating"] = 2, ["Tags"] = new[] { "wifi" }, ["Embedding"] = new ReadOnlyMemory<float>([0, 1, 0, 0]) },
    ]);

    var a = await collection.GetAsync("a");
    Check("a string that looks like a date comes back as written", a?["Text"] is "2026-10-04");
    Check("a DateTimeOffset comes back as the same instant", a?["Opened"] is DateTimeOffset o && o == opened);
    Check("an array comes back", a?["Tags"] is string[] tags && tags.SequenceEqual(["spa", "pool"]));

    var results = await collection.SearchAsync(new ReadOnlyMemory<float>([1, 0, 0, 0]), top: 2).ToListAsync();
    Check("search returns the nearest record first", results.Count == 2 && results[0].Record["Key"] is "a");

    var filtered = await collection.GetAsync(r => (int)r["Rating"]! >= 4, top: 10).ToListAsync();
    Check("a filter selects the records", filtered.Count == 1 && filtered[0]["Key"] is "a");

    // The vector store registered with dependency injection, without reflection too: its collections are dynamic.
    var services = new ServiceCollection();
    services.AddChromaVectorStore(uri);
    await using var serviceProvider = services.BuildServiceProvider();
    using var registered = serviceProvider.GetRequiredService<VectorStore>().GetDynamicCollection("native-aot", definition);
    Check("the vector store of dependency injection reads the records", await registered.GetAsync("a") is { } fromServices && fromServices["Key"] is "a");
}
finally
{
    await collection.EnsureCollectionDeletedAsync();
}

Console.WriteLine(failures == 0 ? "All checks passed." : $"{failures} checks failed.");
return failures == 0 ? 0 : 1;
