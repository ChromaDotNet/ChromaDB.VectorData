// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.VectorData;

namespace ChromaDB.VectorData.UnitTests;

public sealed class Hotel<TKey>
{
    [VectorStoreKey]
    public TKey HotelId { get; set; } = default!;

    [VectorStoreData]
    public string? HotelName { get; set; }

    [VectorStoreData]
    public int? Rating { get; set; }

    [VectorStoreData]
    public double Price { get; set; }

    [VectorStoreData]
    public bool Parking { get; set; }

    [VectorStoreData]
    public List<string>? Tags { get; set; }

    [VectorStoreVector(4)]
    public ReadOnlyMemory<float>? Embedding { get; set; }
}

public sealed class DotProductHotel
{
    [VectorStoreKey]
    public string HotelId { get; set; } = default!;

    [VectorStoreVector(4, DistanceFunction = DistanceFunction.DotProductSimilarity)]
    public ReadOnlyMemory<float>? Embedding { get; set; }
}

public sealed class FullTextHotel
{
    [VectorStoreKey]
    public string HotelId { get; set; } = default!;

    [VectorStoreData(IsFullTextIndexed = true)]
    public string? Description { get; set; }

    [VectorStoreVector(4)]
    public ReadOnlyMemory<float>? Embedding { get; set; }
}
