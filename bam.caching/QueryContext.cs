using Bam.Data;
using Bam.Data.Repositories;

namespace Bam.Caching
{
    /// <summary>
    /// Encapsulates a data source and query filter pair, used as a cache key for query results.
    /// </summary>
    public class QueryContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="QueryContext"/> class.
        /// </summary>
        /// <param name="datasource">The queryable data source.</param>
        /// <param name="filter">The query filter to apply.</param>
        public QueryContext(IQueryFilterable datasource, QueryFilter filter)
        {
            DataSource = datasource;
            Filter = filter;
        }
        /// <summary>
        /// Gets or sets the query filter.
        /// </summary>
        public QueryFilter Filter { get; set; }

        /// <summary>
        /// Gets or sets the queryable data source.
        /// </summary>
        public IQueryFilterable DataSource { get; set; }

        /// <summary>
        /// Executes the query filter against the data source and returns typed results.
        /// </summary>
        /// <typeparam name="T">The type of results to retrieve.</typeparam>
        /// <returns>The query results.</returns>
        public IEnumerable<T> Retrieve<T>() where T : class, new()
        {
            return DataSource.Query<T>(this.Filter);
        }

        public IEnumerable<object> Retrieve(Type type)
        {
            return DataSource.Query(type, Filter);
        }

        public override bool Equals(object? obj)
        {
            QueryContext? ctx = obj as QueryContext;
            if(ctx != null)
            {
                return ctx.Filter.Equals(Filter) & ctx.DataSource.Equals(DataSource);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return Filter.GetHashCode() + DataSource.GetHashCode();
        }
    }
}
