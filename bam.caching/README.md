# bam.caching

In-memory object caching with background grooming, file caching, and repository cache integration.

## Overview

`bam.caching` provides a comprehensive caching framework for the BAM ecosystem. The core `Cache` class is an in-memory object store that tracks items by ID, UUID, CUID, and name, with sorted sets organized by hit and miss counts. A background groomer thread automatically evicts low-hit items when the cache exceeds its configurable `MaxBytes` limit. The `CacheManager` coordinates multiple type-keyed `Cache` instances and provides a singleton `Default` instance for global access.

The `CachingRepository` wraps any `IRepository` implementation with a transparent caching layer. It intercepts `Create`, `Retrieve`, `Update`, and `Query` operations, checking the cache first and falling back to the source repository on cache misses. Query results from both cache and source are merged using parallel tasks, and the cache is refreshed in the background. Delete operations intentionally throw `DeleteNotSupportedException` to prevent cache-source consistency issues.

The library also includes file caching infrastructure (`FileCache`, `TextFileCache`, `BinaryFileCache`, `JsFileCache`) that stores file contents in memory with automatic hash-based change detection and reload. The `JsFileCache` additionally supports JavaScript minification. Extension methods provide GZip compression for both `byte[]` and `string` types, and a `QueryCache` class supports caching query results by filter context.

## Key Classes

| Class | Description |
|---|---|
| `Cache` | In-memory object cache with background groomer thread. Tracks items by ID, UUID, CUID, and name. Supports eviction by count, predicate, or queue. Default max size: 512 KB. |
| `Cache<T>` | Generic strongly-typed cache for items implementing `IMemorySize`. |
| `CacheItem` | Wraps a cached value with metadata: ID, UUID, CUID, Name, creation time, last read time, hit/miss counts, and serialized memory size. |
| `CacheItem<T>` | Strongly-typed cache item where `T` implements `IMemorySize`. |
| `CacheManager` | Manages a `ConcurrentDictionary` of type-keyed `Cache` instances. Default max: 500 MB across all caches. Provides a singleton `Default`. |
| `ICacheManager` | Interface for cache management operations: get/set cache by type, clear all, monitor size. |
| `CachingRepository` | Repository decorator that adds transparent caching. Intercepts CRUD and query operations, merging cache and source results in parallel. |
| `CachingRepository<T>` | Generic variant with implicit conversion to the source repository type. |
| `FileCache` | Abstract base for file content caching with hash-based change detection and automatic reload. |
| `BinaryFileCache` | `FileCache` implementation for binary file content (raw bytes). |
| `TextFileCache` | `FileCache` implementation for text files with extension validation. Throws on byte access (use `BinaryFileCache` instead). |
| `JsFileCache` | Extends `TextFileCache` with JavaScript minification support via `MinifyResult`. |
| `CachedFile` | Represents a single cached file with lazy-loaded text, bytes, and GZipped variants. Auto-reloads on file system changes. |
| `IFileCache` | Interface for file cache operations: get bytes, text, zipped bytes, and load files. |
| `QueryCache` / `QueryCache<T>` | Caches query results keyed by `QueryContext` (data source + filter). Supports typed and untyped queries with reload capability. |
| `QueryContext` | Identifies a query by its `IQueryFilterable` data source and `QueryFilter`. Used as dictionary key in `QueryCache`. |
| `CacheItemComparer` | `IComparer<CacheItem>` that sorts by hits or misses in ascending or descending order. |
| `IMemorySize` | Interface requiring implementing types to report their in-memory size in bytes. |
| `DeleteNotSupportedException` | Thrown by `CachingRepository.Delete` to prevent cache consistency issues. |
| `CacheEvictionEventArgs` | Event args carrying the cache reference and array of evicted items. |
| `CacheManagerEventArgs` | Event args for cache set/removed events, carrying the type and cache reference. |
| `CacheQueryEventArgs<T>` | Event args for query events with type, filter, and results. |
| `CacheRetrieveEventArgs` / `CacheRetrieveEventArgs<T>` | Event args for retrieve events with the retrieved item. |
| `CachingRepositoryEventArgs` | Event args for typeless queries and differing-type warnings. |
| `ByteArrayExtensions` | Extension methods: `GZip()` and `GZipAsync()` for `byte[]`. |
| `StringExtensions` | Extension methods: `GZip()` and `GZipAsync()` for `string`. |

## Dependencies

### Project References
- `bam.base` -- `Loggable`, `ILogger`, `Log`, `Args`, extension methods, `VerbosityAttribute`, reflection utilities
- `bam.data.repositories` -- `Repository`, `IRepository`, `IMetaProvider`, `Meta`, `IQueryFilterable`, `IQueryFilter`, `DaoRepository`, `MongoRepository`
- `bam.data` -- `SortOrder`, `DataExtensions`, `QueryFilter`, `IQueryFilter`
- `bam.javascript` -- `MinifyResult` for JavaScript minification in `JsFileCache`

### Package References
- None

## Target Framework
- `net10.0`

## Usage Examples

### Basic object caching
```csharp
using Bam.Caching;

Cache cache = new Cache("my-cache", maxBytes: 1048576, groomInBackground: true);

// Add an item
CacheItem item = cache.Add(new { Id = 1UL, Name = "Widget", Uuid = Guid.NewGuid().ToString() });

// Retrieve by UUID
CacheItem retrieved = cache.Retrieve(item.Uuid);
var widget = retrieved.ValueAs<dynamic>();

// Retrieve with fallback
string result = cache.RetrieveByName<string>("config", () => LoadConfigFromDb());

// Query with predicate
IEnumerable<CacheItem> matches = cache.Query(obj => obj.ToString().Contains("Widget"));
```

### Using CacheManager for type-based caches
```csharp
using Bam.Caching;

CacheManager manager = CacheManager.Default;

// Get or create a cache for a specific type
Cache userCache = manager.CacheFor<User>();
userCache.Add(new User { Id = 1, Name = "Alice" });

// Check total memory across all caches
uint totalSize = manager.AllCacheSize;
```

### Wrapping a repository with CachingRepository
```csharp
using Bam.Caching;

IRepository sourceRepo = GetDaoRepository();
CachingRepository cachingRepo = new CachingRepository(sourceRepo);

// Create goes to source and cache
var user = cachingRepo.Create(new User { Name = "Bob" });

// Retrieve checks cache first, falls back to source
var found = cachingRepo.Retrieve<User>(user.Id);

// Query merges cache and source results
var results = cachingRepo.Query<User>(u => u.Name.StartsWith("B"));

// Prime the cache asynchronously
await cachingRepo.CacheAsync<User>(u => u.IsActive);
```

### Caching file contents
```csharp
using Bam.Caching.File;

BinaryFileCache binaryCache = new BinaryFileCache();
byte[] content = binaryCache.GetContent("/path/to/image.png");
byte[] zipped = binaryCache.GetZippedContent("/path/to/image.png");

TextFileCache textCache = new TextFileCache(".html");
string html = textCache.GetText(new FileInfo("/path/to/page.html"));

JsFileCache jsCache = new JsFileCache { Minify = true };
byte[] minified = jsCache.GetContent("/path/to/app.js");
```

### Using QueryCache for cached query results
```csharp
using Bam.Caching;

QueryCache<User> queryCache = new QueryCache<User>();
IEnumerable<User> users = queryCache.Results(repository, new QueryFilter("IsActive", true));

// Force reload
users = queryCache.Reload(repository, new QueryFilter("IsActive", true));
```

## Known Gaps / Not Yet Implemented

- **Extensions class is empty**: `Caching/Extensions.cs` contains an empty class body with no extension methods.
- **Delete is not supported on CachingRepository**: `Delete` operations throw `DeleteNotSupportedException` by design. Callers must access `CachingRepository.SourceRepository.Delete` directly.
- **Javascript folder excluded**: The `Javascript\` subfolder is excluded from compilation in the .csproj, though `JsFileCache` still references `Bam.Javascript.MinifyResult` via the `bam.javascript` project reference.
- **CachingRepository only supports DaoRepository and MongoRepository**: The `DelegateOrThrow` method explicitly checks for these two types and throws `UnsupportedRepositoryTypeException` for any other `IRepository` implementation.
- **Thread.Abort in StopGrooming**: The `Cache.StopGrooming` method calls `Thread.Abort()`, which is not supported in .NET Core/.NET 5+ and will throw `PlatformNotSupportedException` at runtime.
- **CacheAsync typo**: The method `CacheAysync` (in `CachingRepository`) has a misspelling in its name.
