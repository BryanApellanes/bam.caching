namespace Bam.Caching
{
    public class QueryCacheEventArgs: EventArgs
    {
        public QueryContext QueryContext { get; set; }
    }
}
