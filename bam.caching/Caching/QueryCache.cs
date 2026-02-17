using System.Collections.Concurrent;
using Bam.Data;
using Bam.Data.Repositories;

namespace Bam.Caching
{
    /// <summary>
    /// Caches the results of typed queries, keyed by <see cref="QueryContext"/>.
    /// </summary>
    /// <typeparam name="T">The type of query results.</typeparam>
    public class QueryCache<T> where T : class, new()
    {
        ConcurrentDictionary<QueryContext, IEnumerable<T>> _typedQueryResults;
        /// <summary>
        /// Initializes a new instance of the <see cref="QueryCache{T}"/> class.
        /// </summary>
        public QueryCache()
        {
            _typedQueryResults = new ConcurrentDictionary<QueryContext, IEnumerable<T>>();            
        }

        /// <summary>
        /// Returns cached results for the specified source and filter, querying the source if not cached.
        /// </summary>
        /// <param name="source">The queryable data source.</param>
        /// <param name="filter">The query filter to apply.</param>
        /// <returns>The query results.</returns>
        public IEnumerable<T> Results(IQueryFilterable source, QueryFilter filter)
        {
            QueryContext queryContext = new QueryContext(source, filter);
            if (!_typedQueryResults.TryGetValue(queryContext, out IEnumerable<T>? results))
            {
                results = Reload(queryContext);
            }
            return results;
        }

        /// <summary>
        /// Reloads results from the source for the specified filter, updating the cache.
        /// </summary>
        /// <param name="source">The queryable data source.</param>
        /// <param name="filter">The query filter to apply.</param>
        /// <returns>The refreshed query results.</returns>
        public IEnumerable<T> Reload(IQueryFilterable source, QueryFilter filter)
        {
            return Reload(new QueryContext(source, filter));
        }

        /// <summary>
        /// Reloads results from the source using the specified query context, updating the cache.
        /// </summary>
        /// <param name="queryContext">The query context to reload.</param>
        /// <returns>The refreshed query results.</returns>
        public IEnumerable<T> Reload(QueryContext queryContext)
        {
            IEnumerable<T> results = queryContext.Retrieve<T>();
            _typedQueryResults.TryAdd(queryContext, results);
            return results;
        }
    }

    /// <summary>
    /// Caches the results of non-generic queries, keyed by <see cref="QueryContext"/>.
    /// </summary>
    public class QueryCache
    {
        ConcurrentDictionary<QueryContext, IEnumerable<object>> _queryResults;
        /// <summary>
        /// Initializes a new instance of the <see cref="QueryCache"/> class.
        /// </summary>
        public QueryCache()
        {
            _queryResults = new ConcurrentDictionary<QueryContext, IEnumerable<object>>();
        }

        /// <summary>
        /// Returns cached results for the specified type, source, and filter, querying the source if not cached.
        /// </summary>
        /// <param name="type">The type of items to query.</param>
        /// <param name="source">The queryable data source.</param>
        /// <param name="filter">The query filter to apply.</param>
        /// <returns>The query results.</returns>
        public IEnumerable<object> Results(Type type, IQueryFilterable source, QueryFilter filter)
        {
            QueryContext queryContext = new QueryContext(source, filter);
            if (!_queryResults.TryGetValue(queryContext, out IEnumerable<object>? results))
            {
                results = Reload(type, queryContext);
            }
            return results;
        }

        /// <summary>
        /// Reloads results from the source for the specified type and filter, updating the cache.
        /// </summary>
        /// <param name="type">The type of items to query.</param>
        /// <param name="source">The queryable data source.</param>
        /// <param name="filter">The query filter to apply.</param>
        /// <returns>The refreshed query results.</returns>
        public IEnumerable<object> Reload(Type type, IQueryFilterable source, QueryFilter filter)
        {
            return Reload(type, new QueryContext(source, filter));
        }

        /// <summary>
        /// Occurs before a reload operation begins.
        /// </summary>
        public event EventHandler<QueryCacheEventArgs> Reloading = null!;

        /// <summary>
        /// Occurs after a reload operation completes.
        /// </summary>
        public event EventHandler<QueryCacheEventArgs> Reloaded = null!;

        /// <summary>
        /// Reloads results from the source using the specified type and query context, updating the cache.
        /// </summary>
        /// <param name="type">The type of items to query.</param>
        /// <param name="queryContext">The query context to reload.</param>
        /// <returns>The refreshed query results.</returns>
        public IEnumerable<object> Reload(Type type, QueryContext queryContext)
        {
            Reloading?.Invoke(this, new QueryCacheEventArgs { QueryContext = queryContext });
            IEnumerable<object> results = queryContext.Retrieve(type);
            _queryResults.TryAdd(queryContext, results);
            Reloaded?.Invoke(this, new QueryCacheEventArgs { QueryContext = queryContext });
            return results;
        }
    }
}
