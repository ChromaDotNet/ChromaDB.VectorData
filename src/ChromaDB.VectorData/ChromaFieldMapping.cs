// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.VectorData.ProviderServices;

namespace ChromaDB.VectorData;

/// <summary>
/// Contains helper methods for mapping fields to and from Chroma metadata values.
/// </summary>
internal static class ChromaFieldMapping
{
    /// <summary>
    /// Get the data property whose value is also the Chroma document of a record, the text that Chroma searches with
    /// <c>where_document</c> and that other Chroma clients store: the only full-text indexed string property, or
    /// <see langword="null"/> when there is none or more than one, as a record has one document.
    /// </summary>
    public static DataPropertyModel? GetDocumentProperty(CollectionModel model)
    {
        var fullTextProperties = model.DataProperties.Where(p => p.IsFullTextIndexed && p.Type == typeof(string)).Take(2).ToList();
        return fullTextProperties.Count == 1 ? fullTextProperties[0] : null;
    }

    /// <summary>
    /// Convert the given key to a Chroma record id.
    /// </summary>
    public static string ToId(object key)
        => key switch
        {
            string id => id,
            Guid id => id.ToString("D"),
            _ => throw new NotSupportedException($"The provided key type '{key.GetType().Name}' is not supported by Chroma.")
        };

    /// <summary>
    /// Convert the given <paramref name="sourceValue"/> to a value that can be stored in Chroma metadata.
    /// </summary>
    /// <param name="sourceValue">The object to convert.</param>
    /// <returns>The converted value, or <see langword="null"/> when there is nothing to store, since Chroma metadata has no null values.</returns>
    /// <exception cref="InvalidOperationException">Thrown when an unsupported type is encountered.</exception>
    public static object? ToMetadataValue(object? sourceValue)
        => sourceValue switch
        {
            null => null,
            int or long or float or double or bool or string => sourceValue,
            // In UTC, so that equal instants are equal strings, as == compares them in C#.
            DateTimeOffset dateTimeOffsetValue => dateTimeOffsetValue.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            DateTime dateTimeValue => dateTimeValue.ToString("O", CultureInfo.InvariantCulture),
#if NET
            DateOnly dateOnlyValue => dateOnlyValue.ToString("O", CultureInfo.InvariantCulture),
#endif
            IEnumerable<int> or
                IEnumerable<long> or
                IEnumerable<string> or
                IEnumerable<float> or
                IEnumerable<double> or
                IEnumerable<bool> or
                IEnumerable<DateTime> or
                IEnumerable<DateTimeOffset>
#if NET
                or IEnumerable<DateOnly>
#endif
                => ((IEnumerable)sourceValue).Cast<object?>().Select(ToMetadataValue).ToList(),

            _ => throw new InvalidOperationException($"Unsupported source value type {sourceValue.GetType().FullName}.")
        };

    /// <summary>
    /// Convert the given Chroma metadata value to the given type.
    /// </summary>
    /// <param name="metadataValue">The value read from Chroma metadata.</param>
    /// <param name="targetType">The type of the property the value is read into.</param>
    /// <returns>The converted value.</returns>
    /// <exception cref="InvalidOperationException">Thrown when an unsupported type is encountered.</exception>
    public static object? FromMetadataValue(object? metadataValue, Type targetType)
    {
        if (Nullable.GetUnderlyingType(targetType) is Type unwrapped)
        {
            targetType = unwrapped;
        }

        return metadataValue switch
        {
            null => null,
            IList list => FromList(list, targetType),
            _ => FromScalar(metadataValue, targetType),
        };
    }

    private static object FromScalar(object value, Type targetType)
        => (value, targetType) switch
        {
            (string s, var t) when t == typeof(string) => s,
            (string s, var t) when t == typeof(DateTimeOffset) => DateTimeOffset.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            (string s, var t) when t == typeof(DateTime) => DateTime.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
#if NET
            (string s, var t) when t == typeof(DateOnly) => DateOnly.Parse(s, CultureInfo.InvariantCulture),
#endif

            (bool b, _) => b,

            (long l, var t) when t == typeof(int) => checked((int)l),
            (long l, var t) when t == typeof(long) => l,
            (long l, var t) when t == typeof(double) => (double)l,
            (long l, var t) when t == typeof(float) => (float)l,
            (double d, var t) when t == typeof(double) => d,
            (double d, var t) when t == typeof(float) => (float)d,
            (int i, var t) when t == typeof(int) => i,
            (int i, var t) when t == typeof(long) => (long)i,

            _ => throw new InvalidOperationException($"Cannot read the metadata value of type {value.GetType().Name} into a property of type {targetType.Name}.")
        };

    private static object FromList(IList list, Type targetType)
    {
        var values = list.Cast<object>();

        return targetType switch
        {
            Type t when t == typeof(List<string>) => Convert<string>(values).ToList(),
            Type t when t == typeof(string[]) => Convert<string>(values).ToArray(),
            Type t when t == typeof(List<int>) => Convert<int>(values).ToList(),
            Type t when t == typeof(int[]) => Convert<int>(values).ToArray(),
            Type t when t == typeof(List<long>) => Convert<long>(values).ToList(),
            Type t when t == typeof(long[]) => Convert<long>(values).ToArray(),
            Type t when t == typeof(List<double>) => Convert<double>(values).ToList(),
            Type t when t == typeof(double[]) => Convert<double>(values).ToArray(),
            Type t when t == typeof(List<float>) => Convert<float>(values).ToList(),
            Type t when t == typeof(float[]) => Convert<float>(values).ToArray(),
            Type t when t == typeof(List<bool>) => Convert<bool>(values).ToList(),
            Type t when t == typeof(bool[]) => Convert<bool>(values).ToArray(),
            Type t when t == typeof(List<DateTime>) => Convert<DateTime>(values).ToList(),
            Type t when t == typeof(DateTime[]) => Convert<DateTime>(values).ToArray(),
            Type t when t == typeof(List<DateTimeOffset>) => Convert<DateTimeOffset>(values).ToList(),
            Type t when t == typeof(DateTimeOffset[]) => Convert<DateTimeOffset>(values).ToArray(),
#if NET
            Type t when t == typeof(List<DateOnly>) => Convert<DateOnly>(values).ToList(),
            Type t when t == typeof(DateOnly[]) => Convert<DateOnly>(values).ToArray(),
#endif

            _ => throw new UnreachableException($"Unsupported collection type {targetType.Name}"),
        };

        static IEnumerable<T> Convert<T>(IEnumerable<object> values)
            => values.Select(value => (T)FromScalar(value, typeof(T)));
    }
}
