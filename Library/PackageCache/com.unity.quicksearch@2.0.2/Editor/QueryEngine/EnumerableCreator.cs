using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.PlayerLoop;

namespace Unity.QuickSearch
{
    interface IQueryEnumerable<T> : IEnumerable<T>
    {
        void SetPayload(IEnumerable<T> payload);
    }

    interface IQueryEnumerableFactory
    {
        IQueryEnumerable<T> Create<T>(IQueryNode root, QueryEngine<T> engine, ICollection<QueryError> errors);
    }

    class EnumerableCreatorAttribute : Attribute
    {
        public QueryNodeType nodeType { get; }

        public EnumerableCreatorAttribute(QueryNodeType nodeType)
        {
            this.nodeType = nodeType;
        }
    }

    static class EnumerableCreator
    {
        static Dictionary<QueryNodeType, IQueryEnumerableFactory> s_EnumerableFactories;

        [InitializeOnLoadMethod]
        public static void Init()
        {
            s_EnumerableFactories = new Dictionary<QueryNodeType, IQueryEnumerableFactory>();
            var factoryTypes = TypeCache.GetTypesWithAttribute<EnumerableCreatorAttribute>().Where(t => typeof(IQueryEnumerableFactory).IsAssignableFrom(t));
            foreach (var factoryType in factoryTypes)
            {
                var enumerableCreatorAttribute = factoryType.GetCustomAttributes(typeof(EnumerableCreatorAttribute), false).Cast<EnumerableCreatorAttribute>().FirstOrDefault();
                if (enumerableCreatorAttribute == null)
                    continue;

                var nodeType = enumerableCreatorAttribute.nodeType;
                var factory = Activator.CreateInstance(factoryType) as IQueryEnumerableFactory;
                if (factory == null)
                    continue;

                if (s_EnumerableFactories.ContainsKey(nodeType))
                {
                    Debug.LogWarning($"Factory for node type {nodeType} already exists.");
                    continue;
                }
                s_EnumerableFactories.Add(nodeType, factory);
            }
        }

        public static IQueryEnumerable<T> Create<T>(IQueryNode root, QueryEngine<T> engine, ICollection<QueryError> errors)
        {
            return s_EnumerableFactories.TryGetValue(root.type, out var factory) ? factory.Create<T>(root, engine, errors) : null;
        }
    }
}