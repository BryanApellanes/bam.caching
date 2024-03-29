/*
	Copyright © Bryan Apellanes 2015  
*/
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bam.Net.Data.Repositories;
using Bam.Net.Logging;

namespace Bam.Net.Caching
{
    public class CacheManager : Loggable, ICacheManager
    {
	    readonly ConcurrentDictionary<Type, Cache> _cacheDictionary;
		public CacheManager(uint maxCacheSizeBytes = 524288000) // 500 megabytes
        {
			_cacheDictionary = new ConcurrentDictionary<Type, Cache>();
            MaxCacheSizeBytes = maxCacheSizeBytes;
		}

        public void Clear()
        {
            _cacheDictionary.Clear();
        }

        public uint MaxCacheSizeBytes { get; set; }

		public uint AllCacheSize
		{
			get
			{
				return (uint)_cacheDictionary.Values.Sum(c => c.ItemsMemorySize);
			}
		}

		object _getLock = new object();
		public Cache CacheFor<T>()
		{
			return CacheFor(typeof(T));
		}

		public void CacheFor<T>(Cache cache)
		{
			CacheFor(typeof(T), cache);
		}

        public event EventHandler Evicted;

        [Verbosity(LogEventType.Warning, SenderMessageFormat = "Failed to get Cache for type {TypeName}")]
        public event EventHandler GetCacheFailed;

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

        private void OnEvicted(object sender, EventArgs args)
        {
            Evicted?.Invoke(sender, args);
        }

        [Verbosity(LogEventType.Information, SenderMessageFormat = "Removed Cache for type {TypeName}")]
        public event EventHandler CacheRemoved;
        [Verbosity(LogEventType.Information, SenderMessageFormat = "Set Cache for type {TypeName}")]
        public event EventHandler CacheSet;
        
        public Cache<TCached> CacheFor<TCached>(Func<Cache<TCached>> cacheProvider) where TCached : IMemorySize, new()
        {
            Cache<TCached> cache = cacheProvider();
            CacheFor(typeof(TCached), cache);
            return cache;
        }

        public Cache CacheFor(Type type)
        {
            EnsureCache(type, () => new Cache(type.Name, MaxCacheSizeBytes, true, OnEvicted));
            if (!_cacheDictionary.TryGetValue(type, out Cache result))
            {
                FireEvent(GetCacheFailed, new CacheManagerEventArgs { Type = type });
            }
            return result;
        }

        public void CacheFor(Type type, Cache cache)
        {
            if (_cacheDictionary.TryRemove(type, out Cache removed))
            {
                FireEvent(CacheRemoved, new CacheManagerEventArgs { Type = type, Cache = removed });
            }
            cache.Name = type.Name;
            cache.MaxBytes = MaxCacheSizeBytes;
            EnsureCache(type, () => cache);            
        }

		static CacheManager _defaultCacheManager;
		static readonly object _defaultCacheManagerLock = new object();
		public static CacheManager Default
		{
			get
			{
				return _defaultCacheManagerLock.DoubleCheckLock(ref _defaultCacheManager, () => new CacheManager());
			}
		}
		
		public Func<Cache<T>> GetDefaultCacheProvider<T>() where T : IMemorySize, new()
		{
			return () => new Cache<T>(typeof(T).Name, MaxCacheSizeBytes, true, OnEvicted);
		}
	}
}
