//#define DEBUG_INDEXING

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;

namespace Unity.QuickSearch
{
    [DebuggerDisplay("{type} - K:{key}|{number} - C:{crc} - I:{index}")]
    readonly struct SearchIndexEntry : IEquatable<SearchIndexEntry>
    {
        //  1- Initial format
        //  2- Added score to words
        //  3- Save base name in entry paths
        //  4- Added entry types
        //  5- Added indexing tags
        //  6- Revert indexes back to 32 bits instead of 64 bits.
        //  7- Remove min and max char variations.
        //  8- Add metadata field to documents
        //  9- Add document hash support
        // 10- Remove the index tag header
        // 11- Save more keywords
        internal const int version = 0x4242E000 | 0x011;

        public enum Type : int
        {
            Undefined = 0,
            Word,
            Number,
            Property
        }

        public readonly long key;      // Value hash
        public readonly int crc;       // Value correction code (can be length, property key hash, etc.)
        public readonly Type type;     // Type of the index entry
        public readonly int index;     // Index of documents in the documents array
        public readonly int score;
        public readonly double number;

        public SearchIndexEntry(long _key, int _crc, Type _type, int _index = -1, int _score = int.MaxValue)
        {
            key = _key;
            crc = _crc;
            type = _type;
            index = _index;
            score = _score;
            number = BitConverter.Int64BitsToDouble(key);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return key.GetHashCode() ^ crc.GetHashCode() ^ type.GetHashCode() ^ index.GetHashCode();
            }
        }

        public override bool Equals(object other)
        {
            return other is SearchIndexEntry l && Equals(l);
        }

        public bool Equals(SearchIndexEntry other)
        {
            return key == other.key && crc == other.crc && type == other.type && index == other.index;
        }
    }

    /// <summary>
    /// Encapsulate an element that was retrieved from a query in a g.
    /// </summary>
    [DebuggerDisplay("{id}[{index}] ({score})")]
    public readonly struct SearchResult : IEquatable<SearchResult>, IComparable<SearchResult>
    {
        /// <summary>Id of the document containing that result.</summary>
        public readonly string id;
        /// <summary>Index of the document containing that result.</summary>
        public readonly int index;
        /// <summary>Score of the result. Higher means it is a more relevant result.</summary>
        public readonly int score;

        /// <summary>
        /// Create a new SearchResult
        /// </summary>
        /// <param name="id">Id of the document containing that result.</param>
        /// <param name="index">Index of the document containing that result.</param>
        /// <param name="score">Score of the result. Higher means it is a more relevant result.</param>
        public SearchResult(string id, int index, int score)
        {
            this.id = id;
            this.index = index;
            this.score = score;
        }

        /// <summary>
        /// Create a new SearchResult
        /// </summary>
        /// <param name="index">Index of the document containing that result.</param>
        public SearchResult(int index)
        {
            this.id = null;
            this.score = 0;
            this.index = index;
        }

        /// <summary>
        /// Create a new SearchResult
        /// </summary>
        /// <param name="index">Index of the document containing that result.</param>
        /// <param name="score">Score of the result. Higher means it is a more relevant result.</param>
        public SearchResult(int index, int score)
        {
            this.id = null;
            this.index = index;
            this.score = score;
        }

        internal SearchResult(in SearchIndexEntry entry)
        {
            this.id = null;
            this.index = entry.index;
            this.score = entry.score;
        }

        /// <summary>
        /// Compare Search Result using their index value.
        /// </summary>
        /// <param name="other">Another SearchResult to compare.</param>
        /// <returns>Returns true if both SearchResult have the same index.</returns>
        public bool Equals(SearchResult other)
        {
            return index == other.index;
        }

        /// <summary>
        /// Compute the hash code for this SearchResult from its index property.
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            unchecked
            {
                return index.GetHashCode();
            }
        }
        /// <summary>
        /// Compare Search Result using their index value.
        /// </summary>
        /// <param name="other">Another SearchResult to compare.</param>
        /// <returns>Returns true if both SearchResult have the same index.</returns>
        public override bool Equals(object other)
        {
            return other is SearchResult l && Equals(l);
        }

        /// <summary>
        /// Compare Search Result using their index value.
        /// </summary>
        /// <param name="other">Another SearchResult to compare.</param>
        /// <returns>Returns true if both SearchResult have the same index.</returns>
        public int CompareTo(SearchResult other)
        {
            var c = other.score.CompareTo(other.score);
            if (c == 0)
                return index.CompareTo(other.index);
            return c;
        }
    }

    /// <summary>
    /// Represents a searchable document that has been indexed.
    /// </summary>
    public class SearchDocument
    {
        /// <summary>
        /// Create a new SearchDocument
        /// </summary>
        /// <param name="id">Document Id</param>
        /// <param name="metadata">Additional data about this document</param>
        public SearchDocument(string id, string metadata = null)
        {
            this.id = id;
            this.metadata = metadata;
        }

        /// <summary>
        /// Document unique id in the search index.
        /// </summary>
        public string id { get; private set; }

        /// <summary>
        /// Additional meta data about the document
        /// </summary>
        [CanBeNull] public string metadata { get; internal set; }

        /// <summary>
        /// Returns the document id string.
        /// </summary>
        /// <returns>Returns a string representation of the Document.</returns>
        public override string ToString()
        {
            return id;
        }
    }

    /// <summary>
    /// Base class for an Indexer of document which allow retrieving of a document given a specific pattern in roughly log(n).
    /// </summary>
    public class SearchIndexer
    {
        /// <summary>
        /// Name of the document. Generally this name is given by a user from a <see cref="SearchDatabase.Settings"/>
        /// </summary>
        public string name { get; set; }

        internal int keywordCount => m_Keywords.Count;
        internal int documentCount => m_Documents.Count;
        internal int indexCount
        {
            get
            {
                lock (this)
                {
                    int total = 0;
                    if (m_Indexes != null && m_Indexes.Length > 0)
                        total += m_Indexes.Length;
                    if (m_BatchIndexes != null && m_BatchIndexes.Count > 0)
                        total += m_BatchIndexes.Count;
                    return total;
                }
            }
        }

        /// <summary>
        /// Handler used to skip some entries.
        /// </summary>
        public Func<string, bool> skipEntryHandler { get; set; }

        /// <summary>
        /// Handler used to parse and split the search query text into words. The tokens needs to be split similarly to words and properties are indexed.
        /// </summary>
        public Func<string, string[]> getQueryTokensHandler { get; set; }

        private Thread m_IndexerThread;
        private volatile bool m_IndexReady = false;
        /// <summary>
        /// Is the current indexing thread aborted.
        /// </summary>
        protected volatile bool m_ThreadAborted = false;
        private readonly Dictionary<RangeSet, IndexRange> m_FixedRanges;
        private SearchResultCollection m_AllDocumentIndexes;
        private readonly Dictionary<int, int> m_PatternMatchCount;

        // Temporary documents and entries while the index is being built (i.e. Start/Finish).
        private readonly List<SearchIndexEntry> m_BatchIndexes;

        // Final documents and entries when the index is ready.
        private List<SearchDocument> m_Documents;
        private SearchIndexEntry[] m_Indexes;
        private HashSet<string> m_Keywords;

        #if UNITY_2020_1_OR_NEWER
        private Dictionary<string, Hash128> m_DocumentHashes;
        #else
        private Dictionary<string, string> m_DocumentHashes;
        #endif

        /// <summary>
        /// Create a new default SearchIndexer.
        /// </summary>
        public SearchIndexer()
            : this(String.Empty)
        {
        }

        /// <summary>
        /// Create a new SearchIndexer.
        /// </summary>
        /// <param name="name">Name of the indexer</param>
        public SearchIndexer(string name)
        {
            this.name = name;

            skipEntryHandler = e => false;
            getQueryTokensHandler = ParseQuery;

            m_Keywords = new HashSet<string>();
            m_Documents = new List<SearchDocument>();
            m_Indexes = new SearchIndexEntry[0];
            m_BatchIndexes = new List<SearchIndexEntry>();
            m_PatternMatchCount = new Dictionary<int, int>();
            m_FixedRanges = new Dictionary<RangeSet, IndexRange>();

            #if UNITY_2020_1_OR_NEWER
            m_DocumentHashes = new Dictionary<string, Hash128>();
            #else
            m_DocumentHashes = new Dictionary<string, string>();
            #endif
        }

        /// <summary>
        /// Build custom derived indexes.
        /// </summary>
        public virtual void Build()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Add a new word coming from a specific document to the index. The word will be added with multiple variations allowing partial search.
        /// </summary>
        /// <param name="word">Word to add to the index.</param>
        /// <param name="score">Relevance score of the word.</param>
        /// <param name="documentIndex">Document where the indexed word was found.</param>
        public void AddWord(string word, int score, int documentIndex)
        {
            lock (this)
                AddWord(word, 2, word.Length, score, documentIndex, m_BatchIndexes);
        }

        /// <summary>
        /// Add a new word coming from a specific document to the index. The word will be added with multiple variations allowing partial search.
        /// </summary>
        /// <param name="word">Word to add to the index.</param>
        /// <param name="size">Number of variations to compute.</param>
        /// <param name="score">Relevance score of the word.</param>
        /// <param name="documentIndex">Document where the indexed word was found.</param>
        public void AddWord(string word, int size, int score, int documentIndex)
        {
            lock (this)
                AddWord(word, size, size, score, documentIndex, m_BatchIndexes);
        }

        /// <summary>
        /// Add a new word coming from a specific document to the index. The word will be added as an exact match.
        /// </summary>
        /// <param name="word">Word to add to the index.</param>
        /// <param name="score">Relevance score of the word.</param>
        /// <param name="documentIndex">Document where the indexed word was found.</param>
        public void AddExactWord(string word, int score, int documentIndex)
        {
            lock (this)
                AddExactWord(word, score, documentIndex, m_BatchIndexes);
        }

        /// <summary>
        /// Add a new word coming from a specific document to the index. The word will be added with multiple variations allowing partial search.
        /// </summary>
        /// <param name="word">Word to add to the index.</param>
        /// <param name="minVariations">Minimum number of variations to compute. Cannot be higher than the length of the word.</param>
        /// <param name="maxVariations">Maximum number of variations to compute. Cannot be higher than the length of the word.</param>
        /// <param name="score">Relevance score of the word.</param>
        /// <param name="documentIndex">Document where the indexed word was found.</param>
        public void AddWord(string word, int minVariations, int maxVariations, int score, int documentIndex)
        {
            lock (this)
                AddWord(word, minVariations, maxVariations, score, documentIndex, m_BatchIndexes);
        }

        /// <summary>
        /// Add a key-number value pair to the index. The key won't be added with variations.
        /// </summary>
        /// <param name="key">Key used to retrieve the value.</param>
        /// <param name="value">Number value to store in the index.</param>
        /// <param name="score">Relevance score of the word.</param>
        /// <param name="documentIndex">Document where the indexed value was found.</param>
        public void AddNumber(string key, double value, int score, int documentIndex)
        {
            lock (this)
                AddNumber(key, value, score, documentIndex, m_BatchIndexes);
        }

        /// <summary>
        /// Add a property value to the index. A property is specified with a key and a string value. The value will be stored with multiple variations.
        /// </summary>
        /// <param name="key">Key used to retrieve the value.</param>
        /// <param name="value">String value to store in the index.</param>
        /// <param name="documentIndex">Document where the indexed value was found.</param>
        /// <param name="saveKeyword">Define if we store this key in the keyword registry of the index. See <see cref="SearchIndexer.GetKeywords"/>.</param>
        /// <param name="exact">If true, we will store also an exact match entry for this word.</param>
        public void AddProperty(string key, string value, int documentIndex, bool saveKeyword = false, bool exact = true)
        {
            lock (this)
                AddProperty(key, value, 2, value.Length, 0, documentIndex, m_BatchIndexes, exact, saveKeyword);
        }

        /// <summary>
        /// Add a property value to the index. A property is specified with a key and a string value. The value will be stored with multiple variations.
        /// </summary>
        /// <param name="key">Key used to retrieve the value.</param>
        /// <param name="value">String value to store in the index.</param>
        /// <param name="score">Relevance score of the word.</param>
        /// <param name="documentIndex">Document where the indexed value was found.</param>
        /// <param name="saveKeyword">Define if we store this key in the keyword registry of the index. See <see cref="SearchIndexer.GetKeywords"/>.</param>
        /// <param name="exact">If true, we will store also an exact match entry for this word.</param>
        public void AddProperty(string key, string value, int score, int documentIndex, bool saveKeyword = false, bool exact = true)
        {
            lock (this)
                AddProperty(key, value, 2, value.Length, score, documentIndex, m_BatchIndexes, exact, saveKeyword);
        }

        /// <summary>
        /// Add a property value to the index. A property is specified with a key and a string value. The value will be stored with multiple variations.
        /// </summary>
        /// <param name="name">Key used to retrieve the value.</param>
        /// <param name="value">String value to store in the index.</param>
        /// <param name="minVariations">Minimum number of variations to compute for the value. Cannot be higher than the length of the word.</param>
        /// <param name="maxVariations">Maximum number of variations to compute for the value. Cannot be higher than the length of the word.</param>
        /// <param name="score">Relevance score of the word.</param>
        /// <param name="documentIndex">Document where the indexed value was found.</param>
        /// <param name="saveKeyword">Define if we store this key in the keyword registry of the index. See <see cref="SearchIndexer.GetKeywords"/>.</param>
        /// <param name="exact">If true, we will store also an exact match entry for this word.</param>
        public void AddProperty(string name, string value, int minVariations, int maxVariations, int score, int documentIndex, bool saveKeyword = false, bool exact = true)
        {
            lock (this)
                AddProperty(name, value, minVariations, maxVariations, score, documentIndex, m_BatchIndexes, exact, saveKeyword);
        }

        /// <summary>
        /// Is the index fully built and up to date and ready for search.
        /// </summary>
        /// <returns>Returns true if the index is ready for search.</returns>
        public bool IsReady()
        {
            return m_IndexReady;
        }

        /// <summary>
        /// Run a search query in the index.
        /// </summary>
        /// <param name="query">Search query to look out for. If if matches any of the indexed variations a result will be returned.</param>
        /// <param name="maxScore">Maximum score of any matched Search Result. See <see cref="SearchResult.score"/>.</param>
        /// <param name="patternMatchLimit">Maximum number of matched Search Result that can be returned. See <see cref="SearchResult"/>.</param>
        /// <returns>Returns a collection of Search Result matching the query.</returns>
        public virtual IEnumerable<SearchResult> Search(string query, int maxScore = int.MaxValue, int patternMatchLimit = 2999)
        {
            //using (new DebugTimer($"Search Index ({query})"))
            {
                if (!m_IndexReady)
                    return Enumerable.Empty<SearchResult>();

                var tokens = getQueryTokensHandler(query);
                Array.Sort(tokens, SortTokensByPatternMatches);

                var lengths = tokens.Select(p => p.Length).ToArray();
                var patterns = tokens.Select(p => p.GetHashCode()).ToArray();

                if (patterns.Length == 0)
                    return Enumerable.Empty<SearchResult>();

                var wiec = new SearchIndexComparer();
                lock (this)
                {
                    var remains = SearchIndexes(patterns[0], lengths[0], SearchIndexEntry.Type.Word, maxScore, wiec, null, patternMatchLimit).ToList();
                    m_PatternMatchCount[patterns[0]] = remains.Count;

                    if (remains.Count == 0)
                        return Enumerable.Empty<SearchResult>();

                    for (int i = 1; i < patterns.Length; ++i)
                    {
                        var subset = new SearchResultCollection(remains);
                        remains = SearchIndexes(patterns[i], lengths[i], SearchIndexEntry.Type.Word, maxScore, wiec, subset, patternMatchLimit).ToList();
                        if (remains.Count == 0)
                            break;
                    }

                    return remains.Select(fi => new SearchResult(m_Documents[fi.index].id, fi.index, fi.score));
                }
            }
        }

        /// <summary>
        /// Write a binary representation of the the index on a stream.
        /// </summary>
        /// <param name="stream">Stream where to write the index.</param>
        public void Write(Stream stream)
        {
            using (var indexWriter = new BinaryWriter(stream))
            {
                indexWriter.Write(SearchIndexEntry.version);

                // Documents
                indexWriter.Write(m_Documents.Count);
                foreach (var p in m_Documents)
                {
                    indexWriter.Write(p.id);

                    bool writeMetadata = !String.IsNullOrEmpty(p.metadata);
                    indexWriter.Write(writeMetadata);
                    if (writeMetadata)
                        indexWriter.Write(p.metadata);
                }

                // Hashes
                indexWriter.Write(m_DocumentHashes.Count);
                foreach (var kvp in m_DocumentHashes)
                {
                    indexWriter.Write(kvp.Key);
                    indexWriter.Write(kvp.Value.ToString());
                }

                // Indexes
                indexWriter.Write(m_Indexes.Length);
                foreach (var p in m_Indexes)
                {
                    indexWriter.Write(p.key);
                    indexWriter.Write(p.crc);
                    indexWriter.Write((int)p.type);
                    indexWriter.Write(p.index);
                    indexWriter.Write(p.score);
                }

                // Keywords
                indexWriter.Write(m_Keywords.Count);
                foreach (var t in m_Keywords)
                    indexWriter.Write(t);
            }
        }
        /// <summary>
        /// Get the bytes representation of this index. See <see cref="SearchIndexer.Write"/>.
        /// </summary>
        /// <returns>Bytes representation of the index.</returns>
        public byte[] SaveBytes()
        {
            using (var memoryStream = new MemoryStream())
            {
                lock (this)
                    Write(memoryStream);
                return memoryStream.ToArray();
            }
        }

        /// <summary>
        /// Read a stream and populate the index from it.
        /// </summary>
        /// <param name="stream">Stream where to read the index from.</param>
        /// <param name="checkVersionOnly">If true, it will only read the version of the index and stop reading any more content.</param>
        /// <returns>Returns false if the version of the index is not supported.</returns>
        public bool Read(Stream stream, bool checkVersionOnly)
        {
            using (var indexReader = new BinaryReader(stream))
            {
                int version = indexReader.ReadInt32();
                if (version != SearchIndexEntry.version)
                    return false;

                if (checkVersionOnly)
                    return true;

                // Documents
                var elementCount = indexReader.ReadInt32();
                var documents = new SearchDocument[elementCount];
                for (int i = 0; i < elementCount; ++i)
                {
                    documents[i] = new SearchDocument(indexReader.ReadString());
                    bool readMetadata = indexReader.ReadBoolean();
                    if (readMetadata)
                        documents[i].metadata = indexReader.ReadString();
                }

                // Hashes
                elementCount = indexReader.ReadInt32();
                #if UNITY_2020_1_OR_NEWER
                var hashes = new Dictionary<string, Hash128>();
                #else
                var hashes = new Dictionary<string, string>();
                #endif
                for (int i = 0; i < elementCount; ++i)
                {
                    var key = indexReader.ReadString();
                    #if UNITY_2020_1_OR_NEWER
                    var hash = Hash128.Parse(indexReader.ReadString());
                    #else
                    var hash = indexReader.ReadString();
                    #endif
                    hashes[key] = hash;
                }

                // Indexes
                elementCount = indexReader.ReadInt32();
                var indexes = new List<SearchIndexEntry>(elementCount);
                for (int i = 0; i < elementCount; ++i)
                {
                    var key = indexReader.ReadInt64();
                    var crc = indexReader.ReadInt32();
                    var type = (SearchIndexEntry.Type)indexReader.ReadInt32();
                    var index = indexReader.ReadInt32();
                    var score = indexReader.ReadInt32();
                    indexes.Add(new SearchIndexEntry(key, crc, type, index, score));
                }

                // Keywords
                elementCount = indexReader.ReadInt32();
                var keywords = new string[elementCount];
                for (int i = 0; i < elementCount; ++i)
                    keywords[i] = indexReader.ReadString();

                // No need to sort the index, it is already sorted in the file stream.
                lock (this)
                {
                    ApplyIndexes(documents, indexes.ToArray(), hashes);
                    m_Keywords = new HashSet<string>(keywords);
                }

                return true;
            }
        }

        /// <summary>
        /// Load asynchronously (i.e. in another thread) the index from a binary buffer.
        /// </summary>
        /// <param name="bytes">Binary buffer containing the index representation.</param>
        /// <param name="finished">Callback that will trigger when the index is fully loaded. The callback parameters indicate if the loading was succesful.</param>
        /// <returns>Returns false if the index is of an unsupported version or if there was a problem initializing the reading thread.</returns>
        public bool LoadBytes(byte[] bytes, Action<bool> finished)
        {
            using (var memoryStream = new MemoryStream(bytes))
                if (!Read(memoryStream, true))
                    return false;

            var t = new Thread(() =>
            {
                using (var memoryStream = new MemoryStream(bytes))
                {
                    var success = Read(memoryStream, false);
                    Dispatcher.Enqueue(() => finished(success));
                }
            });
            t.Start();
            return t.ThreadState != System.Threading.ThreadState.Unstarted;
        }

        internal bool LoadBytes(byte[] bytes)
        {
            using (var memoryStream = new MemoryStream(bytes))
                return Read(memoryStream, false);
        }

        /// <summary>
        /// Called when the index is built to see if a specified document needs to be indexed. See <see cref="SearchIndexer.skipEntryHandler"/>
        /// </summary>
        /// <param name="document">Path of a document</param>
        /// <param name="checkRoots"></param>
        /// <returns>Returns true if the document doesn't need to be indexed.</returns>
        public virtual bool SkipEntry(string document, bool checkRoots = false)
        {
            return skipEntryHandler?.Invoke(document) ?? false;
        }

        /// <summary>
        /// Function to override in a concrete SearchIndexer to index the content of a document.
        /// </summary>
        /// <param name="document">Path of the document to index.</param>
        /// <param name="checkIfDocumentExists">Check if the document actually exists.</param>
        public virtual void IndexDocument(string document, bool checkIfDocumentExists)
        {
            throw new NotImplementedException($"{nameof(IndexDocument)} must be implemented by a specialized indexer.");
        }

        internal void CombineIndexes(SearchIndexer si, int baseScore = 0, Action<int, SearchIndexer> documentIndexing = null)
        {
            int sourceIndex = 0;
            foreach (var doc in si.GetDocuments())
            {
                var di = AddDocument(doc.id, doc.metadata, false);
                documentIndexing?.Invoke(di, this);
                m_BatchIndexes.AddRange(
                    si.m_Indexes.Where(i => i.index == sourceIndex)
                                .Select(i => new SearchIndexEntry(i.key, i.crc, i.type, di, baseScore + i.score)));

                m_Keywords.UnionWith(si.m_Keywords);
                foreach (var hkvp in si.m_DocumentHashes)
                    m_DocumentHashes[hkvp.Key] = hkvp.Value;
                sourceIndex++;
            }
        }

        internal void ApplyFrom(SearchIndexer source)
        {
            lock (this)
            {
                m_IndexReady = false;
                m_Indexes = source.m_Indexes;
                m_Documents = source.m_Documents;
                m_Keywords = source.m_Keywords;
                m_DocumentHashes = source.m_DocumentHashes;

                m_BatchIndexes.Clear();

                m_IndexReady = true;
            }
        }

        internal void ApplyUnsorted()
        {
            lock (this)
                m_Indexes = m_BatchIndexes.ToArray();
        }

        internal IEnumerable<string> GetKeywords() { lock (this) return m_Keywords; }
        internal IEnumerable<SearchDocument> GetDocuments() { lock (this) return m_Documents; }
        internal SearchDocument GetDocument(int index) { lock (this) return m_Documents[index]; }

        internal bool TryGetHash(string id, out Hash128 hash)
        {
            lock (this)
            {
                #if UNITY_2020_1_OR_NEWER
                return m_DocumentHashes.TryGetValue(id, out hash);
                #else
                if (m_DocumentHashes.TryGetValue(id, out string hashStr))
                {
                    hash = Hash128.Parse(hashStr);
                    return true;
                }
                hash = new Hash128();
                return false;
                #endif
            }
        }

        internal void AddWord(string word, int score, int documentIndex, List<SearchIndexEntry> indexes)
        {
            AddWord(word, 2, word.Length, score, documentIndex, indexes);
        }

        internal int AddDocument(string document, bool checkIfExists = true)
        {
            return AddDocument(document, null, checkIfExists);
        }

        internal int AddDocument(string document, string metadata, bool checkIfExists = true)
        {
            // Reformat entry to have them all uniformized.
            if (skipEntryHandler(document))
                return -1;

            lock (this)
            {
                if (checkIfExists)
                {
                    var di = m_Documents.FindIndex(d => d.id == document);
                    if (di >= 0)
                    {
                        m_Documents[di].metadata = metadata;
                        return di;
                    }
                }
                var newDocument = new SearchDocument(document, metadata);
                m_Documents.Add(newDocument);
                return m_Documents.Count - 1;
            }
        }

        internal void AddDocumentHash(string document, Hash128 hash)
        {
            lock (this)
            {
                #if UNITY_2020_1_OR_NEWER
                m_DocumentHashes[document] = hash;
                #else
                m_DocumentHashes[document] = hash.ToString();
                #endif
            }
        }

        internal void AddExactWord(string word, int score, int documentIndex, List<SearchIndexEntry> indexes)
        {
            indexes.Add(new SearchIndexEntry(word.GetHashCode(), int.MaxValue, SearchIndexEntry.Type.Word, documentIndex, score));
        }

        internal void AddWord(string word, int minVariations, int maxVariations, int score, int documentIndex, List<SearchIndexEntry> indexes)
        {
            if (word == null || word.Length == 0)
                return;

            maxVariations = Math.Min(maxVariations, word.Length);

            for (int c = Math.Min(minVariations, maxVariations); c <= maxVariations; ++c)
            {
                var ss = word.Substring(0, c);
                indexes.Add(new SearchIndexEntry(ss.GetHashCode(), ss.Length, SearchIndexEntry.Type.Word, documentIndex, score));
            }

            if (word.Length > maxVariations)
                indexes.Add(new SearchIndexEntry(word.GetHashCode(), word.Length, SearchIndexEntry.Type.Word, documentIndex, score-1));
        }

        private bool ExcludeWordVariations(string word)
        {
            if (word == "true" || word == "false")
                return true;
            return false;
        }

        internal void AddExactProperty(string name, string value, int score, int documentIndex, bool saveKeyword)
        {
            lock (this)
                AddExactProperty(name, value, score, documentIndex, m_BatchIndexes, saveKeyword);
        }

        internal void AddExactProperty(string name, string value, int score, int documentIndex, List<SearchIndexEntry> indexes, bool saveKeyword)
        {
            var nameHash = name.GetHashCode();
            var valueHash = value.GetHashCode();

            // Add an exact match for property="match"
            nameHash ^= name.Length.GetHashCode();
            valueHash ^= value.Length.GetHashCode();
            indexes.Add(new SearchIndexEntry(valueHash, nameHash, SearchIndexEntry.Type.Property, documentIndex, score - 3));

            #if DEBUG_INDEXING
            UnityEngine.Debug.Log($"[E] {name}={value} -> {nameHash}={valueHash}");
            #endif

            if (saveKeyword)
                m_Keywords.Add($"{name}:{value}");
        }

        internal void AddProperty(string name, string value, int minVariations, int maxVariations, int score, int documentIndex, List<SearchIndexEntry> indexes, bool exact, bool saveKeyword)
        {
            var nameHash = name.GetHashCode();
            var valueHash = value.GetHashCode();
            maxVariations = Math.Min(maxVariations, value.Length);
            if (minVariations > value.Length)
                minVariations = value.Length;
            if (ExcludeWordVariations(value))
                minVariations = maxVariations = value.Length;

            #if DEBUG_INDEXING
            UnityEngine.Debug.Log($"[C] {name}:{value} -> {nameHash}:{valueHash}");
            #endif

            for (int c = Math.Min(minVariations, maxVariations); c <= maxVariations; ++c)
            {
                var ss = value.Substring(0, c);
                indexes.Add(new SearchIndexEntry(ss.GetHashCode(), nameHash, SearchIndexEntry.Type.Property, documentIndex, score + (maxVariations - c)));

                #if DEBUG_INDEXING
                UnityEngine.Debug.Log($"[V] {name}:{ss} -> {nameHash}:{ss.GetHashCode()}");
                #endif
            }

            if (value.Length > maxVariations)
            {
                indexes.Add(new SearchIndexEntry(valueHash, nameHash, SearchIndexEntry.Type.Property, documentIndex, score-1));

                #if DEBUG_INDEXING
                UnityEngine.Debug.Log($"[O] {name}:{value} -> {nameHash}:{valueHash}");
                #endif
            }

            if (exact)
            {
                nameHash ^= name.Length.GetHashCode();
                valueHash ^= value.Length.GetHashCode();
                indexes.Add(new SearchIndexEntry(valueHash, nameHash, SearchIndexEntry.Type.Property, documentIndex, score - 3));

                #if DEBUG_INDEXING
                UnityEngine.Debug.Log($"[E] {name}={value} -> {nameHash}={valueHash}");
                #endif
            }

            if (saveKeyword)
                m_Keywords.Add($"{name}:{value}");
            else
                m_Keywords.Add($"{name}:");
        }

        internal void AddNumber(string key, double value, int score, int documentIndex, List<SearchIndexEntry> indexes)
        {
            var keyHash = key.GetHashCode();
            var longNumber = BitConverter.DoubleToInt64Bits(value);
            indexes.Add(new SearchIndexEntry(longNumber, keyHash, SearchIndexEntry.Type.Number, documentIndex, score));

            m_Keywords.Add($"{key}:");
        }

        internal void Start(bool clear = false)
        {
            lock (this)
            {
                m_IndexerThread = null;
                m_ThreadAborted = false;
                m_IndexReady = false;
                m_BatchIndexes.Clear();
                m_FixedRanges.Clear();
                m_PatternMatchCount.Clear();

                if (clear)
                {
                    m_Keywords.Clear();
                    m_Documents.Clear();
                    m_DocumentHashes.Clear();
                    m_Indexes = new SearchIndexEntry[0];
                }
            }
        }

        internal void Finish()
        {
            Finish(null, null, saveBytes: false);
        }

        internal void Finish(Action threadCompletedCallback)
        {
            Finish(bytes => threadCompletedCallback?.Invoke(), null, saveBytes: false);
        }

        internal void Finish(Action threadCompletedCallback, string[] removedDocuments)
        {
            Finish(bytes => threadCompletedCallback?.Invoke(), removedDocuments, saveBytes: false);
        }

        internal void Finish(Action<byte[]> threadCompletedCallback, string[] removedDocuments)
        {
            Finish(threadCompletedCallback, removedDocuments, saveBytes: true);
        }

        internal void Finish(Action<byte[]> threadCompletedCallback, string[] removedDocuments, bool saveBytes)
        {
            m_ThreadAborted = false;
            m_IndexerThread = new Thread(() =>
            {
                try
                {
                    using (new IndexerThreadScope(AbortIndexing))
                    {
                        Finish(removedDocuments);

                        if (threadCompletedCallback != null)
                        {
                            byte[] bytes = null;
                            if (saveBytes)
                                bytes = SaveBytes();
                            Dispatcher.Enqueue(() => threadCompletedCallback(bytes));
                        }
                    }
                }
                catch (ThreadAbortException)
                {
                    m_ThreadAborted = true;
                    Thread.ResetAbort();
                }
            });
            m_IndexerThread.Start();
        }

        internal void Finish(string[] removedDocuments)
        {
            lock (this)
            {
                var shouldRemoveDocuments = removedDocuments != null && removedDocuments.Length > 0;
                if (shouldRemoveDocuments)
                {
                    var removedDocIndexes = new HashSet<int>();
                    foreach (var rd in removedDocuments)
                    {
                        var di = m_Documents.FindIndex(d => d.id == rd);
                        if (di > -1)
                            removedDocIndexes.Add(di);
                    }
                    m_BatchIndexes.AddRange(m_Indexes.Where(e => !removedDocIndexes.Contains(e.index)));
                }
                else
                {
                    m_BatchIndexes.AddRange(m_Indexes);
                }
                UpdateIndexes(m_BatchIndexes, null);
                m_BatchIndexes.Clear();
            }
        }

        internal void Print()
        {
            #if UNITY_2020_1_OR_NEWER
            foreach (var i in m_Indexes)
            {
                UnityEngine.Debug.LogFormat(UnityEngine.LogType.Log, UnityEngine.LogOption.NoStacktrace, null,
                    $"{i.type} - {i.crc} - {i.key} - {i.index} - {i.score}");
            }
            #endif
        }

        private IEnumerable<SearchResult> SearchWord(string word, SearchIndexOperator op, int maxScore, SearchResultCollection subset, int patternMatchLimit)
        {
            var comparer = new SearchIndexComparer(op);
            int crc = word.Length;
            if (op == SearchIndexOperator.Equal)
                crc = int.MaxValue;
            return SearchIndexes(word.GetHashCode(), crc, SearchIndexEntry.Type.Word, maxScore, comparer, subset, patternMatchLimit);
        }

        private IEnumerable<SearchResult> ExcludeWord(string word, SearchIndexOperator op, SearchResultCollection subset)
        {
            if (subset == null)
                subset = GetAllDocumentIndexesSet();

            var includedDocumentIndexes = new SearchResultCollection(SearchWord(word, op, int.MaxValue, null, int.MaxValue));
            return subset.Where(d => !includedDocumentIndexes.Contains(d));
        }

        private IEnumerable<SearchResult> ExcludeProperty(string name, string value, SearchIndexOperator op, int maxScore, SearchResultCollection subset, int limit)
        {
            if (subset == null)
                subset = GetAllDocumentIndexesSet();

            var includedDocumentIndexes = new SearchResultCollection(SearchProperty(name, value, op, int.MaxValue, null, int.MaxValue));
            return subset.Where(d => !includedDocumentIndexes.Contains(d));
        }

        private IEnumerable<SearchResult> SearchProperty(string name, string value, SearchIndexOperator op, int maxScore, SearchResultCollection subset, int patternMatchLimit)
        {
            var comparer = new SearchIndexComparer(op);
            var valueHash = value.GetHashCode();
            var nameHash = name.GetHashCode();
            if (comparer.op == SearchIndexOperator.Equal)
            {
                nameHash ^= name.Length.GetHashCode();
                valueHash ^= value.Length.GetHashCode();
            }

            return SearchIndexes(valueHash, nameHash, SearchIndexEntry.Type.Property, maxScore, comparer, subset, patternMatchLimit);
        }

        private SearchResultCollection GetAllDocumentIndexesSet()
        {
            if (m_AllDocumentIndexes != null)
                return m_AllDocumentIndexes;
            m_AllDocumentIndexes = new SearchResultCollection();
            for (int i = 0; i < documentCount; ++i)
                m_AllDocumentIndexes.Add(new SearchResult(i, 0));
            return m_AllDocumentIndexes;
        }

        private IEnumerable<SearchResult> ExcludeNumber(string name, double number, SearchIndexOperator op, SearchResultCollection subset)
        {
            if (subset == null)
                subset = GetAllDocumentIndexesSet();

            var includedDocumentIndexes = new SearchResultCollection(SearchNumber(name, number, op, int.MaxValue, null).Select(m => new SearchResult(m.index, m.score)));
            return subset.Where(d => !includedDocumentIndexes.Contains(d));
        }

        private IEnumerable<SearchResult> SearchNumber(string key, double value, SearchIndexOperator op, int maxScore, SearchResultCollection subset)
        {
            var wiec = new SearchIndexComparer(op);
            return SearchIndexes(BitConverter.DoubleToInt64Bits(value), key.GetHashCode(), SearchIndexEntry.Type.Number, maxScore, wiec, subset);
        }

        internal IEnumerable<SearchResult> SearchTerm(
            string name, object value, SearchIndexOperator op, bool exclude,
            int maxScore = int.MaxValue, SearchResultCollection subset = null, int limit = int.MaxValue)
        {
            if (op == SearchIndexOperator.NotEqual)
            {
                exclude = true;
                op = SearchIndexOperator.Equal;
            }

            IEnumerable<SearchResult> matches = null;
            if (!String.IsNullOrEmpty(name))
            {
                name = name.ToLowerInvariant();

                // Search property
                double number;
                if (value is double)
                {
                    number = (double)value;
                    matches = SearchNumber(name, number, op, maxScore, subset);
                }
                else if (value is string)
                {
                    var valueString = (string)value;
                    if (double.TryParse(valueString, out number))
                    {
                        if (!exclude && op != SearchIndexOperator.NotEqual)
                            matches = SearchNumber(name, number, op, maxScore, subset);
                        else
                            matches = ExcludeNumber(name, number, op, subset);
                    }
                    else
                    {
                        if (!exclude)
                            matches = SearchProperty(name, valueString.ToLowerInvariant(), op, maxScore, subset, limit);
                        else
                            matches = ExcludeProperty(name, valueString.ToLowerInvariant(), op, maxScore, subset, limit);
                    }
                }
                else
                    throw new ArgumentException($"value must be a number or a string", nameof(value));
            }
            else if (value is string)
            {
                // Search word
                if (!exclude)
                    matches = SearchWord((string)value, op, maxScore, subset, limit);
                else
                    matches = ExcludeWord((string)value, op, subset);
            }
            else
                throw new ArgumentException($"word value must be a string", nameof(value));

            if (matches == null)
                return null;
            return matches.Select(r => new SearchResult(m_Documents[r.index].id, r.index, r.score));
        }

        private int SortTokensByPatternMatches(string item1, string item2)
        {
            m_PatternMatchCount.TryGetValue(item1.GetHashCode(), out var item1PatternMatchCount);
            m_PatternMatchCount.TryGetValue(item2.GetHashCode(), out var item2PatternMatchCount);
            var c = item1PatternMatchCount.CompareTo(item2PatternMatchCount);
            if (c != 0)
                return c;
            return item1.Length.CompareTo(item2.Length);
        }

        private void SaveIndexToDisk(string indexFilePath)
        {
            if (String.IsNullOrEmpty(indexFilePath))
                return;

            var indexTempFilePath = Path.GetTempFileName();
            using (var fileStream = new FileStream(indexTempFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                Write(fileStream);

            try
            {
                try
                {
                    if (File.Exists(indexFilePath))
                        File.Delete(indexFilePath);
                }
                catch (IOException)
                {
                    // ignore file index persistence operation, since it is not critical and will redone later.
                }

                File.Move(indexTempFilePath, indexFilePath);
            }
            catch (IOException)
            {
                // ignore file index persistence operation, since it is not critical and will redone later.
            }
        }

        internal bool ReadIndexFromDisk(string indexFilePath, bool checkVersionOnly = false)
        {
            lock (this)
            {
                using (var fileStream = new FileStream(indexFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    return Read(fileStream, checkVersionOnly);
            }
        }

        internal bool LoadIndexFromDisk(string indexFilePath, bool useThread = false)
        {
            if (indexFilePath == null || !File.Exists(indexFilePath))
                return false;

            if (useThread)
            {
                if (!ReadIndexFromDisk(indexFilePath, true))
                    return false;

                var t = new Thread(() => ReadIndexFromDisk(indexFilePath));
                t.Start();
                return t.ThreadState != System.Threading.ThreadState.Unstarted;
            }

            return ReadIndexFromDisk(indexFilePath);
        }

        private void AbortIndexing()
        {
            if (m_IndexReady)
                return;

            m_ThreadAborted = true;
        }

        private bool UpdateIndexes(List<SearchIndexEntry> entries, Action onIndexesCreated)
        {
            if (entries == null)
                return false;

            lock (this)
            {
                m_IndexReady = false;
                var comparer = new SearchIndexComparer();

                try
                {
                    // Sort word indexes to run quick binary searches on them.
                    entries.Sort(comparer);
                    m_Indexes = entries.Distinct(comparer).ToArray();
                    onIndexesCreated?.Invoke();
                    m_IndexReady = true;
                }
                catch
                {
                    // This can happen while a domain reload is happening.
                    return false;
                }

                return true;
            }
        }

        private void UpdateIndexes(IEnumerable<SearchDocument> documents, List<SearchIndexEntry> entries,
            #if UNITY_2020_1_OR_NEWER
            Dictionary<string, Hash128> hashes
            #else
            Dictionary<string, string> hashes
            #endif
            )
        {
            UpdateIndexes(entries, () =>
            {
                lock (this)
                {
                    m_Documents = documents.ToList();
                    m_DocumentHashes = hashes;
                }
            });
        }

        private void ApplyIndexes(IEnumerable<SearchDocument> documents, SearchIndexEntry[] entries,
            #if UNITY_2020_1_OR_NEWER
            Dictionary<string, Hash128> hashes
            #else
            Dictionary<string, string> hashes
            #endif
        )
        {
            m_Documents = documents.ToList();
            m_DocumentHashes = hashes;
            m_Indexes = entries;
            m_IndexReady = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool NumberCompare(SearchIndexOperator op, double d1, double d2)
        {
            if (op == SearchIndexOperator.Equal)
                return d1 == d2;
            if (op == SearchIndexOperator.Contains)
                return Mathf.Approximately((float)d1, (float)d2);
            if (op == SearchIndexOperator.Greater)
                return d1 > d2;
            if (op == SearchIndexOperator.GreaterOrEqual)
                return d1 >= d2;
            if (op == SearchIndexOperator.Less)
                return d1 < d2;
            if (op == SearchIndexOperator.LessOrEqual)
                return d1 <= d2;

            return false;
        }

        private bool Rewind(int foundIndex, in SearchIndexEntry term, SearchIndexOperator op)
        {
            if (foundIndex <= 0)
                return false;

            var prevEntry =  m_Indexes[foundIndex - 1];
            if (prevEntry.crc != term.crc || prevEntry.type != term.type)
                return false;

            if (term.type == SearchIndexEntry.Type.Number)
                return NumberCompare(op, prevEntry.number, term.number);

            return prevEntry.key == term.key;
        }

        private bool Advance(int foundIndex, in SearchIndexEntry term, SearchIndexOperator op)
        {
            if (foundIndex < 0 || foundIndex >= m_Indexes.Length ||
                    m_Indexes[foundIndex].crc != term.crc || m_Indexes[foundIndex].type != term.type)
                return false;

            if (term.type == SearchIndexEntry.Type.Number)
                return NumberCompare(op, m_Indexes[foundIndex].number, term.number);

            return m_Indexes[foundIndex].key == term.key;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool Lower(ref int foundIndex, in SearchIndexEntry term, SearchIndexOperator op)
        {
            if (op == SearchIndexOperator.Less || op == SearchIndexOperator.LessOrEqual)
            {
                var cont = !Advance(foundIndex, term, op);
                if (cont)
                    foundIndex--;
                return IsIndexValid(foundIndex, term.key, term.type) && cont;
            }

            {
                var cont = Rewind(foundIndex, term, op);
                if (cont)
                    foundIndex--;
                return cont;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool Upper(ref int foundIndex, in SearchIndexEntry term, SearchIndexOperator op)
        {
            if (op == SearchIndexOperator.Less || op == SearchIndexOperator.LessOrEqual)
            {
                var cont = Rewind(foundIndex, term, op);
                if (cont)
                    foundIndex--;
                return IsIndexValid(foundIndex, term.crc, term.type) && cont;
            }

            return Advance(++foundIndex, term, op);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsIndexValid(int foundIndex, long crc, SearchIndexEntry.Type type)
        {
            return foundIndex >= 0 && foundIndex < m_Indexes.Length && m_Indexes[foundIndex].crc == crc && m_Indexes[foundIndex].type == type;
        }

        private IndexRange FindRange(in SearchIndexEntry term, SearchIndexComparer comparer)
        {
            // Find a first match in the sorted indexes.
            int foundIndex = Array.BinarySearch(m_Indexes, term, comparer);
            if (foundIndex < 0 && comparer.op != SearchIndexOperator.Contains && comparer.op != SearchIndexOperator.Equal)
            {
                // Potential range insertion, only used for not exact matches
                foundIndex = (-foundIndex) - 1;
            }

            if (!IsIndexValid(foundIndex, term.crc, term.type))
                return IndexRange.Invalid;

            // Rewind to first element
            while (Lower(ref foundIndex, term, comparer.op))
                ;

            if (!IsIndexValid(foundIndex, term.crc, term.type))
                return IndexRange.Invalid;

            int startRange = foundIndex;

            // Advance to last matching element
            while (Upper(ref foundIndex, term, comparer.op))
                ;

            return new IndexRange(startRange, foundIndex);
        }

        private IndexRange FindTypeRange(int hitIndex, in SearchIndexEntry term)
        {
            if (term.type == SearchIndexEntry.Type.Word)
            {
                if (m_Indexes[0].type != SearchIndexEntry.Type.Word || m_Indexes[hitIndex].type != SearchIndexEntry.Type.Word)
                    return IndexRange.Invalid; // No words

                IndexRange range;
                var rangeSet = new RangeSet(term.type, 0);
                if (m_FixedRanges.TryGetValue(rangeSet, out range))
                    return range;

                int endRange = hitIndex;
                while (m_Indexes[endRange+1].type == SearchIndexEntry.Type.Word)
                    endRange++;

                range = new IndexRange(0, endRange);
                m_FixedRanges[rangeSet] = range;
                return range;
            }
            else if (term.type == SearchIndexEntry.Type.Property || term.type == SearchIndexEntry.Type.Number)
            {
                if (m_Indexes[hitIndex].type != SearchIndexEntry.Type.Property)
                    return IndexRange.Invalid;

                IndexRange range;
                var rangeSet = new RangeSet(term.type, term.crc);
                if (m_FixedRanges.TryGetValue(rangeSet, out range))
                    return range;

                int startRange = hitIndex, prev = hitIndex - 1;
                while (prev >= 0 && m_Indexes[prev].type == SearchIndexEntry.Type.Property && m_Indexes[prev].crc == term.crc)
                    startRange = prev--;

                var indexCount = m_Indexes.Length;
                int endRange = hitIndex, next = hitIndex + 1;
                while (next < indexCount && m_Indexes[next].type == SearchIndexEntry.Type.Property && m_Indexes[next].crc == term.crc)
                    endRange = next++;

                range = new IndexRange(startRange, endRange);
                m_FixedRanges[rangeSet] = range;
                return range;
            }

            return IndexRange.Invalid;
        }

        private IEnumerable<SearchResult> SearchRange(
                int foundIndex, in SearchIndexEntry term,
                int maxScore, SearchIndexComparer comparer,
                SearchResultCollection subset, int limit)
        {
            if (foundIndex < 0 && comparer.op != SearchIndexOperator.Contains && comparer.op != SearchIndexOperator.Equal)
            {
                // Potential range insertion, only used for not exact matches
                foundIndex = (-foundIndex) - 1;
            }

            if (!IsIndexValid(foundIndex, term.crc, term.type))
                return Enumerable.Empty<SearchResult>();

            // Rewind to first element
            while (Lower(ref foundIndex, term, comparer.op))
                ;

            if (!IsIndexValid(foundIndex, term.crc, term.type))
                return Enumerable.Empty<SearchResult>();

            var matches = new List<SearchResult>();
            bool findAll = subset == null;
            do
            {
                var indexEntry = new SearchResult(m_Indexes[foundIndex]);
                #if USE_SORTED_SET
                bool intersects = findAll || (subset.Contains(indexEntry) && subset.TryGetValue(ref indexEntry));
                #else
                bool intersects = findAll || subset.Contains(indexEntry);
                #endif
                if (intersects && indexEntry.score < maxScore)
                {
                    if (term.type == SearchIndexEntry.Type.Number)
                        matches.Add(new SearchResult(indexEntry.index, indexEntry.score + (int)Math.Abs(term.number - m_Indexes[foundIndex].number)));
                    else
                        matches.Add(new SearchResult(indexEntry.index, indexEntry.score));

                    if (matches.Count >= limit)
                        return matches;
                }

                // Advance to last matching element
            } while (Upper(ref foundIndex, term, comparer.op));

            return matches;
        }

        private IEnumerable<SearchResult> SearchIndexes(
                long key, int crc, SearchIndexEntry.Type type, int maxScore,
                SearchIndexComparer comparer, SearchResultCollection subset, int limit = int.MaxValue)
        {
            if (subset != null && subset.Count == 0)
                return Enumerable.Empty<SearchResult>();

            // Find a first match in the sorted indexes.
            var matchKey = new SearchIndexEntry(key, crc, type);
            int foundIndex = Array.BinarySearch(m_Indexes, matchKey, comparer);
            return SearchRange(foundIndex, matchKey, maxScore, comparer, subset, limit);
        }

        private string[] ParseQuery(string query)
        {
            return Regex.Matches(query, @"([\!]*([\""](.+?)[\""]|[^\s_\/]))+").Cast<Match>()
                .Select(m => m.Value.Replace("\"", "").ToLowerInvariant())
                .Where(t => t.Length > 0)
                .OrderBy(t => -t.Length)
                .ToArray();
        }

        readonly struct IndexRange
        {
            public readonly int start;
            public readonly int end;

            public IndexRange(int s, int e)
            {
                start = s;
                end = e;
            }

            public bool valid => start != -1;

            public static IndexRange Invalid = new IndexRange(-1, -1);
        }

        readonly struct RangeSet : IEquatable<RangeSet>
        {
            public readonly SearchIndexEntry.Type type;
            public readonly int crc;

            public RangeSet(SearchIndexEntry.Type type, int crc)
            {
                this.type = type;
                this.crc = crc;
            }

            public override int GetHashCode() => (type, crc).GetHashCode();
            public override bool Equals(object other) => other is RangeSet l && Equals(l);
            public bool Equals(RangeSet other) => type == other.type && crc == other.crc;
        }

        struct IndexerThreadScope : IDisposable
        {
            private bool m_Disposed;
            private readonly AssemblyReloadEvents.AssemblyReloadCallback m_AbortHandler;

            public IndexerThreadScope(AssemblyReloadEvents.AssemblyReloadCallback abortHandler)
            {
                m_Disposed = false;
                m_AbortHandler = abortHandler;
                AssemblyReloadEvents.beforeAssemblyReload -= abortHandler;
                AssemblyReloadEvents.beforeAssemblyReload += abortHandler;
            }

            public void Dispose()
            {
                if (m_Disposed)
                    return;
                AssemblyReloadEvents.beforeAssemblyReload -= m_AbortHandler;
                m_Disposed = true;
            }
        }
    }
}
