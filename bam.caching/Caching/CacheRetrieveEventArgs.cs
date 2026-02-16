namespace Bam.Caching
{
    /// <summary>
    /// Typed event arguments for cache retrieve events, containing the retrieved item.
    /// </summary>
    /// <typeparam name="T">The type of the retrieved item.</typeparam>
    public class CacheRetrieveEventArgs<T>: EventArgs
    {
        /// <summary>
        /// Gets or sets the retrieved item.
        /// </summary>
        public T Item { get; set; }
    }

    /// <summary>
    /// Event arguments for cache retrieve events, containing the retrieved item and its type.
    /// </summary>
    public class CacheRetrieveEventArgs: EventArgs
    {
        /// <summary>
        /// Gets or sets the type of the retrieved item.
        /// </summary>
        public Type Type { get; set; }

        /// <summary>
        /// Gets or sets the retrieved item.
        /// </summary>
        public object Item { get; set; }
    }
}
