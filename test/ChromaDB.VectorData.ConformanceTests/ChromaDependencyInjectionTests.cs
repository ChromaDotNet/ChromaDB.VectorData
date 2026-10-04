// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ChromaDB.VectorData;
using Qdrant.Client;
using VectorData.ConformanceTests;
using Xunit;

namespace ChromaDB.VectorData.ConformanceTests;

public class ChromaDependencyInjectionTests
    : DependencyInjectionTests<ChromaVectorStore, ChromaCollection<ulong, DependencyInjectionTests<ulong>.Record>, ulong, DependencyInjectionTests<ulong>.Record>
{
    private const string Host = "localhost";
    private const int Port = 8080;
    private const string ApiKey = "fakeKey";

    protected override void PopulateConfiguration(ConfigurationManager configuration, object? serviceKey = null)
        => configuration.AddInMemoryCollection(
        [
            new(CreateConfigKey("Qdrant", serviceKey, "Host"), Host),
            new(CreateConfigKey("Qdrant", serviceKey, "Port"), Port.ToString()),
            new(CreateConfigKey("Qdrant", serviceKey, "ApiKey"), ApiKey),
        ]);

    private static string HostProvider(IServiceProvider sp, object? serviceKey = null)
        => sp.GetRequiredService<IConfiguration>().GetRequiredSection(CreateConfigKey("Qdrant", serviceKey, "Host")).Value!;

    private static int PortProvider(IServiceProvider sp, object? serviceKey = null)
        => int.Parse(sp.GetRequiredService<IConfiguration>().GetRequiredSection(CreateConfigKey("Qdrant", serviceKey, "Port")).Value!);

    private static string ApiKeyProvider(IServiceProvider sp, object? serviceKey = null)
        => sp.GetRequiredService<IConfiguration>().GetRequiredSection(CreateConfigKey("Qdrant", serviceKey, "ApiKey")).Value!;

    public override IEnumerable<Func<IServiceCollection, object?, string, ServiceLifetime, IServiceCollection>> CollectionDelegates
    {
        get
        {
            yield return (services, serviceKey, name, lifetime) => serviceKey is null
                ? services
                    .AddSingleton<QdrantClient>(sp => new QdrantClient(Host, Port, apiKey: ApiKey))
                    .AddChromaCollection<ulong, Record>(name, lifetime: lifetime)
                : services
                    .AddSingleton<QdrantClient>(sp => new QdrantClient(Host, Port, apiKey: ApiKey))
                    .AddKeyedChromaCollection<ulong, Record>(serviceKey, name, lifetime: lifetime);

            yield return (services, serviceKey, name, lifetime) => serviceKey is null
                ? services.AddChromaCollection<ulong, Record>(
                    name, Host, Port, apiKey: ApiKey, lifetime: lifetime)
                : services.AddKeyedChromaCollection<ulong, Record>(
                    serviceKey, name, Host, Port, apiKey: ApiKey, lifetime: lifetime);

            yield return (services, serviceKey, name, lifetime) => serviceKey is null
                ? services.AddChromaCollection<ulong, Record>(
                    name, sp => new QdrantClient(HostProvider(sp), PortProvider(sp), apiKey: ApiKeyProvider(sp)), lifetime: lifetime)
                : services.AddKeyedChromaCollection<ulong, Record>(
                    serviceKey, name, sp => new QdrantClient(HostProvider(sp, serviceKey), PortProvider(sp, serviceKey), apiKey: ApiKeyProvider(sp, serviceKey)), lifetime: lifetime);
        }
    }

    public override IEnumerable<Func<IServiceCollection, object?, ServiceLifetime, IServiceCollection>> StoreDelegates
    {
        get
        {
            yield return (services, serviceKey, lifetime) => serviceKey is null
                ? services.AddChromaVectorStore(
                    Host, Port, apiKey: ApiKey, lifetime: lifetime)
                : services.AddKeyedChromaVectorStore(
                    serviceKey, Host, Port, apiKey: ApiKey, lifetime: lifetime);

            yield return (services, serviceKey, lifetime) => serviceKey is null
                ? services
                    .AddSingleton<QdrantClient>(sp => new QdrantClient(Host, Port, apiKey: ApiKey))
                    .AddChromaVectorStore(lifetime: lifetime)
                : services
                    .AddSingleton<QdrantClient>(sp => new QdrantClient(Host, Port, apiKey: ApiKey))
                    .AddKeyedChromaVectorStore(serviceKey, lifetime: lifetime);
        }
    }

    [Fact]
    public void HostCantBeNullOrEmpty()
    {
        IServiceCollection services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() => services.AddChromaVectorStore(host: null!));
        Assert.Throws<ArgumentNullException>(() => services.AddKeyedChromaVectorStore(serviceKey: "notNull", host: null!));
        Assert.Throws<ArgumentNullException>(() => services.AddChromaCollection<ulong, Record>(
            name: "notNull", host: null!));
        Assert.Throws<ArgumentException>(() => services.AddChromaCollection<ulong, Record>(
            name: "notNull", host: ""));
        Assert.Throws<ArgumentNullException>(() => services.AddKeyedChromaCollection<ulong, Record>(
            serviceKey: "notNull", name: "notNull", host: null!));
        Assert.Throws<ArgumentException>(() => services.AddKeyedChromaCollection<ulong, Record>(
            serviceKey: "notNull", name: "notNull", host: ""));
    }
}
