using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine.Assertions;

namespace Unity.QuickSearch
{
    class WhereEnumerable<T> : IQueryEnumerable<T>
    {
        IEnumerable<T> m_Payload;
        Func<T, bool> m_Predicate;

        public WhereEnumerable(Func<T, bool> predicate)
        {
            m_Predicate = predicate;
        }

        public void SetPayload(IEnumerable<T> payload)
        {
            m_Payload = payload;
        }

        public IEnumerator<T> GetEnumerator()
        {
            return m_Payload.Where(m_Predicate).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    [EnumerableCreator(QueryNodeType.Where)]
    class WhereEnumerableFactory : IQueryEnumerableFactory
    {
        public IQueryEnumerable<T> Create<T>(IQueryNode root, QueryEngine<T> engine, ICollection<QueryError> errors)
        {
            if (root.leaf || root.children == null || root.children.Count != 1)
            {
                errors.Add(new QueryError(root.token.position, "Where node must have a child."));
                return null;
            }

            var predicateGraphRoot = root.children[0];

            var predicate = BuildFunctionFromNode(predicateGraphRoot, engine, errors);
            var whereEnumerable = new WhereEnumerable<T>(predicate);
            return whereEnumerable;
        }

        private Func<T, bool> BuildFunctionFromNode<T>(IQueryNode node, QueryEngine<T> engine, ICollection<QueryError> errors)
        {
            Func<T, bool> noOp = o => false;

            if (node == null)
                return noOp;

            switch (node.type)
            {
                case QueryNodeType.And:
                {
                    Assert.IsFalse(node.leaf, "And node cannot be leaf.");
                    var leftFunc = BuildFunctionFromNode(node.children[0], engine, errors);
                    var rightFunc = BuildFunctionFromNode(node.children[1], engine, errors);
                    return o => leftFunc(o) && rightFunc(o);
                }
                case QueryNodeType.Or:
                {
                    Assert.IsFalse(node.leaf, "Or node cannot be leaf.");
                    var leftFunc = BuildFunctionFromNode(node.children[0], engine, errors);
                    var rightFunc = BuildFunctionFromNode(node.children[1], engine, errors);
                    return o => leftFunc(o) || rightFunc(o);
                }
                case QueryNodeType.Not:
                {
                    Assert.IsFalse(node.leaf, "Not node cannot be leaf.");
                    var childFunc = BuildFunctionFromNode(node.children[0], engine, errors);
                    return o => !childFunc(o);
                }
                case QueryNodeType.Filter:
                {
                    var filterNode = node as FilterNode;
                    if (filterNode == null)
                        return noOp;
                    var filterOperation = GenerateFilterOperation(filterNode, engine, errors);
                    if (filterOperation == null)
                        return noOp;
                    return o => filterOperation.Match(o);
                }
                case QueryNodeType.Search:
                {
                    if (engine.searchDataCallback == null)
                        return o => false;
                    var searchNode = node as SearchNode;
                    Assert.IsNotNull(searchNode);
                    Func<string, bool> matchWordFunc;
                    var stringComparison = engine.globalStringComparison;
                    if (engine.searchDataOverridesStringComparison)
                        stringComparison = engine.searchDataStringComparison;
                    if (searchNode.exact)
                        matchWordFunc = s => s.Equals(searchNode.searchValue, stringComparison);
                    else
                        matchWordFunc = s => s.IndexOf(searchNode.searchValue, stringComparison) >= 0;
                    return o => engine.searchDataCallback(o).Any(data => matchWordFunc(data));
                }
                case QueryNodeType.FilterIn:
                {
                    var filterNode = node as InFilterNode;
                    if (filterNode == null)
                        return noOp;
                    var filterOperation = GenerateFilterOperation(filterNode, engine, errors);
                    if (filterOperation == null)
                        return noOp;
                    var inFilterFunction = GenerateInFilterFunction(filterNode, filterOperation, engine, errors);
                    return inFilterFunction;
                }
            }

            return noOp;
        }

        private BaseFilterOperation<T> GenerateFilterOperation<T>(FilterNode node, QueryEngine<T> engine, ICollection<QueryError> errors)
        {
            var operatorIndex = node.token.position + node.filter.token.Length + (string.IsNullOrEmpty(node.paramValue) ? 0 : node.paramValue.Length);
            var filterValueIndex = operatorIndex + node.op.token.Length;

            Type filterValueType;
            IParseResult parseResult = null;
            if (QueryEngineUtils.IsNestedQueryToken(node.filterValue))
            {
                if (node.filter?.queryHandlerTransformer == null)
                {

                    errors.Add(new QueryError(filterValueIndex, node.filterValue.Length, $"No nested query handler transformer set on filter \"{node.filter.token}\"."));
                    return null;
                }
                filterValueType = node.filter.queryHandlerTransformer.rightHandSideType;
            }
            else
            {
                parseResult = engine.ParseFilterValue(node.filterValue, node.filter, node.op, out filterValueType);
                if (!parseResult.success)
                {
                    errors.Add(new QueryError(filterValueIndex, node.filterValue.Length, $"The value \"{node.filterValue}\" could not be converted to any of the supported handler types."));
                    return null;
                }
            }

            IFilterOperationGenerator generator = engine.GetGeneratorForType(filterValueType);
            if (generator == null)
            {
                errors.Add(new QueryError(filterValueIndex, node.filterValue.Length, $"Unknown type \"{filterValueType}\". Did you set an operator handler for this type?"));
                return null;
            }

            var generatorData = new FilterOperationGeneratorData
            {
                filterValue = node.filterValue,
                filterValueParseResult = parseResult,
                globalStringComparison = engine.globalStringComparison,
                op = node.op,
                paramValue = node.paramValue,
                generator = generator
            };
            var operation = node.filter.GenerateOperation(generatorData, operatorIndex, errors);
            return operation as BaseFilterOperation<T>;
        }

        private Func<T, bool> GenerateInFilterFunction<T>(InFilterNode node, BaseFilterOperation<T> filterOperation, QueryEngine<T> engine, ICollection<QueryError> errors)
        {
            if (node.leaf || node.children == null || node.children.Count == 0)
            {
                errors.Add(new QueryError(node.token.position, "InFilter node cannot be a leaf."));
                return null;
            }

            var nestedQueryType = GetNestedQueryType(node);
            if (nestedQueryType == null)
            {
                errors.Add(new QueryError(node.token.position, "Could not deduce nested query type. Did you forget to set the nested query handler?"));
                return null;
            }

            var transformType = node.filter.queryHandlerTransformer.rightHandSideType;

            var inFilterFunc = typeof(WhereEnumerableFactory)
                ?.GetMethod("GenerateInFilterFunctionWithTypes", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
                ?.MakeGenericMethod(typeof(T), nestedQueryType, transformType)
                ?.Invoke(this, new object[] { node, filterOperation, engine, errors }) as Func<T, bool>;

            if (inFilterFunc == null)
            {
                errors.Add(new QueryError(node.token.position, "Could not create filter function with nested query."));
                return null;
            }

            return inFilterFunc;
        }

        private Func<T, bool> GenerateInFilterFunctionWithTypes<T, TNested, TTransform>(InFilterNode node, BaseFilterOperation<T> filterOperation, QueryEngine<T> engine, ICollection<QueryError> errors)
        {
            var nestedQueryEnumerable = EnumerableCreator.Create<TNested>(node.children[0], null, errors);
            if (nestedQueryEnumerable == null)
                return null;
            var nestedQueryTransformer = node.filter.queryHandlerTransformer as NestedQueryHandlerTransformer<TNested, TTransform>;
            if (nestedQueryTransformer == null)
                return null;
            var transformerFunction = nestedQueryTransformer.handler;
            var dynamicFilterOperation = filterOperation as IDynamicFilterOperation<TTransform>;
            if (dynamicFilterOperation == null)
                return null;
            return o =>
            {
                foreach (var item in nestedQueryEnumerable)
                {
                    var transformedValue = transformerFunction(item);
                    dynamicFilterOperation.SetFilterValue(transformedValue);
                    if (filterOperation.Match(o))
                        return true;
                }

                return false;
            };
        }

        private static Type GetNestedQueryType(IQueryNode node)
        {
            if (node.type == QueryNodeType.NestedQuery)
            {
                var nn = node as NestedQueryNode;
                return nn?.nestedQueryHandler.enumerableType;
            }

            if (node.leaf || node.children == null)
                return null;

            foreach (var child in node.children)
            {
                var type = GetNestedQueryType(child);
                if (type != null)
                    return type;
            }

            return null;
        }
    }
}