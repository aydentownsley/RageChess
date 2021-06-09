using System;
using System.Collections;
using System.Collections.Generic;

namespace Unity.QuickSearch
{
    class NestedQueryEnumerable<T> : IQueryEnumerable<T>
    {
        IEnumerable<T> m_NestedQueryEnumerable;

        public NestedQueryEnumerable(IEnumerable<T> nestedQueryEnumerable)
        {
            m_NestedQueryEnumerable = nestedQueryEnumerable;
        }

        public void SetPayload(IEnumerable<T> payload)
        { }

        public IEnumerator<T> GetEnumerator()
        {
            return m_NestedQueryEnumerable.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    [EnumerableCreator(QueryNodeType.NestedQuery)]
    class NestedQueryEnumerableFactory : IQueryEnumerableFactory
    {
        public IQueryEnumerable<T> Create<T>(IQueryNode root, QueryEngine<T> engine, ICollection<QueryError> errors)
        {
            var nestedQueryNode = root as NestedQueryNode;
            var nestedQueryHandler = nestedQueryNode?.nestedQueryHandler as NestedQueryHandler<T>;
            if (nestedQueryHandler == null)
            {
                errors.Add(new QueryError(root.token.position, "There is no handler set for nested queries."));
                return null;
            }

            var nestedEnumerable = nestedQueryHandler.handler(nestedQueryNode.identifier, nestedQueryNode.associatedFilter);
            if (nestedEnumerable == null)
            {
                errors.Add(new QueryError(root.token.position, "Could not create enumerable from nested query handler."));
            }
            return new NestedQueryEnumerable<T>(nestedEnumerable);
        }
    }
}