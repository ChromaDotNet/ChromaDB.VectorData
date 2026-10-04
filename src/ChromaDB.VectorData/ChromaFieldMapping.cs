// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace ChromaDB.VectorData;

/// <summary>
/// Contains helper methods for mapping fields to and from Chroma metadata values.
/// </summary>
internal static class ChromaFieldMapping
{
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
            DateTimeOffset dateTimeOffsetValue => dateTimeOffsetValue.ToString("O", CultureInfo.InvariantCulture),
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
            JsonElement { ValueKind: JsonValueKind.Array } array => FromArray(array, targetType),
            JsonElement element => FromScalar(FromJsonScalar(element), targetType),
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

            // ChromaDotNet.Client reads metadata strings that look like dates as DateTime.
            (DateTime d, var t) when t == typeof(DateTime) => d,
            (DateTime d, var t) when t == typeof(DateTimeOffset) => new DateTimeOffset(d),
#if NET
            (DateTime d, var t) when t == typeof(DateOnly) => DateOnly.FromDateTime(d),
#endif
            (DateTime d, var t) when t == typeof(string) => d.ToString("O", CultureInfo.InvariantCulture),

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

    private static object FromJsonScalar(JsonElement element)
        => element.ValueKind switch
        {
            JsonValueKind.String => element.GetString()!,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when element.TryGetInt64(out var l) => l,
            JsonValueKind.Number => element.GetDouble(),
            _ => throw new InvalidOperationException($"Unsupported metadata value kind {element.ValueKind}.")
        };

    private static object FromArray(JsonElement array, Type targetType)
    {
        Type elementType;
        bool isList;

        if (targetType.IsArray)
        {
            elementType = targetType.GetElementType()!;
            isList = false;
        }
        else if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(List<>))
        {
            elementType = targetType.GenericTypeArguments[0];
            isList = true;
        }
        else
        {
            throw new InvalidOperationException($"Cannot read a metadata array into a property of type {targetType.Name}.");
        }

        var length = array.GetArrayLength();
        IList result = isList
            ? (IList)Activator.CreateInstance(targetType, length)!
            : Array.CreateInstance(elementType, length);

        var index = 0;
        foreach (var item in array.EnumerateArray())
        {
            var value = FromScalar(FromJsonScalar(item), elementType);

            if (isList)
            {
                result.Add(value);
            }
            else
            {
                result[index] = value;
            }

            index++;
        }

        Debug.Assert(index == length, "The array length does not match the number of elements.");
        return result;
    }
}
