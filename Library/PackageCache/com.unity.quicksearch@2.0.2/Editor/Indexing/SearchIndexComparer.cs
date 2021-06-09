using System.Collections.Generic;

namespace Unity.QuickSearch
{
    internal enum SearchIndexOperator
    {
        Contains,
        Equal,
        NotEqual,
        Greater,
        GreaterOrEqual,
        Less,
        LessOrEqual,

        Document,
        None
    }

    class SearchIndexComparer : IComparer<SearchIndexEntry>, IEqualityComparer<SearchIndexEntry>
    {
        const double EPSILON = 0.0000000000001;

        public SearchIndexOperator op { get; private set; }

        public SearchIndexComparer(SearchIndexOperator op = SearchIndexOperator.Contains)
        {
            this.op = op;
        }

        public int Compare(SearchIndexEntry item1, SearchIndexEntry item2)
        {
            var c = item1.type.CompareTo(item2.type);
            if (c != 0)
                return c;
            c = item1.crc.CompareTo(item2.crc);
            if (c != 0)
                return c;

            if (item1.type == SearchIndexEntry.Type.Number)
            {
                double eps = 0.0;
                if (op == SearchIndexOperator.Less)
                    eps = -EPSILON;
                else if (op == SearchIndexOperator.Greater)
                    eps = EPSILON;
                c = item1.number.CompareTo(item2.number + eps);
            }
            else
                c = item1.key.CompareTo(item2.key);
            if (c != 0)
                return c;

            if (op == SearchIndexOperator.Document)
            {
                c = item1.index.CompareTo(item2.index);
                if (c != 0)
                    return c;
            }

            if (item2.score == int.MaxValue)
                return 0;
            return item1.score.CompareTo(item2.score);
        }

        public bool Equals(SearchIndexEntry x, SearchIndexEntry y)
        {
            return x.Equals(y);
        }

        public int GetHashCode(SearchIndexEntry obj)
        {
            return obj.GetHashCode();
        }
    }
}
