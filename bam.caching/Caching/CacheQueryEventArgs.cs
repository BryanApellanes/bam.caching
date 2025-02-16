using Bam.Data;

namespace Bam.Caching
{
    public class CacheQueryEventArgs<T>: EventArgs
    {
        public Type Type { get; set; }

        /// <summary>
        /// The filter used for the query; may be null
        /// in cases where a different query overload
        /// was used not requiring a QueryFilter
        /// </summary>
        public IQueryFilter QueryFilter { get; set; }

        public IEnumerable<T> Results{ get; set; }
    }
}
