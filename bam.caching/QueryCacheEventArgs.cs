namespace Bam.Caching
{
    /// <summary>
    /// Event arguments for <see cref="QueryCache"/> reload events, containing the query context.
    /// </summary>
    public class QueryCacheEventArgs: EventArgs
    {
        /// <summary>
        /// Gets or sets the query context associated with the reload event.
        /// </summary>
        public QueryContext QueryContext { get; set; } = null!;
    }
}
