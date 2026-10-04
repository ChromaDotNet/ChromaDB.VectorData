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
    private const string TextFilterNotSupported
        = "Chroma filters text with Contains only on the single full-text indexed string property, which is stored as the document, joined to the other conditions with &&.";

    private DataPropertyModel? _documentProperty;

    private const string KeyFilterNotSupported
        = "Chroma filters on the key only with == or Contains over a list of keys, joined to the other conditions with &&.";

    /// <summary>
    /// Translate the filter to the where clause and the ids of a Chroma request.
    /// </summary>
    internal ChromaFilter Translate(LambdaExpression lambdaExpression, CollectionModel model)
    {
        var preprocessedExpression = PreprocessFilter(lambdaExpression, model, new FilterPreprocessingOptions());
        _documentProperty = ChromaFieldMapping.GetDocumentProperty(model);

        // The key of a record is its Chroma id, not a metadata field: conditions on it become the ids of the request.
        // Chroma joins the where clause and the where_document clause with AND only, so text conditions are conjuncts too.
        List<string>? ids = null;
        ChromaWhereDocumentOperator? whereDocument = null;
        var condition = Condition.All;
        foreach (var conjunct in GetConjuncts(preprocessedExpression))
        {
            if (TryTranslateKeyCondition(conjunct, out var keys))
            {
                ids = ids is null ? keys : ids.Intersect(keys).ToList();
            }
            else if (TryTranslateTextCondition(conjunct, out var textCondition))
            {
                whereDocument = whereDocument is null ? textCondition : whereDocument & textCondition;
            }
            else
            {
                condition = And(condition, Translate(conjunct, negated: false));
            }
        }

        return condition.MatchesNothing ? ChromaFilter.Nothing : ChromaFilter.Create(condition.Where, whereDocument, ids);
    }

    private static IEnumerable<Expression> GetConjuncts(Expression expression)
        => expression is BinaryExpression { NodeType: ExpressionType.AndAlso } andAlso
            ? GetConjuncts(andAlso.Left).Concat(GetConjuncts(andAlso.Right))
            : [expression];

    // r.Key == "a", "a" == r.Key, or a list of keys that contains r.Key.
    private bool TryTranslateKeyCondition(Expression expression, out List<string> ids)
    {
        switch (expression)
        {
            case BinaryExpression { NodeType: ExpressionType.Equal } equal
                when TryBindKey(equal.Left) && TryGetConstant(equal.Right, out var value)
                    || TryBindKey(equal.Right) && TryGetConstant(equal.Left, out value):
                ids = [ToId(value)];
                return true;

            case MethodCallExpression methodCall
                when TryMatchContains(methodCall, out var source, out var item) && TryBindKey(item):
                IEnumerable keys = source switch
                {
                    NewArrayExpression newArray => GetInlineArrayElements(newArray),
                    ConstantExpression { Value: IEnumerable enumerable and not string } => enumerable,
                    _ => throw new NotSupportedException(KeyFilterNotSupported)
                };
                ids = keys.Cast<object?>().Select(ToId).Distinct().ToList();
                return true;

            default:
                ids = [];
                return false;
        }

        static string ToId(object? key)
            => key is null
                ? throw new NotSupportedException("Chroma does not support filtering on a null key.")
                : ChromaFieldMapping.ToId(key);
    }

    // r.Text.Contains("word") or !r.Text.Contains("word"), on the property stored as the document.
    private bool TryTranslateTextCondition(Expression expression, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out ChromaWhereDocumentOperator? whereDocument)
    {
        var negated = false;
        if (expression is UnaryExpression { NodeType: ExpressionType.Not } not)
        {
            negated = true;
            expression = not.Operand;
        }

        if (_documentProperty is null
            || !IsStringContains(expression, out var target, out var argument)
            || !TryBindProperty(target, out var property)
            || property != _documentProperty)
        {
            whereDocument = null;
            return false;
        }

        if (!TryGetConstant(argument, out var value) || value is not string text)
        {
            throw new NotSupportedException("Chroma filters the text of the document with Contains over a constant string.");
        }

        whereDocument = negated ? ChromaWhereDocumentOperator.NotContains(text) : ChromaWhereDocumentOperator.Contains(text);
        return true;
    }

    private static bool IsStringContains(Expression expression, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Expression? target, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Expression? argument)
    {
        if (expression is MethodCallExpression { Method.Name: nameof(string.Contains), Object: { } instance, Arguments: [var single] } call
            && call.Method.DeclaringType == typeof(string)
            && single.Type == typeof(string))
        {
            target = instance;
            argument = single;
            return true;
        }

        target = null;
        argument = null;
        return false;
    }

    private bool TryBindKey(Expression expression)
        => TryBindProperty(expression, out var property) && property is KeyPropertyModel;

    // Any other condition on the key, like != or inside ||, cannot be expressed with the ids of a request.
    private bool TryBindDataProperty(Expression expression, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out PropertyModel? property)
    {
        if (!TryBindProperty(expression, out var bound))
        {
            property = null;
            return false;
        }

        if (bound is KeyPropertyModel)
        {
            throw new NotSupportedException(KeyFilterNotSupported);
        }

        property = bound;
        return true;
    }

    // Chroma has no $not, so a negation is pushed down to the comparisons it contains.
    private Condition Translate(Expression? node, bool negated)
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
            Expression when node.Type == typeof(bool) && TryBindDataProperty(node, out var property)
                => ChromaWhereOperator.Equal(property.StorageName, !negated),

            // r => true matches every record, which is useful for fetching all records, and r => false none.
            ConstantExpression { Value: bool value } => value != negated ? Condition.All : Condition.Nothing,

            MethodCallExpression methodCall => TranslateMethodCall(methodCall, negated),

            _ => throw new NotSupportedException("Chroma does not support the following NodeType in filters: " + node?.NodeType)
        };

    private Condition TranslateEqual(Expression left, Expression right, bool negated)
        => TryBindDataProperty(left, out var property) && TryGetConstant(right, out var rightConstant)
            ? GenerateEqual(property.StorageName, rightConstant, negated)
            : TryBindDataProperty(right, out property) && TryGetConstant(left, out var leftConstant)
                ? GenerateEqual(property.StorageName, leftConstant, negated)
                : throw new NotSupportedException("Invalid equality/comparison");

    private static ChromaWhereOperator GenerateEqual(string propertyStorageName, object? value, bool negated)
    {
        var metadataValue = ToFilterValue(value);

        return negated
            ? ChromaWhereOperator.NotEqual(propertyStorageName, metadataValue)
            : ChromaWhereOperator.Equal(propertyStorageName, metadataValue);
    }

    private Condition TranslateComparison(BinaryExpression comparison, bool negated)
    {
        // Normalize to property-on-the-left.
        var (property, value, nodeType) =
            TryBindDataProperty(comparison.Left, out var leftProperty) && TryGetConstant(comparison.Right, out var rightValue)
                ? (leftProperty, rightValue, comparison.NodeType)
                : TryBindDataProperty(comparison.Right, out var rightProperty) && TryGetConstant(comparison.Left, out var leftValue)
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

    // A constant compared with a nullable property is converted to the nullable type, e.g. r => r.NullableInt == 5
    private static bool TryGetConstant(Expression expression, out object? value)
    {
        switch (expression)
        {
            case ConstantExpression constant:
                value = constant.Value;
                return true;

            case UnaryExpression { NodeType: ExpressionType.Convert, Operand: ConstantExpression constant } convert
                when Nullable.GetUnderlyingType(convert.Type) == constant.Type:
                value = constant.Value;
                return true;

            default:
                value = null;
                return false;
        }
    }

    // A side that matches no record makes the whole AND match no record.
    private static Condition And(Condition left, Condition right)
        => left.MatchesNothing || right.MatchesNothing ? Condition.Nothing
            : left.Where is null ? right
            : right.Where is null ? left
            : left.Where & right.Where;

    // A side that matches every record makes the whole OR match every record, and a side that matches none drops out.
    private static Condition Or(Condition left, Condition right)
        => left.MatchesNothing ? right
            : right.MatchesNothing ? left
            : left.Where is null || right.Where is null ? Condition.All
            : left.Where | right.Where;

    private Condition TranslateMethodCall(MethodCallExpression methodCall, bool negated)
        => methodCall switch
        {
            // string.Contains() on the document property is handled as a conjunct; Chroma has no substring filter on metadata.
            _ when IsStringContains(methodCall, out _, out _) => throw new NotSupportedException(TextFilterNotSupported),

            // Enumerable.Contains(), List.Contains(), MemoryExtensions.Contains()
            _ when TryMatchContains(methodCall, out var source, out var item)
                => TranslateContains(source, item, negated),

            // Enumerable.Any() with a Contains predicate (r => r.Strings.Any(s => array.Contains(s)))
            { Method.Name: nameof(Enumerable.Any), Arguments: [var anySource, LambdaExpression lambda] } any
                when any.Method.DeclaringType == typeof(Enumerable)
                => TranslateAny(anySource, lambda, negated),

            _ => throw new NotSupportedException($"Unsupported method call: {methodCall.Method.DeclaringType?.Name}.{methodCall.Method.Name}")
        };

    private Condition TranslateContains(Expression source, Expression item, bool negated)
    {
        switch (source)
        {
            // Contains over field enumerable
            case var _ when TryBindDataProperty(source, out var property):
                if (!TryGetConstant(item, out var value))
                {
                    throw new NotSupportedException("Unsupported item in Contains");
                }

                return negated
                    ? ChromaWhereOperator.NotContains(property.StorageName, ToFilterValue(value))
                    : ChromaWhereOperator.Contains(property.StorageName, ToFilterValue(value));

            // Contains over inline enumerable
            case NewArrayExpression newArray:
                return ProcessInlineEnumerable(GetInlineArrayElements(newArray), item);

            case ConstantExpression { Value: IEnumerable enumerable and not string }:
                return ProcessInlineEnumerable(enumerable, item);

            default:
                throw new NotSupportedException("Unsupported Contains");
        }

        Condition ProcessInlineEnumerable(IEnumerable elements, Expression item)
        {
            if (!TryBindDataProperty(item, out var property))
            {
                throw new NotSupportedException("Unsupported item type in Contains");
            }

            var values = elements.Cast<object?>().Select(ToFilterValue).ToArray();

            return values.Length switch
            {
                // Contains over no values matches no record, and its negation matches every record;
                // Chroma rejects $in and $nin without values, so neither is sent.
                0 => negated ? Condition.All : Condition.Nothing,

                _ => negated
                    ? ChromaWhereOperator.NotIn(property.StorageName, values)
                    : ChromaWhereOperator.In(property.StorageName, values)
            };
        }
    }

    // r.Strings.Any(s => array.Contains(s)) is true when the field contains at least one of the values.
    private Condition TranslateAny(Expression source, LambdaExpression lambda, bool negated)
    {
        if (!TryBindDataProperty(source, out var property)
            || lambda.Body is not MethodCallExpression containsCall
            || !TryMatchContains(containsCall, out var valuesExpression, out var itemExpression)
            || itemExpression != lambda.Parameters[0])
        {
            throw new NotSupportedException("Unsupported method call: Enumerable.Any");
        }

        IEnumerable values = valuesExpression switch
        {
            NewArrayExpression newArray => GetInlineArrayElements(newArray),
            ConstantExpression { Value: IEnumerable enumerable and not string } => enumerable,
            _ => throw new NotSupportedException("Unsupported method call: Enumerable.Any")
        };

        var conditions = values.Cast<object?>()
            .Select(value => negated
                ? ChromaWhereOperator.NotContains(property.StorageName, ToFilterValue(value))
                : ChromaWhereOperator.Contains(property.StorageName, ToFilterValue(value)))
            .ToList();

        return conditions.Count switch
        {
            // Any() over no values matches no record, and its negation matches every record.
            0 => negated ? Condition.All : Condition.Nothing,

            // !(contains a || contains b) is !contains a && !contains b.
            _ => conditions.Aggregate((left, right) => negated ? left & right : left | right)
        };
    }

    // The elements of an inline array: new[] { "a", "b" }, or none for new string[0], whose only expression is its length.
    private static object?[] GetInlineArrayElements(NewArrayExpression newArray)
        => newArray switch
        {
            { NodeType: ExpressionType.NewArrayInit } => newArray.Expressions
                .Select(element => element is ConstantExpression { Value: var value }
                    ? value
                    : throw new NotSupportedException("Inline array elements must be constants"))
                .ToArray(),
            { NodeType: ExpressionType.NewArrayBounds, Expressions: [ConstantExpression { Value: 0 }] } => [],
            _ => throw new NotSupportedException("Unsupported inline array")
        };

    private static object ToFilterValue(object? value)
        => ChromaFieldMapping.ToMetadataValue(value) switch
        {
            null => throw new NotSupportedException("Chroma does not support filtering on null values."),
            IList => throw new NotSupportedException("Chroma does not support comparing an array property with an array."),
            var metadataValue => metadataValue
        };

    // A condition on the metadata: a where clause, every record (no where clause), or no record.
    private readonly struct Condition
    {
        private Condition(ChromaWhereOperator? where, bool matchesNothing)
        {
            Where = where;
            MatchesNothing = matchesNothing;
        }

        public static Condition All => default;

        public static Condition Nothing => new(where: null, matchesNothing: true);

        public ChromaWhereOperator? Where { get; }

        public bool MatchesNothing { get; }

        public static implicit operator Condition(ChromaWhereOperator where) => new(where, matchesNothing: false);
    }
}
