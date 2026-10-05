// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using ChromaDB.Client;
using ChromaDB.Client.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VectorData.ConformanceTests;
using Xunit;

namespace ChromaDB.VectorData.ConformanceTests;

public class ChromaDependencyInjectionTests
    : DependencyInjectionTests<ChromaVectorStore, ChromaCollection<string, DependencyInjectionTests<string>.Record>, string, DependencyInjectionTests<string>.Record>
{
    private const string Uri = "http://localhost:8000";

    protected override void PopulateConfiguration(ConfigurationManager configuration, object? serviceKey = null)
        => configuration.AddInMemoryCollection(
        [
            new(CreateConfigKey("Chroma", serviceKey, "Uri"), Uri),
        ]);

    private static string UriProvider(IServiceProvider sp, object? serviceKey = null)
        => sp.GetRequiredService<IConfiguration>().GetRequiredSection(CreateConfigKey("Chroma", serviceKey, "Uri")).Value!;

    // The ChromaClient of ChromaDotNet.Client.DependencyInjection, a singleton with an HttpClient from IHttpClientFactory.
    private static IServiceCollection AddClient(IServiceCollection services)
    {
        services.AddChromaClient(_ => new ChromaConfigurationOptions(Uri));
        return services;
    }

    public override IEnumerable<Func<IServiceCollection, object?, string, ServiceLifetime, IServiceCollection>> CollectionDelegates
    {
        get
        {
            yield return (services, serviceKey, name, lifetime) => serviceKey is null
                ? AddClient(services)
                    .AddChromaCollection<string, Record>(name, lifetime: lifetime)
                : AddClient(services)
                    .AddKeyedChromaCollection<string, Record>(serviceKey, name, lifetime: lifetime);

            yield return (services, serviceKey, name, lifetime) => serviceKey is null
                ? services.AddChromaCollection<string, Record>(name, Uri, lifetime: lifetime)
                : services.AddKeyedChromaCollection<string, Record>(serviceKey, name, Uri, lifetime: lifetime);

            yield return (services, serviceKey, name, lifetime) => serviceKey is null
                ? services.AddChromaCollection<string, Record>(name, new ChromaConfigurationOptions(Uri).WithBatchSplitting(300), lifetime: lifetime)
                : services.AddKeyedChromaCollection<string, Record>(serviceKey, name, new ChromaConfigurationOptions(Uri).WithBatchSplitting(300), lifetime: lifetime);

            yield return (services, serviceKey, name, lifetime) => serviceKey is null
                ? services.AddChromaCollection<string, Record>(
                    name, sp => new ChromaClient(new ChromaConfigurationOptions(UriProvider(sp)), new HttpClient()), lifetime: lifetime)
                : services.AddKeyedChromaCollection<string, Record>(
                    serviceKey, name, sp => new ChromaClient(new ChromaConfigurationOptions(UriProvider(sp, serviceKey)), new HttpClient()), lifetime: lifetime);
        }
    }

    public override IEnumerable<Func<IServiceCollection, object?, ServiceLifetime, IServiceCollection>> StoreDelegates
    {
        get
        {
            yield return (services, serviceKey, lifetime) => serviceKey is null
                ? services.AddChromaVectorStore(Uri, lifetime: lifetime)
                : services.AddKeyedChromaVectorStore(serviceKey, Uri, lifetime: lifetime);

            yield return (services, serviceKey, lifetime) => serviceKey is null
                ? services.AddChromaVectorStore(new ChromaConfigurationOptions(Uri).WithBatchSplitting(300), lifetime: lifetime)
                : services.AddKeyedChromaVectorStore(serviceKey, new ChromaConfigurationOptions(Uri).WithBatchSplitting(300), lifetime: lifetime);

            yield return (services, serviceKey, lifetime) => serviceKey is null
                ? AddClient(services)
                    .AddChromaVectorStore(lifetime: lifetime)
                : AddClient(services)
                    .AddKeyedChromaVectorStore(serviceKey, lifetime: lifetime);

            yield return (services, serviceKey, lifetime) => serviceKey is null
                ? services.AddChromaVectorStore(
                    sp => new ChromaClient(new ChromaConfigurationOptions(UriProvider(sp)), new HttpClient()), lifetime: lifetime)
                : services.AddKeyedChromaVectorStore(
                    serviceKey, sp => new ChromaClient(new ChromaConfigurationOptions(UriProvider(sp, serviceKey)), new HttpClient()), lifetime: lifetime);
        }
    }

    [Fact]
    public void ChromaOptionsReachTheClient()
    {
        IServiceCollection services = new ServiceCollection();
        var chromaOptions = new ChromaConfigurationOptions(Uri, tenant: "tenant1", database: "database1");
        services.AddChromaVectorStore(chromaOptions);
        services.AddChromaCollection<string, Record>("collection1", chromaOptions);

        using var serviceProvider = services.BuildServiceProvider();
        var store = serviceProvider.GetRequiredService<ChromaVectorStore>();
        var collection = serviceProvider.GetRequiredService<ChromaCollection<string, Record>>();

        foreach (var client in new[] { (ChromaClient)store.GetService(typeof(ChromaClient))!, (ChromaClient)collection.GetService(typeof(ChromaClient))! })
        {
            Assert.Equal("tenant1", client.Options.Tenant);
            Assert.Equal("database1", client.Options.Database);
        }
    }

    [Fact]
    public void ChromaOptionsCantBeNull()
    {
        IServiceCollection services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() => services.AddChromaVectorStore(chromaOptions: null!));
        Assert.Throws<ArgumentNullException>(() => services.AddKeyedChromaVectorStore(serviceKey: "notNull", chromaOptions: null!));
        Assert.Throws<ArgumentNullException>(() => services.AddChromaCollection<string, Record>(name: "notNull", chromaOptions: null!));
        Assert.Throws<ArgumentNullException>(() => services.AddKeyedChromaCollection<string, Record>(serviceKey: "notNull", name: "notNull", chromaOptions: null!));
    }

    [Fact]
    public void UriCantBeNullOrEmpty()
    {
        IServiceCollection services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() => services.AddChromaVectorStore(uri: null!));
        Assert.Throws<ArgumentNullException>(() => services.AddKeyedChromaVectorStore(serviceKey: "notNull", uri: null!));
        Assert.Throws<ArgumentNullException>(() => services.AddChromaCollection<string, Record>(
            name: "notNull", uri: null!));
        Assert.Throws<ArgumentException>(() => services.AddChromaCollection<string, Record>(
            name: "notNull", uri: ""));
        Assert.Throws<ArgumentNullException>(() => services.AddKeyedChromaCollection<string, Record>(
            serviceKey: "notNull", name: "notNull", uri: null!));
        Assert.Throws<ArgumentException>(() => services.AddKeyedChromaCollection<string, Record>(
            serviceKey: "notNull", name: "notNull", uri: ""));
    }
}
