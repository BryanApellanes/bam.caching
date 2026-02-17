/*
	Copyright © Bryan Apellanes 2015  
*/

using System.Collections.Concurrent;
using Bam.Logging;

namespace Bam.Caching
{
    /// <summary>
    /// Manages a collection of per-type <see cref="Cache"/> instances with a configurable maximum total cache size.
    /// </summary>
    public class CacheManager : Loggable, ICacheManager
    {
	    readonly ConcurrentDictionary<Type, Cache> _cacheDictionary;
        /// <summary>
        /// Initializes a new instance of the <see cref="CacheManager"/> class with the specified maximum cache size.
        /// </summary>
        /// <param name="maxCacheSizeBytes">The maximum cache size in bytes for each type cache. Defaults to 500 MB.</param>
		public CacheManager(uint maxCacheSizeBytes = 524288000) // 500 megabytes
        {
			_cacheDictionary = new ConcurrentDictionary<Type, Cache>();
            MaxCacheSizeBytes = maxCacheSizeBytes;
		}

        /// <summary>
        /// Removes all caches from this cache manager.
        /// </summary>
        public void Clear()
        {
            _cacheDictionary.Clear();
        }

        /// <summary>
        /// Gets or sets the maximum size in bytes for each individual type cache.
        /// </summary>
        public uint MaxCacheSizeBytes { get; set; }

        /// <summary>
        /// Gets the total memory size in bytes across all managed caches.
        /// </summary>
		public uint AllCacheSize
		{
			get
			{
				return (uint)_cacheDictionary.Values.Sum(c => c.ItemsMemorySize);
			}
		}

		object _getLock = new object();
        /// <summary>
        /// Gets the cache for the specified type, creating one if it does not exist.
        /// </summary>
        /// <typeparam name="T">The type to get the cache for.</typeparam>
        /// <returns>The <see cref="Cache"/> instance for the specified type.</returns>
		public Cache CacheFor<T>()
		{
			return CacheFor(typeof(T));
		}

        /// <summary>
        /// Sets the cache for the specified type, replacing any existing cache.
        /// </summary>
        /// <typeparam name="T">The type to set the cache for.</typeparam>
        /// <param name="cache">The cache instance to use for the specified type.</param>
		public void CacheFor<T>(Cache cache)
		{
			CacheFor(typeof(T), cache);
		}

        /// <summary>
        /// Occurs when items are evicted from any managed cache.
        /// </summary>
        public event EventHandler Evicted = null!;

        /// <summary>
        /// Occurs when retrieving a cache for a type fails.
        /// </summary>
        [Verbosity(LogEventType.Warning, SenderMessageFormat = "Failed to get Cache for type {TypeName}")]
        public event EventHandler GetCacheFailed = null!;

        /// <summary>
        /// Checks for a cache for the specified type setting it to the
        /// return value of cacheProvider if its not present.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="cacheProvider"></param>
        protected void EnsureCache(Type type, Func<Cache> cacheProvider)
        {
            if (!_cacheDictionary.ContainsKey(type))
            {
                Cache cache = cacheProvider();
                _cacheDictionary.TryAdd(type, cache);
                FireEvent(CacheSet, new CacheManagerEventArgs { Type = type, Cache = cache });
            }
        }

        private void OnEvicted(object? sender, EventArgs args)
        {
            Evicted?.Invoke(sender, args);
        }

        /// <summary>
        /// Occurs when a cache for a type is removed.
        /// </summary>
        [Verbosity(LogEventType.Information, SenderMessageFormat = "Removed Cache for type {TypeName}")]
        public event EventHandler CacheRemoved = null!;

        /// <summary>
        /// Occurs when a cache for a type is set.
        /// </summary>
        [Verbosity(LogEventType.Information, SenderMessageFormat = "Set Cache for type {TypeName}")]
        public event EventHandler CacheSet = null!;

        /// <summary>
        /// Gets or creates a typed cache using the specified cache provider function.
        /// </summary>
        /// <typeparam name="TCached">The type to cache.</typeparam>
        /// <param name="cacheProvider">A function that creates the cache if one does not exist.</param>
        /// <returns>The <see cref="Cache{TCached}"/> instance.</returns>
        public Cache<TCached> CacheFor<TCached>(Func<Cache<TCached>> cacheProvider) where TCached : IMemorySize, new()
        {
            Cache<TCached> cache = cacheProvider();
            CacheFor(typeof(TCached), cache);
            return cache;
        }

        /// <summary>
        /// Gets the cache for the specified type, creating one if it does not exist.
        /// </summary>
        /// <param name="type">The type to get the cache for.</param>
        /// <returns>The <see cref="Cache"/> instance, or null if retrieval fails.</returns>
        public Cache CacheFor(Type type)
        {
            EnsureCache(type, () => new Cache(type.Name, MaxCacheSizeBytes, true, OnEvicted));
            if (!_cacheDictionary.TryGetValue(type, out Cache? result))
            {
                FireEvent(GetCacheFailed, new CacheManagerEventArgs { Type = type });
            }
            return result!;
        }

        /// <summary>
        /// Sets the cache for the specified type, replacing any existing cache. The cache name and max bytes are set from this manager.
        /// </summary>
        /// <param name="type">The type to set the cache for.</param>
        /// <param name="cache">The cache instance to use.</param>
        public void CacheFor(Type type, Cache cache)
        {
            if (_cacheDictionary.TryRemove(type, out Cache? removed))
            {
                FireEvent(CacheRemoved, new CacheManagerEventArgs { Type = type, Cache = removed });
            }
            cache.Name = type.Name;
            cache.MaxBytes = MaxCacheSizeBytes;
            EnsureCache(type, () => cache);            
        }

		static CacheManager _defaultCacheManager = null!;
		static readonly object _defaultCacheManagerLock = new object();
        /// <summary>
        /// Gets the default singleton <see cref="CacheManager"/> instance.
        /// </summary>
		public static CacheManager Default
		{
			get
			{
				return _defaultCacheManagerLock.DoubleCheckLock(ref _defaultCacheManager, () => new CacheManager());
			}
		}
		
        /// <summary>
        /// Gets a factory function that creates a default <see cref="Cache{T}"/> for the specified type.
        /// </summary>
        /// <typeparam name="T">The type of items to cache.</typeparam>
        /// <returns>A function that creates a new <see cref="Cache{T}"/> with default settings.</returns>
		public Func<Cache<T>> GetDefaultCacheProvider<T>() where T : IMemorySize, new()
		{
			return () => new Cache<T>(typeof(T).Name, MaxCacheSizeBytes, true, OnEvicted);
		}
	}
}
