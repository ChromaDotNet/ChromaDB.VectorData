// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData.ProviderServices;

namespace ChromaDB.VectorData;

/// <summary>
/// A record as Chroma stores it: an id, an embedding and metadata.
/// </summary>
internal readonly record struct ChromaStorageRecord(string Id, ReadOnlyMemory<float> Embedding, Dictionary<string, object>? Metadata, string? Document);

/// <summary>
/// Mapper between a Chroma record and the consumer data model.
/// </summary>
/// <typeparam name="TRecord">The consumer data model to map to or from.</typeparam>
internal sealed class ChromaMapper<TRecord>(CollectionModel model)
    where TRecord : class
{
    private readonly DataPropertyModel? _documentProperty = ChromaFieldMapping.GetDocumentProperty(model);

    /// <summary>Gets a value indicating whether the records have a Chroma document, from the full-text property.</summary>
    public bool HasDocument => _documentProperty is not null;

    public ChromaStorageRecord MapFromDataToStorageModel(TRecord dataModel, int recordIndex, GeneratedEmbeddings<Embedding<float>>?[]? generatedEmbeddings)
    {
        var keyProperty = model.KeyProperty;
        var key = keyProperty.GetValueAsObject(dataModel)
            ?? throw new InvalidOperationException($"Missing key property '{keyProperty.ModelName}' on provided record of type '{typeof(TRecord).Name}'.");

        // Chroma metadata has no null values, and Chroma drops empty lists: neither is stored, and both come back as null.
        Dictionary<string, object>? metadata = null;
        foreach (var property in model.DataProperties)
        {
            if (ChromaFieldMapping.ToMetadataValue(property.GetValueAsObject(dataModel)) is { } value and not System.Collections.ICollection { Count: 0 })
            {
                (metadata ??= []).Add(property.StorageName, value);
            }
        }

        // There is exactly one vector property, as verified by the model builder.
        Debug.Assert(
            generatedEmbeddings is null || generatedEmbeddings.Length == 1 && generatedEmbeddings[0] is not null,
            "There should be exactly one generated embedding, for the single vector property.");
        var embedding = GetVector(
            model.VectorProperty,
            generatedEmbeddings is null
                ? model.VectorProperty.GetValueAsObject(dataModel)
                : generatedEmbeddings[0]![recordIndex]);

        // The full-text property stays in the metadata too, for the filters on it.
        var document = _documentProperty?.GetValueAsObject(dataModel) as string;

        return new ChromaStorageRecord(ChromaFieldMapping.ToId(key), embedding, metadata, document);

        static ReadOnlyMemory<float> GetVector(PropertyModel property, object? embedding)
            => embedding switch
            {
                ReadOnlyMemory<float> m => m,
                Embedding<float> e => e.Vector,
                float[] a => a,

                null => throw new InvalidOperationException($"Vector property '{property.ModelName}' on provided record of type '{typeof(TRecord).Name}' may not be null."),
                var unknownEmbedding => throw new InvalidOperationException($"Vector property '{property.ModelName}' on provided record of type '{typeof(TRecord).Name}' has unsupported embedding type '{unknownEmbedding.GetType().Name}'.")
            };
    }

    public TRecord MapFromStorageToDataModel(string id, ReadOnlyMemory<float>? embedding, IReadOnlyDictionary<string, object>? metadata, string? document, bool includeVectors)
    {
        var outputRecord = model.CreateRecord<TRecord>()!;

        model.KeyProperty.SetValueAsObject(
            outputRecord,
            (Nullable.GetUnderlyingType(model.KeyProperty.Type) ?? model.KeyProperty.Type) == typeof(Guid) ? Guid.Parse(id) : id);

        if (includeVectors && embedding is { } vector)
        {
            var property = model.VectorProperty;
            property.SetValueAsObject(
                outputRecord,
                (Nullable.GetUnderlyingType(property.Type) ?? property.Type) switch
                {
                    var t when t == typeof(ReadOnlyMemory<float>) => vector,
                    var t when t == typeof(Embedding<float>) => new Embedding<float>(vector),
                    var t when t == typeof(float[]) => vector.ToArray(),

                    _ => throw new UnreachableException()
                });
        }

        foreach (var dataProperty in model.DataProperties)
        {
            if (metadata is not null && metadata.TryGetValue(dataProperty.StorageName, out var value))
            {
                dataProperty.SetValueAsObject(outputRecord, ChromaFieldMapping.FromMetadataValue(value, dataProperty.Type));
            }
            else if (dataProperty == _documentProperty && document is not null)
            {
                // A record written by another Chroma client has its text in the document only.
                dataProperty.SetValueAsObject(outputRecord, document);
            }
            else if (!dataProperty.Type.IsValueType || Nullable.GetUnderlyingType(dataProperty.Type) is not null)
            {
                // A null value is not stored, since Chroma metadata has no null values; a missing one is null.
                dataProperty.SetValueAsObject(outputRecord, null);
            }
        }

        return outputRecord;
    }
}
