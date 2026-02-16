/*
	Copyright © Bryan Apellanes 2015  
*/

using Bam.Logging;
using Bam.Data.Repositories;
using Bam.Data;
using System.Reflection;
using Bam.Data.Schema;

namespace Bam.Caching
{
    /// <summary>
    /// A typed caching repository that wraps a source repository of type <typeparamref name="T"/> with an in-memory cache layer.
    /// </summary>
    /// <typeparam name="T">The type of the underlying source repository.</typeparam>
    public class CachingRepository<T>: CachingRepository where T : class, IRepository
    {
        /// <summary>
        /// Implicitly converts a <see cref="CachingRepository{T}"/> to the underlying source repository type.
        /// </summary>
        /// <param name="repo">The caching repository to convert.</param>
        /// <returns>The underlying source repository.</returns>
        public static implicit operator T(CachingRepository<T> repo)
        {
            return repo.TypedSourceRepository;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CachingRepository{T}"/> class wrapping the specified source repository.
        /// </summary>
        /// <param name="sourceRepository">The source repository to wrap with caching.</param>
        public CachingRepository(T sourceRepository) : base(sourceRepository)
        { }

        /// <summary>
        /// Gets the underlying source repository cast to its specific type.
        /// </summary>
        public T TypedSourceRepository { get { return SourceRepository as T; } }
    }

    /// <summary>
    /// A repository that wraps a source repository with an in-memory cache layer, caching creates, retrieves, queries, and updates.
    /// Delete operations throw <see cref="DeleteNotSupportedException"/>; use <see cref="SourceRepository"/> directly for deletes.
    /// </summary>
	public class CachingRepository: Repository, IQueryFilterable
	{
        const string ExceptionText = "The specified type is not marked as serializable, add the [Serializable] attribute to the class definition to ensure proper caching behavior";

        CacheManager _cacheManager;
        /// <summary>
        /// Occurs when an item is retrieved from the source repository (cache miss).
        /// </summary>
        public event EventHandler RetrievedFromSource;

        /// <summary>
        /// Occurs when an item is retrieved from the cache (cache hit).
        /// </summary>
        public event EventHandler RetrievedFromCache;

        /// <summary>
        /// Occurs when the source repository is queried.
        /// </summary>
        public event EventHandler QueriedSource;

        /// <summary>
        /// Occurs when the cache is queried.
        /// </summary>
        public event EventHandler QueriedCache;

        /// <summary>
        /// Occurs when items are evicted from the cache.
        /// </summary>
        public event EventHandler Evicted;

        protected CachingRepository() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="CachingRepository"/> class wrapping the specified source repository.
        /// </summary>
        /// <param name="sourceRepository">The source repository to wrap with caching. All storable types must have the <see cref="SerializableAttribute"/>.</param>
        /// <param name="logger">An optional logger. If null, uses <see cref="Log.Default"/>.</param>
        public CachingRepository(IRepository sourceRepository, ILogger logger = null)
        {
            SetSourceRepository(sourceRepository);
            SetCacheManager();

            Logger = logger ?? Log.Default;
        }

        protected void SetCacheManager()
        {
            _cacheManager = new CacheManager();
            _cacheManager.SubscribeOnce(nameof(CacheManager.Evicted), OnEvicted);
        }

        protected void SetSourceRepository(IRepository sourceRepository)
        {
            SourceRepository = sourceRepository;
            SourceRepository.StorableTypes.Each(new { Repo = this }, (ctx, t) => ctx.Repo.AddType(t));
            ValidateTypes();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CachingRepository"/> class using a <see cref="DaoRepository"/> as the source.
        /// </summary>
        /// <param name="schemaGenerator">The schema provider for DAO generation.</param>
        /// <param name="daoGenerator">The DAO generator.</param>
        /// <param name="wrapperGenerator">The wrapper generator.</param>
        /// <param name="database">An optional database instance.</param>
        /// <param name="logger">An optional logger.</param>
        public CachingRepository(ISchemaProvider schemaGenerator, IDaoGenerator daoGenerator, IWrapperGenerator wrapperGenerator, IDatabase? database = null, ILogger? logger = null) : this(new DaoRepository(schemaGenerator, daoGenerator, wrapperGenerator, database, logger), logger)
	    {
	    }

        /// <summary>
        /// Validates that all storable types in the source repository are marked with the <see cref="SerializableAttribute"/>.
        /// </summary>
        public void ValidateTypes()
        {
            foreach(Type type in SourceRepository.StorableTypes)
            {
                Args.ThrowIf(!type.HasCustomAttributeOfType<SerializableAttribute>(), ExceptionText);
            }
        }

        /// <summary>
        /// Adds a type to the source repository. Throws if the type is not marked with <see cref="SerializableAttribute"/>.
        /// </summary>
        /// <param name="type">The type to add.</param>
        public override void AddType(Type type)
        {
            Args.ThrowIf(!type.HasCustomAttributeOfType<SerializableAttribute>(), ExceptionText);
            SourceRepository.AddType(type);
        }

        /// <summary>
        /// Queries the source repository and adds the results to the internal cache.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="query"></param>
        /// <returns></returns>
        public override IEnumerable<T> Query<T>(IQueryFilter query)
        {
            IEnumerable<T> results = DelegateGenericOrThrow<IEnumerable<T>, T>("Query", query).CopyAs<T>();
            QueriedSource?.Invoke(this, new CacheQueryEventArgs<T> { Type = typeof(T), QueryFilter = query, Results = results });
            foreach(CacheItem item in _cacheManager.CacheFor<T>().Add(results))
            {
                yield return item.ValueAs<T>();
            }
        }
        
        /// <summary>
        /// Queries the source repository and adds the results to the internal
        /// cache
        /// </summary>
        /// <param name="type"></param>
        /// <param name="query"></param>
        /// <returns>IEnumerable{object}</returns>
        public override IEnumerable<object> Query(Type type, IQueryFilter query)
        {
            IEnumerable<object> results = DelegateOrThrow<IEnumerable<object>>("Query", type, query).CopyAs(type);
            QueriedSource?.Invoke(this, new CacheQueryEventArgs<object> { Type = type, QueryFilter = query, Results = results });
            foreach (CacheItem item in _cacheManager.CacheFor(type).Add(results))
            {
                yield return item.Value;
            }
        }

        /// <summary>
        /// Creates a new item in the source repository and adds it to the cache.
        /// </summary>
        /// <typeparam name="T">The type of the item to create.</typeparam>
        /// <param name="toCreate">The item to create.</param>
        /// <returns>The created item.</returns>
        public override T Create<T>(T toCreate)
		{
			Args.ThrowIfNull(toCreate, "toCreate");

			T result = this.SourceRepository.Create<T>(toCreate);
			_cacheManager.CacheFor<T>().Add(result);

			return result;
		}

        /// <summary>
        /// Creates a new item of the specified type in the source repository and adds it to the cache.
        /// </summary>
        /// <param name="type">The type of the item to create.</param>
        /// <param name="toCreate">The item to create.</param>
        /// <returns>The created item.</returns>
        public override object Create(Type type, object toCreate)
        {
            return Create(toCreate);
        }

        /// <summary>
        /// Creates a new item in the source repository and adds it to the cache.
        /// </summary>
        /// <param name="toCreate">The item to create.</param>
        /// <returns>The created item.</returns>
        public override object Create(object toCreate)
		{
			Args.ThrowIfNull(toCreate, "toCreate");

			object result = this.SourceRepository.Create(toCreate);
			_cacheManager.CacheFor(result.GetType()).Add(result);

			return result;
		}

        /// <summary>
        /// Retrieves an item of type <typeparamref name="T"/> by its integer ID, checking the cache first.
        /// </summary>
        /// <typeparam name="T">The type of the item to retrieve.</typeparam>
        /// <param name="id">The integer ID of the item.</param>
        /// <returns>The retrieved item.</returns>
		public override T Retrieve<T>(int id)
		{
			return Retrieve<T>((long)id);
		}

        /// <summary>
        /// Retrieves an item of type <typeparamref name="T"/> by its unsigned long ID, checking the cache first.
        /// </summary>
        /// <typeparam name="T">The type of the item to retrieve.</typeparam>
        /// <param name="id">The unsigned long ID of the item.</param>
        /// <returns>The retrieved item.</returns>
		public override T Retrieve<T>(ulong id)
		{
            return Retrieve<T>((cache) => cache.Retrieve(id), () => SourceRepository.Retrieve<T>(id));
        }

        /// <summary>
        /// Retrieves an item of type <typeparamref name="T"/> by its long ID, checking the cache first.
        /// </summary>
        /// <typeparam name="T">The type of the item to retrieve.</typeparam>
        /// <param name="id">The long ID of the item.</param>
        /// <returns>The retrieved item.</returns>
        public override T Retrieve<T>(long id)
        {
            return Retrieve<T>((cache) => cache.Retrieve(id), () => SourceRepository.Retrieve<T>(id));
        }

        /// <summary>
        /// Retrieves an item of type <typeparamref name="T"/> by its UUID, checking the cache first.
        /// </summary>
        /// <typeparam name="T">The type of the item to retrieve.</typeparam>
        /// <param name="uuid">The UUID of the item.</param>
        /// <returns>The retrieved item.</returns>
        public override T Retrieve<T>(string uuid)
        {
            return Retrieve<T>((cache) => cache.Retrieve(uuid), () => SourceRepository.Retrieve<T>(uuid));
        }

        /// <summary>
        /// Delegates to the underlying SourceRepository
        /// without caching
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public override IEnumerable<T> RetrieveAll<T>()
		{
			return SourceRepository.RetrieveAll<T>();
		}

        /// <summary>
        /// Delegates batch retrieval to the underlying source repository without caching.
        /// </summary>
        /// <param name="type">The type to retrieve.</param>
        /// <param name="batchSize">The number of items per batch.</param>
        /// <param name="processor">The action to process each batch.</param>
        public override void BatchRetrieveAll(Type type, int batchSize, Action<IEnumerable<object>> processor)
        {
            SourceRepository.BatchRetrieveAll(type, batchSize, processor);
        }

		/// <summary>
		/// Delegates to the underlying SourceRepository
		/// without caching
		/// </summary>
		/// <param name="type"></param>
		/// <returns></returns>
		public override IEnumerable<object> RetrieveAll(Type type)
		{
			return SourceRepository.RetrieveAll(type);
		}

        /// <summary>
        /// Retrieves an item of the specified type by its long ID, checking the cache first and falling back to the source.
        /// </summary>
        /// <param name="objectType">The type of the item to retrieve.</param>
        /// <param name="id">The long ID of the item.</param>
        /// <returns>The retrieved item.</returns>
		public override object Retrieve(Type objectType, long id)
		{
			Cache cache = _cacheManager.CacheFor(objectType);
			CacheItem cacheItem = cache.Retrieve(id);
			object result;
			if (cacheItem == null)
			{
				result = SourceRepository.Retrieve(objectType, id);
				cache.Add(result);
                RetrievedFromSource?.Invoke(this, new CacheRetrieveEventArgs { Type = objectType, Item = result });
			}
			else
			{
				result = cacheItem.Value;
                RetrievedFromCache?.Invoke(this, new CacheRetrieveEventArgs { Type = objectType, Item = result });
            }

			return result;
		}

        /// <summary>
        /// Retrieves an item of the specified type by its unsigned long ID, checking the cache first and falling back to the source.
        /// </summary>
        /// <param name="objectType">The type of the item to retrieve.</param>
        /// <param name="id">The unsigned long ID of the item.</param>
        /// <returns>The retrieved item.</returns>
        public override object Retrieve(Type objectType, ulong id)
        {
            Cache cache = _cacheManager.CacheFor(objectType);
            CacheItem cacheItem = cache.Retrieve(id);
            object result;
            if (cacheItem == null)
            {
                result = SourceRepository.Retrieve(objectType, id);
                cache.Add(result);
                RetrievedFromSource?.Invoke(this, new CacheRetrieveEventArgs { Type = objectType, Item = result });
            }
            else
            {
                result = cacheItem.Value;
                RetrievedFromCache?.Invoke(this, new CacheRetrieveEventArgs { Type = objectType, Item = result });
            }

            return result;
        }

        /// <summary>
        /// Retrieves an item of the specified type by its UUID, checking the cache first and falling back to the source.
        /// </summary>
        /// <param name="objectType">The type of the item to retrieve.</param>
        /// <param name="uuid">The UUID of the item.</param>
        /// <returns>The retrieved item.</returns>
        public override object Retrieve(Type objectType, string uuid)
		{
			Cache cache = _cacheManager.CacheFor(objectType);
			CacheItem cacheItem = cache.Retrieve(uuid);
			object result;
			if (cacheItem == null)
			{
				result = SourceRepository.Retrieve(objectType, uuid);
				cache.Add(result);
                RetrievedFromSource?.Invoke(this, new CacheRetrieveEventArgs { Type = objectType, Item = result });
            }
			else
			{
				result = cacheItem.Value;
                RetrievedFromCache?.Invoke(this, new CacheRetrieveEventArgs { Type = objectType, Item = result });
            }

			return result;
		}

        /// <summary>
        /// Occurs when a non typed query is executed which finds multiple entries with the same property name and value where the 
        /// object types are not the same.
        /// </summary>
        [Verbosity(VerbosityLevel.Information, SenderMessageFormat="Different types were found with the same property name and value: \r\n{DifferingTypes}")]
		public event EventHandler DifferringTypesFound;

        /// <summary>
        /// Gets or sets the property name used in the most recent non-typed query.
        /// </summary>
		public string PropertyName { get; set; }

        /// <summary>
        /// Gets or sets the value used in the most recent non-typed query.
        /// </summary>
		public string Value { get; set; }
        /// <summary>
        /// Event that fires when a non typed query is executed.  Used as a 
        /// warning that the type cannot be determined and will have to be
        /// resolved by the caller.
        /// </summary>
        public event EventHandler<CachingRepositoryEventArgs> TypelessQuery;

        protected void OnQueriedSource<T>(IEnumerable<T> results)
        {
            QueriedSource?.Invoke(this, new CacheQueryEventArgs<T> { Type = typeof(T), Results = results });
        }
        
        /// <summary>
        /// Queries both the cache and the source repository in parallel using the specified predicate, merging results.
        /// </summary>
        /// <typeparam name="T">The type of items to query.</typeparam>
        /// <param name="predicate">The predicate to filter items by.</param>
        /// <returns>The merged results from cache and source.</returns>
        public override IEnumerable<T> Query<T>(Func<T, bool> predicate)
        {
            Cache cache = _cacheManager.CacheFor<T>();
            Task<HashSet<T>> cacheResults = Task.Run(() => QueryCache<T>(predicate));
            Task<HashSet<T>> queryResults = Task.Run(() =>
            {
                HashSet<T> qr = new HashSet<T>(DelegateOrThrow<IEnumerable<object>>("RetrieveAll", typeof(T)).CopyAs<T>().Where(predicate));
                OnQueriedSource<T>(qr);
                return qr;
            });
            Task<HashSet<T>[]> resultsHashes = Task.WhenAll(cacheResults, queryResults);
            resultsHashes.Wait();
            HashSet<T> results = HandleResults(cache, resultsHashes.Result);
            return results;
        }

        /// <summary>
        /// Queries both the cache and the source repository in parallel for items of the specified type, merging results.
        /// </summary>
        /// <param name="type">The type of items to query.</param>
        /// <param name="predicate">The predicate to filter items by.</param>
        /// <returns>The merged results from cache and source.</returns>
        public override IEnumerable<object> Query(Type type, Func<object, bool> predicate)
        {
            Cache cache = _cacheManager.CacheFor(type);
            Task<HashSet<object>> cacheResults = Task.Run(() => QueryCache(type, predicate));
            Task<HashSet<object>> queryResults = Task.Run(() =>
            {
                HashSet<object> qr = new HashSet<object>(DelegateOrThrow<IEnumerable<object>>("Query", type, predicate).CopyAs(type));
                OnQueriedSource<object>(qr);
                return qr;
            });
            Task<HashSet<object>[]> resultsHashes = Task.WhenAll(cacheResults, queryResults);
            resultsHashes.Wait();
            return HandleResults(cache, resultsHashes.Result);
        }

        /// <summary>
        /// Queries both the cache and the source repository in parallel using a dynamic query object, merging results.
        /// </summary>
        /// <typeparam name="T">The type of items to query.</typeparam>
        /// <param name="query">A dynamic object whose properties are used as query parameters.</param>
        /// <returns>The merged results from cache and source.</returns>
        public override IEnumerable<T> Query<T>(dynamic query)
        {
            Cache cache = _cacheManager.CacheFor<T>();
            Task<HashSet<T>> cacheResults = Task.Run((Func<HashSet<T>>)(() => QueryCache<T>((dynamic)query)));
            Task<HashSet<T>> queryResults = Task.Run(() => 
            {
                IEnumerable<object> results = DelegateOrThrow<IEnumerable<object>>("Query", typeof(T), DataExtensions.ToDictionary(query));
                OnQueriedSource<object>(results);
                return new HashSet<T>(results.CopyAs<T>());
            });
            Task<HashSet<T>[]> resultsHashes = Task.WhenAll(cacheResults, queryResults);
            resultsHashes.Wait();
            return HandleResults(cache, resultsHashes.Result);
		}

        /// <summary>
        /// Queries both the cache and the source repository in parallel using dictionary query parameters, merging results.
        /// </summary>
        /// <typeparam name="T">The type of items to query.</typeparam>
        /// <param name="queryParameters">A dictionary of property name-value pairs to match.</param>
        /// <returns>The merged results from cache and source.</returns>
        public override IEnumerable<T> Query<T>(Dictionary<string, object> queryParameters)
        {
            Cache cache = _cacheManager.CacheFor<T>();
            Task<HashSet<T>> cacheResults = Task.Run(() => QueryCache<T>(queryParameters));
            Task<HashSet<T>> queryResults = Task.Run(() =>
            {
                HashSet<T> results = new HashSet<T>(DelegateGenericOrThrow<IEnumerable<T>, T>("Query", queryParameters).CopyAs<T>());
                OnQueriedSource(results);
                return results;
            });
            Task<HashSet<T>[]> resultsHashes = Task.WhenAll(cacheResults, queryResults);
            resultsHashes.Wait();
            return HandleResults(cache, resultsHashes.Result);
        }

        /// <summary>
        /// Queries both the cache and the source repository in parallel for items of the specified type using dictionary parameters, merging results.
        /// </summary>
        /// <param name="type">The type of items to query.</param>
        /// <param name="queryParameters">A dictionary of property name-value pairs to match.</param>
        /// <returns>The merged results from cache and source.</returns>
        public override IEnumerable<object> Query(Type type, Dictionary<string, object> queryParameters)
        {
            Cache cache = _cacheManager.CacheFor(type);
            Task<HashSet<object>> cacheResults = Task.Run(() => QueryCache(type, queryParameters));
            Task<HashSet<object>> queryResults = Task.Run(() =>
            {
                HashSet<object> results = new HashSet<object>(DelegateOrThrow<IEnumerable<object>>("Query", type, queryParameters).CopyAs(type));
                OnQueriedSource(results);
                return results;
            });
            Task<HashSet<object>[]> resultsHashes = Task.WhenAll(cacheResults, queryResults);
            resultsHashes.Wait();
            return HandleResults(cache, resultsHashes.Result);
        }

        /// <summary>
        /// Queries both the cache and the source repository in parallel for items of the specified type using a dynamic query, merging results.
        /// </summary>
        /// <param name="type">The type of items to query.</param>
        /// <param name="query">A dynamic object whose properties are used as query parameters.</param>
        /// <returns>The merged results from cache and source.</returns>
        public override IEnumerable<object> Query(Type type, dynamic query)
        {
            Cache cache = _cacheManager.CacheFor(type);
            Task<HashSet<object>> cacheResults = Task.Run((Func<HashSet<object>>)(() => QueryCache(type, query)));
            Task<HashSet<object>> queryResults = Task.Run(() =>
            {
                IEnumerable<object> results = DelegateOrThrow<IEnumerable<object>>("Query", type, query);
                OnQueriedSource(results);
                return new HashSet<object>(results.CopyAs(type));
            });
            Task<HashSet<object>[]> resultsHashes = Task.WhenAll(cacheResults, queryResults);
            resultsHashes.Wait();
            return HandleResults(cache, resultsHashes.Result);
        }

        /// <summary>
        /// Execute a non typed query against the underlying SourceRepository.
        /// Does not use the cache.
        /// </summary>
        /// <param name="propertyName"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public override IEnumerable<object> Query(string propertyName, object value)
        {
            TypelessQuery?.Invoke(this, new CachingRepositoryEventArgs { PropertyName = PropertyName, ParameterValue = value });
            object[] results = SourceRepository.Query(propertyName, value).ToArray();
            if (results.Length > 0)
            {
                CheckForDifferringTypes(propertyName, value, results);
            }
            return results;
        }

        /// <summary>
        /// Prime the cache with the results of the specified query.
        /// </summary>
        /// <typeparam name="T">The type to cache.</typeparam>
        /// <param name="predicate">The predicate.</param>
        /// <returns>Task{IEnumerable{T}}</returns>
        public Task<IEnumerable<T>> CacheAsync<T>(Func<T, bool> predicate)
        {
            return Task.Run(() => Cache<T>(predicate));
        }

        /// <summary>
        /// Prime the cache with the results of the specified query.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="predicate"></param>
        /// <returns></returns>
        public IEnumerable<T> Cache<T>(Func<T, bool> predicate)
        {
            Cache cache = _cacheManager.CacheFor(typeof(T));
            return HandleResults(cache, new HashSet<T>(DelegateGenericOrThrow<IEnumerable<T>, T>("Query", predicate)));
        }

        /// <summary>
        /// Prime the cache with the results of the specified query.
        /// </summary>
        /// <param name="type">The type to cache.</param>
        /// <param name="predicate">The predicate identifying what to cache.</param>
        /// <returns>Task{IEnumerable{object}}</returns>
        public Task<IEnumerable<object>> CacheAysync(Type type, Func<object, bool> predicate)
        {
            return Task.Run(() => Cache(type, predicate));
        }

        /// <summary>
        /// Prime the cache with the results of the specified query.
        /// </summary>
        /// <param name="type">The type to cache.</param>
        /// <param name="predicate">The predicate identifying what to cache.</param>
        /// <returns>IEnumerable{object}</returns>
        public IEnumerable<object> Cache(Type type, Func<object, bool> predicate)
        {
            Cache cache = _cacheManager.CacheFor(type);
            return HandleResults(cache, new HashSet<object>(DelegateOrThrow<IEnumerable<object>>("Query", type, predicate)));
        }

        /// <summary>
        /// Asynchronously primes the cache with the results of the specified dynamic query.
        /// </summary>
        /// <typeparam name="T">The type to cache.</typeparam>
        /// <param name="query">A dynamic object whose properties are used as query parameters.</param>
        /// <returns>A task that resolves to the cached results.</returns>
        public Task<IEnumerable<T>> CacheAsync<T>(dynamic query)
        {
            return Task.Run((Func<IEnumerable<T>>)(() => (Cache<T>(query))));
        }

        /// <summary>
        /// Primes the cache with the results of the specified dynamic query.
        /// </summary>
        /// <typeparam name="T">The type to cache.</typeparam>
        /// <param name="query">A dynamic object whose properties are used as query parameters.</param>
        /// <returns>The cached results.</returns>
        public IEnumerable<T> Cache<T>(dynamic query)
        {
            Cache cache = _cacheManager.CacheFor<T>();
            return HandleResults(cache, new HashSet<T>(DelegateGenericOrThrow<IEnumerable<T>, T>("Query", query)));
        }

        /// <summary>
        /// Asynchronously primes the cache with the results of the specified dictionary query.
        /// </summary>
        /// <typeparam name="T">The type to cache.</typeparam>
        /// <param name="queryParameters">A dictionary of property name-value pairs to match.</param>
        /// <returns>A task that resolves to the cached results.</returns>
        public Task<IEnumerable<T>> CacheAsync<T>(Dictionary<string, object> queryParameters)
        {
            return Task.Run(() => Cache<T>(queryParameters));
        }

        /// <summary>
        /// Primes the cache with the results of the specified dictionary query.
        /// </summary>
        /// <typeparam name="T">The type to cache.</typeparam>
        /// <param name="queryParameters">A dictionary of property name-value pairs to match.</param>
        /// <returns>The cached results.</returns>
        public IEnumerable<T> Cache<T>(Dictionary<string, object> queryParameters)
        {
            Cache cache = _cacheManager.CacheFor<T>();
            return HandleResults(cache, new HashSet<T>(DelegateGenericOrThrow<IEnumerable<T>, T>("Query", queryParameters)));
        }

        /// <summary>
        /// Asynchronously primes the cache for the specified type with the results of the dictionary query.
        /// </summary>
        /// <param name="type">The type to cache.</param>
        /// <param name="queryParameters">A dictionary of property name-value pairs to match.</param>
        /// <returns>A task that resolves to the cached results.</returns>
        public Task<IEnumerable<object>> CacheAsync(Type type, Dictionary<string, object> queryParameters)
        {
            return Task.Run(() => Cache(type, queryParameters));
        }

        /// <summary>
        /// Primes the cache for the specified type with the results of the dictionary query.
        /// </summary>
        /// <param name="type">The type to cache.</param>
        /// <param name="queryParameters">A dictionary of property name-value pairs to match.</param>
        /// <returns>The cached results.</returns>
        public IEnumerable<object> Cache(Type type, Dictionary<string, object> queryParameters)
        {
            Cache cache = _cacheManager.CacheFor(type);
            return HandleResults(cache, new HashSet<object>(DelegateOrThrow<IEnumerable<object>>("Query", type, queryParameters)));
        }

        /// <summary>
        /// Queries the cache for items of the specified type using a dynamic query object, matching by property values.
        /// </summary>
        /// <param name="type">The type of items to query.</param>
        /// <param name="query">A dynamic object whose properties are used as query parameters.</param>
        /// <returns>A set of matching items.</returns>
        public HashSet<object> QueryCache(Type type, dynamic query)
        {
            Cache cache = _cacheManager.CacheFor(type);
            Type queryType = query.GetType();
            PropertyInfo[] queryProps = queryType.GetProperties();
            return new HashSet<object>(cache.Query<object>(o =>
            {
                foreach (PropertyInfo prop in queryProps)
                {
                    PropertyInfo currentProp = type.GetProperty(prop.Name);
                    if (!ReflectionExtensions.Property(o, prop.Name).Equals(prop.GetValue(query)))
                    {
                        return false;
                    }
                }
                return true;
            }));
        }

        /// <summary>
        /// Queries the cache for items of type <typeparamref name="T"/> using a dynamic query object, matching by property values.
        /// </summary>
        /// <typeparam name="T">The type of items to query.</typeparam>
        /// <param name="query">A dynamic object whose properties are used as query parameters.</param>
        /// <returns>A set of matching items.</returns>
        public HashSet<T> QueryCache<T>(dynamic query)
        {
            Cache cache = _cacheManager.CacheFor<T>();
            Type queryType = query.GetType();
            PropertyInfo[] queryProps = queryType.GetProperties();
            return new HashSet<T>(cache.Query<T>(o =>
            {
                foreach (PropertyInfo prop in queryProps)
                {
                    PropertyInfo currentProp = typeof(T).GetProperty(prop.Name);
                    if (!ReflectionExtensions.Property(o, prop.Name).Equals(prop.GetValue(query)))
                    {
                        return false;
                    }
                }

                return true;
            }));
        }

        /// <summary>
        /// Queries the cache for items of type <typeparamref name="T"/> using a predicate.
        /// </summary>
        /// <typeparam name="T">The type of items to query.</typeparam>
        /// <param name="query">The predicate to filter items by.</param>
        /// <returns>A set of matching items.</returns>
        public HashSet<T> QueryCache<T>(Func<T, bool> query) where T : class, new()
        {
            Cache cache = _cacheManager.CacheFor<T>();
            IEnumerable<T> results = cache.Query<T>(query);
            QueriedCache?.Invoke(this, new CacheQueryEventArgs<T> { Type = typeof(T), Results = results });
            return new HashSet<T>(results);
        }

        /// <summary>
        /// Queries the cache for items of the specified type using a predicate.
        /// </summary>
        /// <param name="type">The type of items to query.</param>
        /// <param name="predicate">The predicate to filter items by.</param>
        /// <returns>A set of matching items.</returns>
        public HashSet<object> QueryCache(Type type, Func<object, bool> predicate)
        {
            Cache cache = _cacheManager.CacheFor(type);
            IEnumerable<object> results = cache.Query(predicate);
            QueriedCache?.Invoke(this, new CacheQueryEventArgs<object> { Type = type, Results = results });
            return new HashSet<object>(results);
        }

        /// <summary>
        /// Queries the cache for items of the specified type matching the given property name-value pairs.
        /// </summary>
        /// <param name="type">The type of items to query.</param>
        /// <param name="parameters">A dictionary of property name-value pairs to match.</param>
        /// <returns>A set of matching items.</returns>
        public HashSet<object> QueryCache(Type type, Dictionary<string, object> parameters)
        {
            Cache cache = _cacheManager.CacheFor(type);
            IEnumerable<object> results = cache.Query(o =>
            {
                foreach (string propName in parameters.Keys)
                {
                    if (!ReflectionExtensions.Property(o.Value, propName).Equals(parameters[propName]))
                    {
                        return false;
                    }
                }
                return true;
            }).Select(ci => ci.Value);
            QueriedCache?.Invoke(this, new CacheQueryEventArgs<object> { Type = type, Results = results });
            return new HashSet<object>(results);
        }

        /// <summary>
        /// Queries the cache for items of type <typeparamref name="T"/> matching the given property name-value pairs.
        /// </summary>
        /// <typeparam name="T">The type of items to query.</typeparam>
        /// <param name="parameters">A dictionary of property name-value pairs to match.</param>
        /// <returns>A set of matching items.</returns>
        public HashSet<T> QueryCache<T>(Dictionary<string, object> parameters)
        {
            Cache cache = _cacheManager.CacheFor<T>();
            return new HashSet<T>(cache.Query<T>(o =>
            {
                foreach(string propName in parameters.Keys)
                {
                    if(!ReflectionExtensions.Property(o, propName).Equals(parameters[propName]))
                    {
                        return false;
                    }
                }
                return true;
            }));
        }

        /// <summary>
        /// Updates the item in the source repository and refreshes it in the cache asynchronously.
        /// </summary>
        /// <typeparam name="T">The type of the item to update.</typeparam>
        /// <param name="toUpdate">The item to update.</param>
        /// <returns>The updated item.</returns>
        public override T Update<T>(T toUpdate)
		{
            Task.Run(() =>
            {
                Cache cache = _cacheManager.CacheFor<T>();
                CacheItem fromCache = cache.Retrieve(toUpdate);
                if (fromCache != null)
                {
                    cache.Evict(fromCache);
                    cache.Add(toUpdate);
                }
            });
            return SourceRepository.Update<T>(toUpdate);
		}

        /// <summary>
        /// Updates the item in the source repository and refreshes it in the cache asynchronously.
        /// </summary>
        /// <param name="toUpdate">The item to update.</param>
        /// <returns>The updated item.</returns>
        public override object Update(object toUpdate)
        {
            return Update(toUpdate.GetType(), toUpdate);
        }

        /// <summary>
        /// Updates the item of the specified type in the source repository and refreshes it in the cache asynchronously.
        /// </summary>
        /// <param name="type">The type of the item to update.</param>
        /// <param name="toUpdate">The item to update.</param>
        /// <returns>The updated item.</returns>
        public override object Update(Type type, object toUpdate)
		{
            Task.Run(() =>
            {
                Cache cache = _cacheManager.CacheFor(type);
                CacheItem fromCache = cache.Retrieve(toUpdate);
                if (fromCache != null)
                {
                    cache.Evict(fromCache);
                    cache.Add(toUpdate);
                }
            });
            return SourceRepository.Update(toUpdate);
        }
        
        /// <summary>
        /// Not supported. Throws <see cref="DeleteNotSupportedException"/>. Use <see cref="SourceRepository"/> directly for deletes.
        /// </summary>
        /// <typeparam name="T">The type of the item to delete.</typeparam>
        /// <param name="toDelete">The item to delete.</param>
        /// <returns>Does not return; always throws.</returns>
		public override bool Delete<T>(T toDelete)
		{
            throw new DeleteNotSupportedException(Meta.GetUuid(toDelete).Or(typeof(T).FullName));
		}

        /// <summary>
        /// Not supported. Throws <see cref="DeleteNotSupportedException"/>. Use <see cref="SourceRepository"/> directly for deletes.
        /// </summary>
        /// <param name="type">The type of the item to delete.</param>
        /// <param name="toDelete">The item to delete.</param>
        /// <returns>Does not return; always throws.</returns>
        public override bool Delete(Type type, object toDelete)
        {
            return Delete(toDelete);
        }

        /// <summary>
        /// Not supported. Throws <see cref="DeleteNotSupportedException"/>. Use <see cref="SourceRepository"/> directly for deletes.
        /// </summary>
        /// <param name="toDelete">The item to delete.</param>
        /// <returns>Does not return; always throws.</returns>
        public override bool Delete(object toDelete)
		{
            string id = toDelete == null ? "[null]" : Meta.GetUuid(toDelete);
            throw new DeleteNotSupportedException(id);
		}

        /// <summary>
        /// Gets the underlying source repository that this caching repository wraps.
        /// </summary>
        public IRepository SourceRepository { get; private set; }

        private static HashSet<T> HandleResults<T>(Cache cache, params HashSet<T>[] arrayOfHashSets)
        {
            HashSet<T> results = new HashSet<T>();
            foreach (HashSet<T> hs in arrayOfHashSets)
            {
                results.UnionWith(hs);
            }
            Task.Run(() => cache.Add(results.ToArray()));
            return results;
        }

        private void CheckForDifferringTypes(string propertyName, object value, object[] results)
        {
            Type firstType = results[0].GetType();
            object differentType = results.FirstOrDefault(o => o.GetType() != firstType);
            if (differentType != null)
            {
                HashSet<Type> differingTypes = new HashSet<Type>();
                results.Each(differingTypes, (typeHash, o) =>
                {
                    typeHash.Add(o.GetType());
                });
                FireEvent(DifferringTypesFound, new CachingRepositoryEventArgs 
                {
                    PropertyName = propertyName, 
                    ParameterValue = value == null ? "null" : value.ToString(), 
                    DifferingTypes = differingTypes.ToArray().ToDelimited(t => t.Name, ", ") 
                });
            }
        }

        private T DelegateOrThrow<T>(string methodName, params object[] parameters)
        {
            Args.ThrowIfNull(SourceRepository, "SourceRepository");
            if (SourceRepository is DaoRepository ||
                SourceRepository is MongoRepository)
            {
                return SourceRepository.Invoke<T>(methodName, parameters);
            }
            else
            {
                throw new UnsupportedRepositoryTypeException(SourceRepository.GetType());
            }
        }

        private T DelegateGenericOrThrow<T, TArg>(string methodName, params object[] parameters)
        {
            Args.ThrowIfNull(SourceRepository, "SourceRepository");
            if (SourceRepository is DaoRepository || 
                SourceRepository is MongoRepository)
            {
                return SourceRepository.InvokeGeneric<T, TArg>(methodName, parameters);
            }
            else
            {
                throw new UnsupportedRepositoryTypeException(SourceRepository.GetType());
            }
        }

        private T Retrieve<T>(Func<Cache, CacheItem> cacheRetriever, Func<T> sourceRetriever)
        {
            Cache cache = _cacheManager.CacheFor<T>();
            CacheItem cacheItem = cacheRetriever(cache);
            T result;
            if (cacheItem == null)
            {
                result = sourceRetriever();
                cache.Add(result);
                RetrievedFromSource?.Invoke(this, new CacheRetrieveEventArgs<T> { Item = result });
            }
            else
            {
                result = cacheItem.ValueAs<T>();
                RetrievedFromCache?.Invoke(this, new CacheRetrieveEventArgs<T> { Item = result });
            }
            if (result != null)
            {
                cache.Add(result);
            }

            return result;
        }

        private void OnEvicted(object sender, EventArgs e)
        {
            Evicted?.Invoke(sender, e);
        }
    }
}
