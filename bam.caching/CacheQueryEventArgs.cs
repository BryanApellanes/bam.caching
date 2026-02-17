using Bam.Data;

namespace Bam.Caching
{
    /// <summary>
    /// Event arguments for cache query events, containing the query filter and results.
    /// </summary>
    /// <typeparam name="T">The type of query results.</typeparam>
    public class CacheQueryEventArgs<T>: EventArgs
    {
        /// <summary>
        /// Gets or sets the type that was queried.
        /// </summary>
        public Type Type { get; set; } = null!;

        /// <summary>
        /// The filter used for the query; may be null
        /// in cases where a different query overload
        /// was used not requiring a QueryFilter
        /// </summary>
        public IQueryFilter QueryFilter { get; set; } = null!;

        /// <summary>
        /// Gets or sets the results of the query.
        /// </summary>
        public IEnumerable<T> Results{ get; set; } = null!;
    }
}
