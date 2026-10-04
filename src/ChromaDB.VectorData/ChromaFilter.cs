// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using ChromaDB.Client;

namespace ChromaDB.VectorData;

/// <summary>
/// A filter translated for Chroma: a where clause on the metadata, a where_document clause on the text,
/// the ids the key must be one of, or no record at all, which needs no request.
/// </summary>
internal sealed class ChromaFilter
{
    private ChromaFilter(ChromaWhereOperator? where, ChromaWhereDocumentOperator? whereDocument, List<string>? ids, bool matchesNothing)
    {
        Where = where;
        WhereDocument = whereDocument;
        Ids = ids;
        MatchesNothing = matchesNothing;
    }

    /// <summary>A filter that matches every record.</summary>
    public static ChromaFilter All { get; } = new(where: null, whereDocument: null, ids: null, matchesNothing: false);

    /// <summary>A filter that matches no record.</summary>
    public static ChromaFilter Nothing { get; } = new(where: null, whereDocument: null, ids: null, matchesNothing: true);

    /// <summary>The where clause, or <see langword="null"/> when the metadata is not filtered.</summary>
    public ChromaWhereOperator? Where { get; }

    /// <summary>The where_document clause on the text of the records, or <see langword="null"/> when it is not filtered.</summary>
    public ChromaWhereDocumentOperator? WhereDocument { get; }

    /// <summary>The ids the records must have, or <see langword="null"/> when the key is not filtered.</summary>
    public List<string>? Ids { get; }

    /// <summary>Whether the filter matches no record.</summary>
    public bool MatchesNothing { get; }

    public static ChromaFilter Create(ChromaWhereOperator? where, ChromaWhereDocumentOperator? whereDocument, List<string>? ids)
        => ids is { Count: 0 } ? Nothing : new(where, whereDocument, ids, matchesNothing: false);
}
