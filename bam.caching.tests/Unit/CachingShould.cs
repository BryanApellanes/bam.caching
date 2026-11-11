using Bam.Caching;
using Bam.Data;
using Bam.Data.Repositories;
using Bam.Test;

namespace Bam.Caching.Tests.Unit;

[Serializable]
public class TestCacheable : IMemorySize
{
    public TestCacheable()
    {
        Name = string.Empty;
        Value = string.Empty;
    }

    public string Name { get; set; }
    public string Value { get; set; }

    public int MemorySize()
    {
        return System.Text.Encoding.UTF8.GetByteCount(Name ?? string.Empty)
             + System.Text.Encoding.UTF8.GetByteCount(Value ?? string.Empty);
    }
}

[UnitTestMenu("Caching Should", Selector = "cs")]
public class CachingShould : UnitTestMenuContainer
{
    [UnitTest]
    public void CacheItems()
    {
        When.A<Cache>("stores items and tracks hits and misses",
            () => new Cache(false),
            (cache) =>
            {
                TestCacheable testObj = new TestCacheable { Name = "item1", Value = "hello world" };
                CacheItem item = cache.Add((object)testObj);

                // Query matching all items - increments hits
                cache.Query(new Predicate<object>(v => true)).ToList();
                // Query matching no items - increments misses
                cache.Query(new Predicate<object>(v => false)).ToList();

                return item;
            })
        .TheTest
        .ShouldPass(because =>
        {
            CacheItem result = because.ResultAs<CacheItem>();
            because.ItsTrue("MemorySize is greater than 0", result.MemorySize > 0);
            because.ItsTrue("Hits is 1 after matching query", result.Hits == 1);
            because.ItsTrue("Misses is 1 after non-matching query", result.Misses == 1);
        })
        .SoBeHappy()
        .UnlessItFailed();
    }

    [UnitTest]
    public void AddAndRetrieveFromCache()
    {
        When.A<Cache>("adds and retrieves items by query",
            () => new Cache(false),
            (cache) =>
            {
                cache.Add((object)new TestCacheable { Name = "alpha", Value = "first" });
                cache.Add((object)new TestCacheable { Name = "beta", Value = "second" });
                cache.Add((object)new TestCacheable { Name = "gamma", Value = "third" });

                // Retrieve by predicate
                List<CacheItem> found = cache.Query(new Predicate<object>(v =>
                {
                    TestCacheable? tc = v as TestCacheable;
                    return tc?.Name == "alpha";
                })).ToList();

                // Query again to verify hit count increments
                cache.Query(new Predicate<object>(v =>
                {
                    TestCacheable? tc = v as TestCacheable;
                    return tc?.Name == "alpha";
                })).ToList();

                return found;
            })
        .TheTest
        .ShouldPass(because =>
        {
            List<CacheItem> found = because.ResultAs<List<CacheItem>>();
            because.ItsTrue("found exactly 1 item", found.Count == 1);
            TestCacheable retrieved = found[0].ValueAs<TestCacheable>();
            because.ItsTrue("retrieved Name is alpha", retrieved.Name == "alpha");
            because.ItsTrue("retrieved Value is first", retrieved.Value == "first");
            because.ItsTrue("hit count is 2 after two matching queries", found[0].Hits == 2);
        })
        .SoBeHappy()
        .UnlessItFailed();
    }

    [UnitTest]
    public void EvictByPredicate()
    {
        When.A<Cache>("evicts items matching a predicate",
            () => new Cache(false),
            (cache) =>
            {
                cache.Add((object)new TestCacheable { Name = "keep1", Value = "important" });
                cache.Add((object)new TestCacheable { Name = "keep2", Value = "important" });
                cache.Add((object)new TestCacheable { Name = "remove1", Value = "temporary" });
                cache.Add((object)new TestCacheable { Name = "remove2", Value = "temporary" });
                cache.Add((object)new TestCacheable { Name = "keep3", Value = "important" });

                int countBefore = cache.Query(new Predicate<object>(v => true)).ToList().Count;
                uint sizeBefore = cache.ItemsMemorySize;

                // Evict items with "temporary" value
                cache.Evict(v =>
                {
                    TestCacheable? tc = v as TestCacheable;
                    return tc?.Value == "temporary";
                });

                int countAfter = cache.Query(new Predicate<object>(v => true)).ToList().Count;
                uint sizeAfter = cache.ItemsMemorySize;

                // Verify only "important" items remain
                List<string> remainingNames = cache.Query<TestCacheable>(t => true)
                    .Select(t => t.Name).OrderBy(n => n).ToList();

                return new object[] { countBefore, countAfter, sizeBefore, sizeAfter, remainingNames };
            })
        .TheTest
        .ShouldPass(because =>
        {
            object[] r = because.ResultAs<object[]>();
            int countBefore = (int)r[0];
            int countAfter = (int)r[1];
            uint sizeBefore = (uint)r[2];
            uint sizeAfter = (uint)r[3];
            List<string> remainingNames = (List<string>)r[4];
            because.ItsTrue($"started with 5 items (got {countBefore})", countBefore == 5);
            because.ItsTrue($"evicted 2 items (before={countBefore}, after={countAfter})", countAfter == 3);
            because.ItsTrue($"memory decreased (before={sizeBefore}, after={sizeAfter})", sizeAfter < sizeBefore);
            because.ItsTrue("remaining items are the 'important' ones", remainingNames.SequenceEqual(new[] { "keep1", "keep2", "keep3" }));
        })
        .SoBeHappy()
        .UnlessItFailed();
    }

    [UnitTest]
    public void CompareByHitsAndMisses()
    {
        When.A<CacheItemComparer>("sorts cache items by hits and misses",
            () => new CacheItemComparer { Hits = true, SortOrder = SortOrder.Ascending },
            (comparer) =>
            {
                IMetaProvider metaProvider = MetaProvider.Default;

                CacheItem low = new CacheItem(new TestCacheable { Name = "low" }, metaProvider);
                low.Hits = 1;
                low.Misses = 2;

                CacheItem high = new CacheItem(new TestCacheable { Name = "high" }, metaProvider);
                high.Hits = 10;
                high.Misses = 20;

                int hitsAsc = comparer.Compare(low, high);

                comparer.SortOrder = SortOrder.Descending;
                int hitsDesc = comparer.Compare(low, high);

                comparer.Misses = true;
                comparer.SortOrder = SortOrder.Ascending;
                int missesAsc = comparer.Compare(low, high);

                comparer.SortOrder = SortOrder.Descending;
                int missesDesc = comparer.Compare(low, high);

                return new int[] { hitsAsc, hitsDesc, missesAsc, missesDesc };
            })
        .TheTest
        .ShouldPass(because =>
        {
            int[] r = because.ResultAs<int[]>();
            because.ItsTrue("ascending hits: low < high is negative", r[0] < 0);
            because.ItsTrue("descending hits: low vs high is positive", r[1] > 0);
            because.ItsTrue("ascending misses: low < high is negative", r[2] < 0);
            because.ItsTrue("descending misses: low vs high is positive", r[3] > 0);
        })
        .SoBeHappy()
        .UnlessItFailed();
    }

    [UnitTest]
    public void ManageCaches()
    {
        When.A<CacheManager>("creates and manages per-type caches",
            () => new CacheManager(),
            (manager) =>
            {
                Cache first = manager.CacheFor<TestCacheable>();
                Cache second = manager.CacheFor<TestCacheable>();
                Cache other = manager.CacheFor<string>();

                first.Add((object)new TestCacheable { Name = "managed", Value = "item" });
                Thread.Sleep(100);

                bool sameInstance = ReferenceEquals(first, second);
                bool differentForType = !ReferenceEquals(first, other);
                uint totalSize = manager.AllCacheSize;

                manager.Clear();
                uint sizeAfterClear = manager.AllCacheSize;

                return new object[] { sameInstance, differentForType, totalSize, sizeAfterClear };
            })
        .TheTest
        .ShouldPass(because =>
        {
            object[] r = because.ResultAs<object[]>();
            because.ItsTrue("same type returns same cache instance", (bool)r[0]);
            because.ItsTrue("different types return different cache instances", (bool)r[1]);
            because.ItsTrue($"AllCacheSize > 0 after adding item ({r[2]})", (uint)r[2] > 0);
            because.ItsTrue("AllCacheSize is 0 after clear", (uint)r[3] == 0);
        })
        .SoBeHappy()
        .UnlessItFailed();
    }

    [UnitTest]
    public void WrapRepositoryWithCaching()
    {
        // Simulates the CachingRepository pattern: CacheManager + Cache as a caching layer over data access
        When.A<CacheManager>("wraps data access with caching via CacheManager",
            () => new CacheManager(),
            (manager) =>
            {
                Cache cache = manager.CacheFor<TestCacheable>();

                // Simulate Create → cache.Add
                TestCacheable created = new TestCacheable { Name = "user1", Value = "created-data" };
                CacheItem addedItem = cache.Add((object)created);

                Thread.Sleep(200); // let Organize populate index

                // Simulate Retrieve → cache.RetrieveByName
                CacheItem retrieved = cache.RetrieveByName("user1");

                bool wasFound = retrieved != null;
                string retrievedName = retrieved?.ValueAs<TestCacheable>()?.Name ?? "not found";
                int hitCount = retrieved?.Hits ?? 0;

                // Simulate Delete → should use DeleteNotSupportedException
                string deleteMessage = string.Empty;
                try
                {
                    throw new DeleteNotSupportedException("user1");
                }
                catch (DeleteNotSupportedException ex)
                {
                    deleteMessage = ex.Message;
                }

                return new object[] { wasFound, retrievedName, hitCount, deleteMessage };
            })
        .TheTest
        .ShouldPass(because =>
        {
            object[] r = because.ResultAs<object[]>();
            because.ItsTrue("item was found in cache by name", (bool)r[0]);
            because.ItsTrue("retrieved name matches", "user1".Equals(r[1]));
            because.ItsTrue("hit count incremented on retrieve", (int)r[2] >= 1);
            because.ItsTrue("delete exception mentions CachingRepository", ((string)r[3]).Contains("CachingRepository"));
        })
        .SoBeHappy()
        .UnlessItFailed();
    }

    [UnitTest]
    public void EvictByCount()
    {
        When.A<Cache>("evicts the least-hit items by count",
            () => new Cache(false),
            (cache) =>
            {
                CacheItem a = cache.Add((object)new TestCacheable { Name = "a", Value = "alpha" });
                CacheItem b = cache.Add((object)new TestCacheable { Name = "b", Value = "bravo" });
                CacheItem c = cache.Add((object)new TestCacheable { Name = "c", Value = "charlie" });
                CacheItem d = cache.Add((object)new TestCacheable { Name = "d", Value = "delta" });
                CacheItem e = cache.Add((object)new TestCacheable { Name = "e", Value = "echo" });

                // Assign different hit counts so eviction order is deterministic
                a.Hits = 10;
                b.Hits = 5;
                c.Hits = 8;
                d.Hits = 1;  // least hits — should be evicted
                e.Hits = 3;  // second least — should be evicted

                uint sizeBefore = cache.ItemsMemorySize;

                // Evict the 2 least-hit items
                cache.Evict(2);

                List<string> remainingNames = cache.Query<TestCacheable>(t => true)
                    .Select(t => t.Name).OrderBy(n => n).ToList();
                uint sizeAfter = cache.ItemsMemorySize;

                return new object[] { remainingNames, sizeBefore, sizeAfter };
            })
        .TheTest
        .ShouldPass(because =>
        {
            object[] r = because.ResultAs<object[]>();
            List<string> remainingNames = (List<string>)r[0];
            uint sizeBefore = (uint)r[1];
            uint sizeAfter = (uint)r[2];
            because.ItsTrue($"3 items remain (got {remainingNames.Count})", remainingNames.Count == 3);
            because.ItsTrue("highest-hit item 'a' kept", remainingNames.Contains("a"));
            because.ItsTrue("second-highest 'c' kept", remainingNames.Contains("c"));
            because.ItsTrue("third-highest 'b' kept", remainingNames.Contains("b"));
            because.ItsTrue($"memory decreased (before={sizeBefore}, after={sizeAfter})", sizeAfter < sizeBefore);
        })
        .SoBeHappy()
        .UnlessItFailed();
    }

    [UnitTest]
    public void CacheQueryResults()
    {
        When.A<Cache>("queries and filters cached results with type safety",
            () => new Cache(false),
            (cache) =>
            {
                cache.Add((object)new TestCacheable { Name = "apple", Value = "fruit" });
                cache.Add((object)new TestCacheable { Name = "banana", Value = "fruit" });
                cache.Add((object)new TestCacheable { Name = "carrot", Value = "vegetable" });
                cache.Add((object)new TestCacheable { Name = "date", Value = "fruit" });

                // Typed query for fruits
                List<TestCacheable> fruits = cache.Query<TestCacheable>(t => t.Value == "fruit").ToList();

                // Typed query for vegetables
                List<TestCacheable> vegs = cache.Query<TestCacheable>(t => t.Value == "vegetable").ToList();

                // Verify misses tracked on non-matching items
                List<CacheItem> allItems = cache.Query(new Predicate<object>(v => true)).ToList();
                int totalMisses = allItems.Sum(ci => ci.Misses);

                List<string> fruitNames = fruits.Select(f => f.Name).OrderBy(n => n).ToList();

                return new object[] { fruits.Count, vegs.Count, fruitNames, totalMisses };
            })
        .TheTest
        .ShouldPass(because =>
        {
            object[] r = because.ResultAs<object[]>();
            because.ItsTrue($"found 3 fruits (got {r[0]})", (int)r[0] == 3);
            because.ItsTrue($"found 1 vegetable (got {r[1]})", (int)r[1] == 1);
            List<string> fruitNames = (List<string>)r[2];
            because.ItsTrue("fruits include apple", fruitNames.Contains("apple"));
            because.ItsTrue("fruits include banana", fruitNames.Contains("banana"));
            because.ItsTrue("fruits include date", fruitNames.Contains("date"));
            because.ItsTrue($"misses tracked on non-matching items ({r[3]})", (int)r[3] > 0);
        })
        .SoBeHappy()
        .UnlessItFailed();
    }

    [UnitTest]
    public void StopTheBackgroundGroomer()
    {
        When.A<Cache>("stops its background groomer thread without Thread.Abort",
            () => new Cache(true),
            (cache) => cache.StopGroomer())
        .TheTest
        .ShouldPass<bool>((because, _, stopped) =>
        {
            because.ItsTrue("the groomer thread exited within the wait", stopped);
        })
        .SoBeHappy()
        .UnlessItFailed();
    }

    [UnitTest]
    public void ReportStoppedWhenNoGroomerWasStarted()
    {
        When.A<Cache>("is asked to stop a groomer it never started",
            () => new Cache(false),
            (cache) => cache.StopGroomer())
        .TheTest
        .ShouldPass<bool>((because, _, stopped) =>
        {
            because.ItsTrue("there is no groomer thread left running", stopped);
        })
        .SoBeHappy()
        .UnlessItFailed();
    }

    [UnitTest]
    public void RestartTheGroomerAfterStoppingIt()
    {
        When.A<Cache>("stops, restarts and stops its groomer again",
            () => new Cache(true),
            (cache) =>
            {
                bool firstStop = cache.StopGroomer();
                cache.StartGroomer();
                bool secondStop = cache.StopGroomer();
                return new GroomerRestartOutcome(firstStop, secondStop);
            })
        .TheTest
        .ShouldPass<GroomerRestartOutcome>((because, _, outcome) =>
        {
            because.ItsTrue("the first groomer stopped", outcome.FirstStop);
            because.ItsTrue("the restarted groomer stopped", outcome.SecondStop);
        })
        .SoBeHappy()
        .UnlessItFailed();
    }

    [UnitTest]
    public void RunOneGroomerWhenStartedWhileAlreadyRunning()
    {
        When.A<GroomerProbeCache>("is started again while its groomer is running",
            () => new GroomerProbeCache(true),
            (cache) =>
            {
                cache.StartGroomer();
                cache.StartGroomer();
                bool oneGroomer = WaitFor(() => cache.Groomers == 1);
                bool stopped = cache.StopGroomer();
                return new SingleGroomerOutcome(oneGroomer, stopped, cache.Groomers);
            })
        .TheTest
        .ShouldPass<SingleGroomerOutcome>((because, _, outcome) =>
        {
            because.ItsTrue("the earlier groomers exited, leaving one", outcome.OneGroomerAfterRestart);
            because.ItsTrue("stopping reports the groomer exited", outcome.Stopped);
            because.ItsTrue($"no groomer is left ({outcome.GroomersAfterStop})", outcome.GroomersAfterStop == 0);
        })
        .SoBeHappy()
        .UnlessItFailed();
    }

    [UnitTest]
    public void LeaveOneGroomerWhenRestartedFromAnEvictionListener()
    {
        When.A<GroomerProbeCache>("is stopped and started by an eviction listener running on the groomer thread",
            () => new GroomerProbeCache(true),
            (cache) =>
            {
                Thread testThread = Thread.CurrentThread;
                ManualResetEventSlim listenerRan = new ManualResetEventSlim(false);
                bool stopFromGroomer = true;
                bool listenerOnGroomerThread = false;
                int listenerCalls = 0;
                cache.Evicted += (sender, args) =>
                {
                    if (Interlocked.Increment(ref listenerCalls) > 1)
                    {
                        return;
                    }
                    listenerOnGroomerThread = Thread.CurrentThread != testThread;
                    stopFromGroomer = cache.StopGroomer();
                    cache.StartGroomer();
                    listenerRan.Set();
                };

                CacheItem item = cache.Add((object)new TestCacheable { Name = "a", Value = "alpha" });
                bool indexed = WaitFor(() => cache.Retrieve(item.Uuid) != null);
                cache.QueueEviction(item.Uuid);
                bool ran = listenerRan.Wait(5000);
                bool oneGroomer = WaitFor(() => cache.Groomers == 1);

                // The groomer the listener started still grooms.
                CacheItem second = cache.Add((object)new TestCacheable { Name = "b", Value = "bravo" });
                bool secondIndexed = WaitFor(() => cache.Retrieve(second.Uuid) != null);
                cache.QueueEviction(second.Uuid);
                bool secondEvicted = WaitFor(() => Volatile.Read(ref listenerCalls) == 2);

                bool stopped = cache.StopGroomer();
                return new ListenerRestartOutcome(indexed && secondIndexed, ran, listenerOnGroomerThread, stopFromGroomer, oneGroomer, secondEvicted, stopped, cache.Groomers);
            })
        .TheTest
        .ShouldPass<ListenerRestartOutcome>((because, _, outcome) =>
        {
            because.ItsTrue("the items were indexed", outcome.Indexed);
            because.ItsTrue("the listener ran", outcome.ListenerRan);
            because.ItsTrue("the listener ran on the groomer thread", outcome.ListenerOnGroomerThread);
            because.ItsTrue("StopGroomer called from the groomer reports it still running", !outcome.StopFromGroomer);
            because.ItsTrue("the stopped groomer exited instead of being revived by the restart", outcome.OneGroomerAfterRestart);
            because.ItsTrue("the groomer started by the listener grooms", outcome.RestartedGroomerEvicted);
            because.ItsTrue("the restarted groomer stops", outcome.Stopped);
            because.ItsTrue($"no groomer is left ({outcome.GroomersAfterStop})", outcome.GroomersAfterStop == 0);
        })
        .SoBeHappy()
        .UnlessItFailed();
    }

    [UnitTest]
    public void ExitWhenStoppedFromItsOwnGroomerThread()
    {
        When.A<GroomerProbeCache>("is stopped by an eviction listener running on the groomer thread",
            () => new GroomerProbeCache(true),
            (cache) =>
            {
                bool stopFromGroomer = true;
                ManualResetEventSlim listenerRan = new ManualResetEventSlim(false);
                cache.Evicted += (sender, args) =>
                {
                    stopFromGroomer = cache.StopGroomer();
                    listenerRan.Set();
                };
                CacheItem item = cache.Add((object)new TestCacheable { Name = "a", Value = "alpha" });
                bool indexed = WaitFor(() => cache.Retrieve(item.Uuid) != null);
                cache.QueueEviction(item.Uuid);
                bool ran = listenerRan.Wait(5000);
                bool exited = WaitFor(() => cache.Groomers == 0);
                return new SelfStopOutcome(indexed, ran, stopFromGroomer, exited, cache.StopGroomer());
            })
        .TheTest
        .ShouldPass<SelfStopOutcome>((because, _, outcome) =>
        {
            because.ItsTrue("the item was indexed", outcome.Indexed);
            because.ItsTrue("the listener ran", outcome.ListenerRan);
            because.ItsTrue("the call from the groomer thread reports it still running", !outcome.StopFromGroomer);
            because.ItsTrue("the groomer exits once the groom returns", outcome.GroomerExited);
            because.ItsTrue("a later stop reports nothing running", outcome.StopAfterwards);
        })
        .SoBeHappy()
        .UnlessItFailed();
    }

    [UnitTest]
    public void GroomOnDemandAfterTheGroomerIsStopped()
    {
        When.A<GroomerProbeCache>("is groomed directly after its background groomer stopped",
            () => new GroomerProbeCache(true),
            (cache) =>
            {
                CacheItem item = cache.Add((object)new TestCacheable { Name = "a", Value = "alpha" });
                bool indexed = WaitFor(() => cache.Retrieve(item.Uuid) != null);
                bool stopped = cache.StopGroomer();
                int evictions = 0;
                cache.Evicted += (sender, args) => Interlocked.Increment(ref evictions);
                cache.QueueEviction(item.Uuid);
                Thread.Sleep(200);
                int evictionsBeforeGroom = Volatile.Read(ref evictions);
                // Counts Evicted events, not items: Evict(CacheItem) leaves the item in place (bam.caching#4).
                cache.Groom();
                return new OnDemandGroomOutcome(indexed, stopped, evictionsBeforeGroom, Volatile.Read(ref evictions));
            })
        .TheTest
        .ShouldPass<OnDemandGroomOutcome>((because, _, outcome) =>
        {
            because.ItsTrue("the item was indexed", outcome.Indexed);
            because.ItsTrue("the groomer stopped", outcome.Stopped);
            because.ItsTrue("nothing grooms in the background once stopped", outcome.EvictionsBeforeGroom == 0);
            because.ItsTrue("Groom works the eviction queue", outcome.EvictionsAfterGroom == 1);
        })
        .SoBeHappy()
        .UnlessItFailed();
    }

    private static bool WaitFor(Func<bool> condition)
    {
        return SpinWait.SpinUntil(condition, 5000);
    }

    private sealed class GroomerProbeCache : Cache
    {
        public GroomerProbeCache(bool groomInBackground) : base(groomInBackground)
        {
        }

        public int Groomers
        {
            get
            {
                return ActiveGroomerCount;
            }
        }
    }

    private sealed record GroomerRestartOutcome(bool FirstStop, bool SecondStop);

    private sealed record SingleGroomerOutcome(bool OneGroomerAfterRestart, bool Stopped, int GroomersAfterStop);

    private sealed record ListenerRestartOutcome(bool Indexed, bool ListenerRan, bool ListenerOnGroomerThread, bool StopFromGroomer, bool OneGroomerAfterRestart, bool RestartedGroomerEvicted, bool Stopped, int GroomersAfterStop);

    private sealed record SelfStopOutcome(bool Indexed, bool ListenerRan, bool StopFromGroomer, bool GroomerExited, bool StopAfterwards);

    private sealed record OnDemandGroomOutcome(bool Indexed, bool Stopped, int EvictionsBeforeGroom, int EvictionsAfterGroom);
}
