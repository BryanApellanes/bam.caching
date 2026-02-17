namespace Bam.Caching
{
    /// <summary>
    /// Defines operations for managing a collection of per-type caches.
    /// </summary>
    public interface ICacheManager
    {
        /// <summary>
        /// Gets the total memory size in bytes across all managed caches.
        /// </summary>
        uint AllCacheSize { get; }

        /// <summary>
        /// Occurs when a cache for a type is removed.
        /// </summary>
        event EventHandler CacheRemoved;

        /// <summary>
        /// Occurs when a cache for a type is set.
        /// </summary>
        event EventHandler CacheSet;

        /// <summary>
        /// Occurs when retrieving a cache for a type fails.
        /// </summary>
        event EventHandler GetCacheFailed;

        /// <summary>
        /// Gets the cache for the specified type, creating one if it does not exist.
        /// </summary>
        /// <param name="type">The type to get the cache for.</param>
        /// <returns>The <see cref="Cache"/> instance.</returns>
        Cache CacheFor(Type type);

        /// <summary>
        /// Sets the cache for the specified type.
        /// </summary>
        /// <param name="type">The type to set the cache for.</param>
        /// <param name="cache">The cache instance to use.</param>
        void CacheFor(Type type, Cache cache);

        /// <summary>
        /// Gets the cache for the specified type, creating one if it does not exist.
        /// </summary>
        /// <typeparam name="T">The type to get the cache for.</typeparam>
        /// <returns>The <see cref="Cache"/> instance.</returns>
        Cache CacheFor<T>();

        /// <summary>
        /// Sets the cache for the specified type.
        /// </summary>
        /// <typeparam name="T">The type to set the cache for.</typeparam>
        /// <param name="cache">The cache instance to use.</param>
        void CacheFor<T>(Cache cache);

        /// <summary>
        /// Removes all caches from this manager.
        /// </summary>
        void Clear();
    }
}