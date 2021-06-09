
namespace Unity.QuickSearch
{
    /// <summary>
    /// Represents a token of a query string.
    /// </summary>
    internal readonly struct QueryToken
    {
        /// <summary>
        /// The text representing the token.
        /// </summary>
        public string text { get; }

        /// <summary>
        /// The position of the token in the entire query string.
        /// </summary>
        public int position { get; }

        /// <summary>
        /// The length of the token. Can be different than the length of the text.
        /// </summary>
        public int length { get; }

        /// <summary>
        /// Creates a token from a string and a position.
        /// </summary>
        /// <param name="text">The value of the token.</param>
        /// <param name="position">The position of the token in the entire query string.</param>
        public QueryToken(string text, int position)
        {
            this.text = text;
            this.position = position;
            this.length = text.Length;
        }

        /// <summary>
        /// Creates a token from a string, a position and a length.
        /// </summary>
        /// <param name="text">The value of the token.</param>
        /// <param name="position">The position of the token in the entire query string.</param>
        /// <param name="length">The length of the token.</param>
        public QueryToken(string text, int position, int length)
            : this(text, position)
        {
            this.length = length;
        }
    }
}
