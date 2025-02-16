namespace Bam.Caching
{
    public class CacheRetrieveEventArgs<T>: EventArgs
    {
        public T Item { get; set; }
    }

    public class CacheRetrieveEventArgs: EventArgs
    {
        public Type Type { get; set; }
        public object Item { get; set; }
    }
}
