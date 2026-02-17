namespace Bam.Caching
{
    /// <summary>
    /// Event arguments for <see cref="CacheManager"/> events, containing the type and cache involved.
    /// </summary>
    public class CacheManagerEventArgs: EventArgs
    {
        /// <summary>
        /// Gets the short name of the type associated with this event.
        /// </summary>
        public string TypeName { get { return Type.Name; } }

        /// <summary>
        /// Gets or sets the type associated with this event.
        /// </summary>
        public Type Type { get; set; } = null!;

        /// <summary>
        /// Gets or sets the cache associated with this event.
        /// </summary>
        public Cache Cache { get; set; } = null!;
    }
}
