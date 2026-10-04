// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Linq.Expressions;
using ChromaDB.Client;
using Microsoft.Extensions.VectorData.ProviderServices;
using Microsoft.Extensions.VectorData.ProviderServices.Filter;

namespace ChromaDB.VectorData;

// https://docs.trychroma.com/docs/querying-collections/metadata-filtering
internal class ChromaFilterTranslator : FilterTranslatorBase
{
    /// <summary>
    /// Translate the filter to a Chroma where operator, or to <see langword="null"/> when it matches every record.
    /// </summary>
    internal ChromaWhereOperator? Translate(LambdaExpression lambdaExpression, CollectionModel model)
    {
        var preprocessedExpression = PreprocessFilter(lambdaExpression, model, new FilterPreprocessingOptions());

        return Translate(preprocessedExpression, negated: false);
    }

    // Chroma has no $not, so a negation is pushed down to the comparisons it contains.
    private ChromaWhereOperator? Translate(Expression? node, bool negated)
        => node switch
        {
            BinaryExpression { NodeType: ExpressionType.Equal } equal => TranslateEqual(equal.Left, equal.Right, negated),
            BinaryExpression { NodeType: ExpressionType.NotEqual } notEqual => TranslateEqual(notEqual.Left, notEqual.Right, !negated),

            BinaryExpression
            {
                NodeType: ExpressionType.GreaterThan or ExpressionType.GreaterThanOrEqual or ExpressionType.LessThan or ExpressionType.LessThanOrEqual
            } comparison
                => TranslateComparison(comparison, negated),

            // !(a && b) is !a || !b, and !(a || b) is !a && !b.
            BinaryExpression { NodeType: ExpressionType.AndAlso } andAlso
                => negated
                    ? Or(Translate(andAlso.Left, negated: true), Translate(andAlso.Right, negated: true))
                    : And(Translate(andAlso.Left, negated: false), Translate(andAlso.Right, negated: false)),
            BinaryExpression { NodeType: ExpressionType.OrElse } orElse
                => negated
                    ? And(Translate(orElse.Left, negated: true), Translate(orElse.Right, negated: true))
                    : Or(Translate(orElse.Left, negated: false), Translate(orElse.Right, negated: false)),

            UnaryExpression { NodeType: ExpressionType.Not } not => Translate(not.Operand, !negated),

            // Handle converting non-nullable to nullable; such nodes are found in e.g. r => r.Int == nullableInt
            UnaryExpression { NodeType: ExpressionType.Convert } convert when Nullable.GetUnderlyingType(convert.Type) == convert.Operand.Type
                => Translate(convert.Operand, negated),

            // Special handling for bool constant as the filter expression (r => r.Bool)
            Expression when node.Type == typeof(bool) && TryBindProperty(node, out var property)
                => ChromaWhereOperator.Equal(property.StorageName, !negated),

            // Handle true literal (r => true), which is useful for fetching all records
            ConstantExpression { Value: true } when !negated => null,

            MethodCallExpression methodCall => TranslateMethodCall(methodCall, negated),

            _ => throw new NotSupportedException("Chroma does not support the following NodeType in filters: " + node?.NodeType)
        };

    private ChromaWhereOperator TranslateEqual(Expression left, Expression right, bool negated)
        => TryBindProperty(left, out var property) && right is ConstantExpression { Value: var rightConstant }
            ? GenerateEqual(property.StorageName, rightConstant, negated)
            : TryBindProperty(right, out property) && left is ConstantExpression { Value: var leftConstant }
                ? GenerateEqual(property.StorageName, leftConstant, negated)
                : throw new NotSupportedException("Invalid equality/comparison");

    private static ChromaWhereOperator GenerateEqual(string propertyStorageName, object? value, bool negated)
    {
        var metadataValue = ToFilterValue(value);

        return negated
            ? ChromaWhereOperator.NotEqual(propertyStorageName, metadataValue)
            : ChromaWhereOperator.Equal(propertyStorageName, metadataValue);
    }

    private ChromaWhereOperator TranslateComparison(BinaryExpression comparison, bool negated)
    {
        // Normalize to property-on-the-left.
        var (property, value, nodeType) =
            TryBindProperty(comparison.Left, out var leftProperty) && comparison.Right is ConstantExpression { Value: var rightValue }
                ? (leftProperty, rightValue, comparison.NodeType)
                : TryBindProperty(comparison.Right, out var rightProperty) && comparison.Left is ConstantExpression { Value: var leftValue }
                    ? (rightProperty, leftValue, Flip(comparison.NodeType))
                    : throw new NotSupportedException("Comparison expression not supported by Chroma");

        if (value is not (int or long or float or double))
        {
            throw new NotSupportedException($"Chroma supports comparisons on numbers only, not on '{value?.GetType().Name ?? "null"}'.");
        }

        if (negated)
        {
            nodeType = Negate(nodeType);
        }

        return nodeType switch
        {
            ExpressionType.GreaterThan => ChromaWhereOperator.GreaterThan(property.StorageName, value),
            ExpressionType.GreaterThanOrEqual => ChromaWhereOperator.GreaterThanOrEqual(property.StorageName, value),
            ExpressionType.LessThan => ChromaWhereOperator.LessThan(property.StorageName, value),
            ExpressionType.LessThanOrEqual => ChromaWhereOperator.LessThanOrEqual(property.StorageName, value),
            _ => throw new InvalidOperationException("Unreachable")
        };

        static ExpressionType Flip(ExpressionType nodeType)
            => nodeType switch
            {
                ExpressionType.GreaterThan => ExpressionType.LessThan,
                ExpressionType.GreaterThanOrEqual => ExpressionType.LessThanOrEqual,
                ExpressionType.LessThan => ExpressionType.GreaterThan,
                ExpressionType.LessThanOrEqual => ExpressionType.GreaterThanOrEqual,
                _ => throw new InvalidOperationException("Unreachable")
            };

        static ExpressionType Negate(ExpressionType nodeType)
            => nodeType switch
            {
                ExpressionType.GreaterThan => ExpressionType.LessThanOrEqual,
                ExpressionType.GreaterThanOrEqual => ExpressionType.LessThan,
                ExpressionType.LessThan => ExpressionType.GreaterThanOrEqual,
                ExpressionType.LessThanOrEqual => ExpressionType.GreaterThan,
                _ => throw new InvalidOperationException("Unreachable")
            };
    }

    private static ChromaWhereOperator? And(ChromaWhereOperator? left, ChromaWhereOperator? right)
        => left is null ? right : right is null ? left : left & right;

    // A side that matches every record makes the whole OR match every record.
    private static ChromaWhereOperator? Or(ChromaWhereOperator? left, ChromaWhereOperator? right)
        => left is null || right is null ? null : left | right;

    private ChromaWhereOperator TranslateMethodCall(MethodCallExpression methodCall, bool negated)
        => methodCall switch
        {
            // Enumerable.Contains(), List.Contains(), MemoryExtensions.Contains()
            _ when TryMatchContains(methodCall, out var source, out var item)
                => TranslateContains(source, item, negated),

            _ => throw new NotSupportedException($"Unsupported method call: {methodCall.Method.DeclaringType?.Name}.{methodCall.Method.Name}")
        };

    private ChromaWhereOperator TranslateContains(Expression source, Expression item, bool negated)
    {
        switch (source)
        {
            // Contains over field enumerable
            case var _ when TryBindProperty(source, out _):
                throw new NotSupportedException("Filtering on whether an array property contains a value is not supported yet.");

            // Contains over inline enumerable
            case NewArrayExpression newArray:
                var elements = new object?[newArray.Expressions.Count];

                for (var i = 0; i < newArray.Expressions.Count; i++)
                {
                    if (newArray.Expressions[i] is not ConstantExpression { Value: var elementValue })
                    {
                        throw new NotSupportedException("Inline array elements must be constants");
                    }

                    elements[i] = elementValue;
                }

                return ProcessInlineEnumerable(elements, item);

            case ConstantExpression { Value: IEnumerable enumerable and not string }:
                return ProcessInlineEnumerable(enumerable, item);

            default:
                throw new NotSupportedException("Unsupported Contains");
        }

        ChromaWhereOperator ProcessInlineEnumerable(IEnumerable elements, Expression item)
        {
            if (!TryBindProperty(item, out var property))
            {
                throw new NotSupportedException("Unsupported item type in Contains");
            }

            var values = elements.Cast<object?>().Select(ToFilterValue).ToArray();

            return negated
                ? ChromaWhereOperator.NotIn(property.StorageName, values)
                : ChromaWhereOperator.In(property.StorageName, values);
        }
    }

    private static object ToFilterValue(object? value)
        => ChromaFieldMapping.ToMetadataValue(value)
            ?? throw new NotSupportedException("Chroma does not support filtering on null values.");
}
