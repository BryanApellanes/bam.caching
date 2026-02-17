/*
	Copyright © Bryan Apellanes 2015  
*/

using Bam.Logging;
using Bam.Data.Repositories;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Bam.Caching
{
    /// <summary>
    /// A typed object cache that stores items implementing <see cref="IMemorySize"/> with automatic memory management.
    /// </summary>
    /// <typeparam name="T">The type of items to cache. Must implement <see cref="IMemorySize"/> and have a parameterless constructor.</typeparam>
    /// <seealso cref="Bam.Caching.Cache" />
    public class Cache<T>: Cache where T: IMemorySize, new()
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Cache{T}"/> class.
        /// </summary>
        public Cache() : base() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="Cache{T}"/> class with the specified configuration.
        /// </summary>
        /// <param name="name">The name of the cache.</param>
        /// <param name="maxBytes">The maximum size of the cache in bytes.</param>
        /// <param name="groomInBackground">If true, starts a background thread to evict items when the cache exceeds max size.</param>
        /// <param name="evictionListener">An optional event handler invoked when items are evicted.</param>
        public Cache(string name, uint maxBytes, bool groomInBackground, EventHandler evictionListener = null!)
			: base(name, maxBytes, groomInBackground, evictionListener)
        {
        }
        
        /// <summary>
        /// Adds the specified values.
        /// </summary>
        /// <param name="values">The values.</param>
        /// <returns></returns>
        public CacheItem<T> Add(params T[] values)
        {
            return (CacheItem<T>)Add<T>(values);
        }

        /// <summary>
        /// Adds the specified values to the cache as typed cache items.
        /// </summary>
        /// <typeparam name="TCacheItem">The type of items to add.</typeparam>
        /// <param name="values">The values to add to the cache.</param>
        /// <returns>An enumerable of <see cref="CacheItem{TCacheItem}"/> for each newly added item.</returns>
        public new IEnumerable<CacheItem<TCacheItem>> Add<TCacheItem>(params TCacheItem[] values) where TCacheItem: IMemorySize, new()
        {
            List<CacheItem> results = new List<CacheItem>();
            HashSet<CacheItem> itemsCopy = new HashSet<CacheItem>(Items);
            foreach (TCacheItem value in values)
            {
                CacheItem<TCacheItem> item = new CacheItem<TCacheItem>(value, MetaProvider);
                if (itemsCopy.Add(item))
                {
                    yield return item;
                }
            }
            Items = itemsCopy;
            Organize();
            GroomerSignal.Set();
        }

        /// <summary>
        /// Gets the default cache for the type <typeparamref name="T"/> from the default <see cref="CacheManager"/>.
        /// </summary>
        /// <returns>The <see cref="Cache{T}"/> instance.</returns>
        public static Cache<T> Get()
        {
	        return CacheManager.Default.CacheFor<T>(CacheManager.Default.GetDefaultCacheProvider<T>());
        }
    }

    /// <summary>
    /// An object cache
    /// </summary>
    /// <seealso cref="Bam.Caching.Cache" />
    public class Cache: Loggable
	{
		bool _keepGrooming;
		Thread _groomerThread = null!;
		readonly ConcurrentQueue<CacheItem> _evictionQueue;

        /// <summary>
        /// Initializes a new instance of the <see cref="Cache"/> class.
        /// </summary>
        public Cache() : this(true) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="Cache"/> class.
        /// </summary>
        /// <param name="groomInBackground">if set to <c>true</c> [groom in background].</param>
        public Cache(bool groomInBackground)
			: this(System.Guid.NewGuid().ToString(), groomInBackground)
		{ }

        /// <summary>
        /// Initializes a new instance of the <see cref="Cache"/> class.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="groomInBackground">if set to <c>true</c> [groom in background].</param>
        public Cache(string name, bool groomInBackground)
			: this(name, 524288, groomInBackground) // 512 kilobytes
		{ }

        /// <summary>
        /// Initializes a new instance of the <see cref="Cache"/> class.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="maxBytes">The maximum bytes.</param>
        /// <param name="groomInBackground">if set to <c>true</c> [groom in background].</param>
        /// <param name="evictionListener">The eviction listener.</param>
        public Cache(string name, uint maxBytes, bool groomInBackground, EventHandler evictionListener = null!)
		{
			Items = new HashSet<CacheItem>();
			ItemsByHits = new SortedSet<CacheItem>();
			ItemsByMisses = new SortedSet<CacheItem>();
			ItemsById = new Dictionary<ulong, CacheItem>();
            ItemsByUuid = new Dictionary<string, CacheItem>();
            ItemsByCuid = new Dictionary<string, CacheItem>();
            ItemsByName = new Dictionary<string, CacheItem>();
			MetaProvider = Bam.Data.Repositories.MetaProvider.Default;
			
			Name = name;
			MaxBytes = maxBytes;

			_keepGrooming = true;
			GroomerSignal = new AutoResetEvent(false);
			_evictionQueue = new ConcurrentQueue<CacheItem>();
			
            if(evictionListener != null)
            {
                this.SubscribeOnce(nameof(Evicted), evictionListener);
            }
			if (groomInBackground)
			{
				StartGroomer();
			}
		}

        /// <summary>
        /// Gets the default cache for the specified type from the default <see cref="CacheManager"/>.
        /// </summary>
        /// <typeparam name="T">The type of items in the cache.</typeparam>
        /// <returns>The <see cref="Cache{T}"/> instance.</returns>
        public static Cache<T> For<T>() where T: IMemorySize, new()
        {
	        return Cache<T>.Get();
        }
        
        /// <summary>
        /// Gets or sets the meta provider.
        /// </summary>
        /// <value>
        /// The meta provider.
        /// </value>
        public IMetaProvider MetaProvider { get; set; }

        /// <summary>
        /// Gets or sets the items.
        /// </summary>
        /// <value>
        /// The items.
        /// </value>
        protected HashSet<CacheItem> Items { get; set; }

        /// <summary>
        /// Gets the items by hits.
        /// </summary>
        /// <value>
        /// The items by hits.
        /// </value>
        protected SortedSet<CacheItem> ItemsByHits { get; private set; }

        /// <summary>
        /// Gets the items by misses.
        /// </summary>
        /// <value>
        /// The items by misses.
        /// </value>
        protected SortedSet<CacheItem> ItemsByMisses { get; private set; }

        /// <summary>
        /// Gets or sets the items by identifier.
        /// </summary>
        /// <value>
        /// The items by identifier.
        /// </value>
        protected Dictionary<ulong, CacheItem> ItemsById { get; set; }

        /// <summary>
        /// Gets or sets the items keyed by UUID.
        /// </summary>
        /// <value>
        /// The items by UUID.
        /// </value>
        protected Dictionary<string, CacheItem> ItemsByUuid { get; set; }

        /// <summary>
        /// Gets or sets the items keyed by cuid.
        /// </summary>
        /// <value>
        /// The items by cuid.
        /// </value>
        protected Dictionary<string, CacheItem> ItemsByCuid { get; set; }

        /// <summary>
        /// Gets or sets the items keyed by name.
        /// </summary>
        /// <value>
        /// The name of the items by.
        /// </value>
        protected Dictionary<string, CacheItem> ItemsByName { get; set; }

        /// <summary>
        /// Gets the groomer signal.
        /// </summary>
        /// <value>
        /// The groomer signal.
        /// </value>
        protected AutoResetEvent GroomerSignal { get; }

        /// <summary>
        /// Gets or sets the maximum bytes.
        /// </summary>
        /// <value>
        /// The maximum bytes.
        /// </value>
        public uint MaxBytes { get; set; }

        /// <summary>
        /// Retrieves the specified instance using the Meta.Uuid.
        /// </summary>
        /// <param name="instance">The instance.</param>
        /// <returns>CacheItem</returns>
        public CacheItem Retrieve(object instance)
        {
            Meta meta = MetaProvider.GetMeta(instance);
            return Retrieve(meta.Uuid);
        }

        /// <summary>
        /// Retrieves the item with the specified identifier.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <returns>CacheItem</returns>
        public CacheItem Retrieve(ulong id)
        {
            if (ItemsById.TryGetValue(id, out CacheItem? result))
            {
                result.IncrementHits();
            }
            return result!;
        }

        /// <summary>
        /// Retrieves the specified UUID.
        /// </summary>
        /// <param name="uuid">The UUID.</param>
        /// <returns>CacheItem</returns>
        public CacheItem Retrieve(string uuid)
        {
            if (ItemsByUuid.TryGetValue(uuid, out CacheItem? result))
            {
                result.IncrementHits();
            }

            return result!;
        }

        /// <summary>
        /// Retrieves the instance of T with the specified name using the specified
        /// sourceRetriever if it is not currently in the cache.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="name">The name.</param>
        /// <param name="sourceRetriever">The source retriever.</param>
        /// <returns>T</returns>
        public virtual T RetrieveByName<T>(string name, Func<T> sourceRetriever)
        {
            CacheItem item = RetrieveByName(name);
            if(item == null)
            {
                item = new CacheItem(sourceRetriever()!, MetaProvider);
                ItemsByName.Add(name, item);
            }

            return item.ValueAs<T>();
        }

        /// <summary>
        /// Retrieves the CacheItem by the specified name.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns>CacheItem</returns>
        public CacheItem RetrieveByName(string name)
        {
            if (ItemsByName.TryGetValue(name, out CacheItem? result))
            {
                result.IncrementHits();
            }

            return result!;
        }

        /// <summary>
        /// Retrieves the CacheItem by the specified cuid.
        /// </summary>
        /// <param name="cuid">The cuid.</param>
        /// <returns></returns>
        public CacheItem RetrieveByCuid(string cuid)
        {
            if (ItemsByCuid.TryGetValue(cuid, out CacheItem? result))
            {
                result.IncrementHits();
            }
            return result!;
        }

        /// <summary>
        /// Adds the specified value.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>CacheItem</returns>
        public virtual CacheItem Add(object value)
        {
            HashSet<CacheItem> itemsCopy = new HashSet<CacheItem>(Items);
            CacheItem item = new CacheItem(value, MetaProvider);
            itemsCopy.Add(item);
            Items = itemsCopy;
            Organize();
            GroomerSignal.Set();
            return item;
        }

        /// <summary>
        /// Adds all items from the specified enumerable to the cache.
        /// </summary>
        /// <typeparam name="T">The type of items to add.</typeparam>
        /// <param name="values">The items to add.</param>
        /// <returns>An enumerable of <see cref="CacheItem"/> instances for each newly added item.</returns>
		public virtual IEnumerable<CacheItem> Add<T>(IEnumerable<T> values)
		{
			return Add(values.ToArray());
		}

        /// <summary>
        /// Add each of the specified values to the cache yielding the
        /// resulting CacheItems
        /// </summary>
        /// <param name="values"></param>
        /// <returns></returns>
        public virtual IEnumerable<CacheItem> Add<T>(params T[] values)
        {
            HashSet<CacheItem> itemsCopy = new HashSet<CacheItem>(Items);
            foreach (object value in values!)
            {
                CacheItem item = new CacheItem(value!, MetaProvider);
                if (itemsCopy.Add(item))
                {
                    yield return item;
                }
            }
            Items = itemsCopy;
            Organize();
            GroomerSignal.Set();
        }

        /// <summary>
        /// Queries the cache using a predicate on item values. Matching items have their hit count incremented; non-matching items have their miss count incremented.
        /// </summary>
        /// <param name="predicate">The predicate to test each cached value against.</param>
        /// <returns>An enumerable of matching <see cref="CacheItem"/> instances.</returns>
        public IEnumerable<CacheItem> Query(Predicate<object> predicate)
        {
            HashSet<CacheItem> itemsCopy = new HashSet<CacheItem>(Items);
            foreach(CacheItem ci in itemsCopy)
            {
                if (predicate(ci.Value))
                {
                    ci.IncrementHits();
                    yield return ci;
                }
                else
                {
                    ci.IncrementMisses();
                }
            }
        }

        /// <summary>
        /// Queries the cache using a predicate on cache items. Matching items have their hit count incremented; non-matching items have their miss count incremented.
        /// </summary>
        /// <param name="predicate">The predicate to test each cache item against.</param>
        /// <returns>An enumerable of matching <see cref="CacheItem"/> instances.</returns>
        public IEnumerable<CacheItem> Query(Func<CacheItem, bool> predicate)
        {
            HashSet<CacheItem> itemsCopy = new HashSet<CacheItem>(Items);
            foreach (CacheItem ci in itemsCopy)
            {
                if (predicate(ci))
                {
                    ci.IncrementHits();
                    yield return ci;
                }
                else
                {
                    ci.IncrementMisses();
                }
            }
        }

        /// <summary>
        /// Queries the cache for items of the specified type matching the predicate. Hit and miss counts are tracked.
        /// </summary>
        /// <typeparam name="T">The type to cast cached values to.</typeparam>
        /// <param name="predicate">The predicate to test each value against.</param>
        /// <returns>An enumerable of matching values of type <typeparamref name="T"/>.</returns>
        public IEnumerable<T> Query<T>(Func<T, bool> predicate)
        {
            HashSet<CacheItem> itemsCopy = new HashSet<CacheItem>(Items);
            foreach(CacheItem ci in itemsCopy)
            {
                if (predicate((T)ci.Value))
                {
                    ci.IncrementHits();
                    yield return (T)ci.Value;
                }
                else
                {
                    ci.IncrementMisses();
                }
            }
        }

        /// <summary>
        /// Queries the cache for items matching the predicate, falling back to the source retriever if no results are found. Optionally refreshes the cache from source in the background.
        /// </summary>
        /// <typeparam name="T">The type to cast cached values to.</typeparam>
        /// <param name="predicate">The predicate to test each value against.</param>
        /// <param name="sourceRetriever">A function that retrieves items from the backing source if the cache has no matches.</param>
        /// <param name="refresh">If true and cache results were found, refreshes the cache from source asynchronously.</param>
        /// <returns>An enumerable of matching values of type <typeparamref name="T"/>.</returns>
        public IEnumerable<T> Query<T>(Func<T, bool> predicate, Func<IEnumerable<T>> sourceRetriever, bool refresh = true)
        {
            IEnumerable<T> results = Query<T>(predicate);
            if (!results.Any())
            {
                results = sourceRetriever();
                Add(results);
            }
            else if(refresh)
            {
                Task.Run(() => Add(sourceRetriever())); // refreshes the cache from source, results are one query behind fresh
            }
            return results;
        }

        /// <summary>
        /// Occurs when items are evicted from the cache.
        /// </summary>
        [Verbosity(VerbosityLevel.Information, SenderMessageFormat="Evicted ({LastEvictionCount}) items from cache named {Name}")]
		public event EventHandler Evicted = null!;

        /// <summary>
        /// Gets the number of items evicted in the most recent eviction operation.
        /// </summary>
		public int LastEvictionCount
		{
			get;
			private set;
		}

        /// <summary>
        /// Gets or sets the name of this cache.
        /// </summary>
		public string Name
		{
			get;
			set;
		}

        /// <summary>
        /// Gets the total memory size in bytes of all items currently in the cache.
        /// </summary>
		public uint ItemsMemorySize
		{
			get
			{
                HashSet<CacheItem> itemsCopy = new HashSet<CacheItem>(Items);
				return (uint)itemsCopy.Select(c => c.MemorySize).Sum();
			}
		}

		/// <summary>
		/// Start grooming in a background thread;
		/// </summary>
		public void StartGroomer()
		{
			Action<dynamic> groomer = (ctx) =>
			{
				ctx.Cache.Groomer();
			};
			
			if(_groomerThread != null && _groomerThread.ThreadState == System.Threading.ThreadState.Running)
			{
				StopGrooming();
			}

			_groomerThread = groomer.ExecuteInThread(new { Cache = this });

			AppDomain.CurrentDomain.DomainUnload += (o, e) =>
			{
				if (_groomerThread.ThreadState == System.Threading.ThreadState.Running)
				{
					StopGrooming();
				}
			};
		}

		/// <summary>
		/// Evict items from the cache that cause the cache to be larger
		/// than MaxBytes and evict anything in the eviction queue
		/// </summary>
		public void Groom()
		{
			if(_keepGrooming)
			{
				int evictableTailCount = GetEvictableTailCount();
				if (evictableTailCount > 0)
				{
					Evict(evictableTailCount);
				}

				if (_evictionQueue.Count > 0)
				{
					lock(_queueLock)
					{
						while (_evictionQueue.Count > 0)
						{
                            if (_evictionQueue.TryDequeue(out CacheItem? item))
                            {
                                Evict(item);
                            }
                        }
					}
				}
			}
		}

		readonly object _queueLock = new object();
        /// <summary>
        /// Queues a cache item for eviction by its numeric ID. The item will be evicted on the next groom cycle.
        /// </summary>
        /// <param name="id">The numeric ID of the item to evict.</param>
		public void QueueEviction(long id)
		{
			lock(_queueLock)
			{
				_evictionQueue.Enqueue(Retrieve(id));
			}
			GroomerSignal.Set();
		}

        /// <summary>
        /// Queues a cache item for eviction by its UUID. The item will be evicted on the next groom cycle.
        /// </summary>
        /// <param name="uuid">The UUID of the item to evict.</param>
		public void QueueEviction(string uuid)
		{
			lock (_queueLock)
			{
				_evictionQueue.Enqueue(Retrieve(uuid));
			}
			GroomerSignal.Set();
		}

        /// <summary>
        /// Immediately evicts the cache item with the specified numeric ID.
        /// </summary>
        /// <param name="id">The numeric ID of the item to evict.</param>
		public void Evict(long id)
		{
			Evict(Retrieve(id));
		}

        /// <summary>
        /// Immediately evicts the cache item with the specified UUID.
        /// </summary>
        /// <param name="uuid">The UUID of the item to evict.</param>
		public void Evict(string uuid)
		{
			Evict(Retrieve(uuid));
		}

        /// <summary>
        /// Immediately evicts the specified cache item and fires the <see cref="Evicted"/> event.
        /// </summary>
        /// <param name="item">The cache item to evict.</param>
        public void Evict(CacheItem item)
        {
            HashSet<CacheItem> itemsCopy = new HashSet<CacheItem>(Items.Where(ci => !ci.Equals(item)));
            Items = itemsCopy;
            Organize();
            
            FireEvent(Evicted, new CacheEvictionEventArgs { Cache = this, EvictedItems = new CacheItem[] { item } });
        }

        /// <summary>
        /// Remove the specified number of entries from the end of 
        /// the Cache; all entries are sorted descending by hits
        /// </summary>
        /// <param name="count"></param>
        public void Evict(int count)
        {
            HashSet<CacheItem> removed = new HashSet<CacheItem>();
            HashSet<CacheItem> itemsCopy = new HashSet<CacheItem>(Items);

            LastEvictionCount = count > itemsCopy.Count ? Items.Count : count;
            int firstCount = itemsCopy.Count - count;
            int tailSize = itemsCopy.Count - firstCount;
            if (firstCount < 0)
            {
                removed = new HashSet<CacheItem>(itemsCopy);
                itemsCopy = new HashSet<CacheItem>();
            }
            else
            {
                HashSet<CacheItem> kept = new HashSet<CacheItem>();
                int i = 0;
                foreach (CacheItem item in ItemsByHits)
                {
                    if (i < firstCount)
                    {
                        kept.Add(item);
                    }
                    else
                    {
                        removed.Add(item);
                    }
                    i++;
                }
                itemsCopy = kept;
            }

            Items = itemsCopy;
            Organize();
            FireEvent(Evicted, new CacheEvictionEventArgs { Cache = this, EvictedItems = removed.ToArray() });
        }

        /// <summary>
        /// Evicts all cache items whose values match the specified predicate and fires the <see cref="Evicted"/> event.
        /// </summary>
        /// <param name="predicate">A function that returns true for items that should be evicted.</param>
		public void Evict(Func<object, bool> predicate)
		{
            HashSet<CacheItem> itemsCopy = new HashSet<CacheItem>(Items);
            HashSet<CacheItem> kept = new HashSet<CacheItem>();
            List<CacheItem> removed = new List<CacheItem>();
            itemsCopy.Each(new { Removed = removed, Kept = kept }, (ctx, ci) =>
            {
                if (predicate(ci.Value))
                {
                    ctx.Removed.Add(ci);
                }
                else
                {
                    ctx.Kept.Add(ci);
                }
            });
            LastEvictionCount = removed.Count;
            Items = kept;
            Organize();

			FireEvent(Evicted, new CacheEvictionEventArgs { Cache = this, EvictedItems = removed.ToArray() });
		}

        /// <summary>
        /// Get the count of items at the tail of the 
        /// cache that can be evicted if any.  This value is
        /// determined by comparing the amount of memory used
        /// by the cache vs the MaxBytes property value
        /// </summary>
        /// <returns></returns>
		protected int GetEvictableTailCount()
		{
			uint bytesOver = ItemsMemorySize - MaxBytes;
			int result = 0;
			if(bytesOver > 0)
			{
				int currentTailSize = 0;
				int currentItemCount = 0;
                foreach(CacheItem item in ItemsByHits.Reverse())
                {
                    currentItemCount++;
                    currentTailSize += item.MemorySize;
                    if (currentTailSize > bytesOver)
                    {
                        result = currentItemCount;
                        break;
                    }
                }
			}

			return result;
		}
		
		private void Groomer() // referenced by dynamic setup of _groomerThread; see StartGroomer
		{
			while(_keepGrooming)
			{
                try
                {
                    GroomerSignal.WaitOne();
                    Groom();
                }
                catch (Exception ex)
                {
                    Trace.Write($"Exception in cache groomer thread: {ex.Message}");
                }
			}
		}
		
		private void StopGrooming()
		{
			try
			{
				_keepGrooming = false;
				_groomerThread.Abort();
				_groomerThread.Join(3000);
			}
			catch
			{
			}
		}

        protected async void Organize()
        {
            await Task.Run(() =>
            {
                HashSet<CacheItem> itemsCopy = new HashSet<CacheItem>(Items);
                Parallel.ForEach(new Action[]
                {
                    () =>
                    {
                        SortedSet<CacheItem> itemsByHits = new SortedSet<CacheItem>(Items, new CacheItemComparer{ Hits = true, SortOrder = Data.SortOrder.Descending });
                        ItemsByHits = itemsByHits;
                    },
                    () =>
                    {
                        SortedSet<CacheItem> itemsByMisses = new SortedSet<CacheItem>(Items, new CacheItemComparer{Misses = true, SortOrder = Data.SortOrder.Descending });
                        ItemsByMisses = itemsByMisses;
                    },
                    () =>
                    {
                        Dictionary<ulong, CacheItem> itemsById = itemsCopy.ToDictionary(ci => ci.Id);
                        ItemsById = itemsById;
                    },
                    () =>
                    {
                        Dictionary<string, CacheItem> itemsByUuid = itemsCopy.ToDictionary(ci => ci.Uuid);
                        ItemsByUuid = itemsByUuid;
                    },
                    () =>
                    {
                        Dictionary<string, CacheItem> itemsByCuid = itemsCopy.ToDictionary(ci => ci.Cuid);
                        ItemsByCuid = itemsByCuid;
                    },
                    () =>
                    {
                        Dictionary<string, CacheItem> itemsByName = new Dictionary<string, CacheItem>();
                        foreach(CacheItem item in itemsCopy)
                        {
                            string name = item.Property<string>("Name", false).Or(item.Uuid)!;
                            if (itemsByName.ContainsKey(name!))
                            {
                                Log.Warn("Multiple cache items with the same name: {0} ({1}) and ({2})", name, itemsByName[name].Uuid, item.Uuid);
                                itemsByName[name] = item;
                            }
                            else
                            {
                                itemsByName.Add(name, item);
                            }
                        }
                        ItemsByName = itemsByName;
                    }
                }, action => action());
            });
        }
    }
}
