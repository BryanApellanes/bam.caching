/*
	Copyright © Bryan Apellanes 2015  
*/

namespace Bam.Caching
{
    /// <summary>
    /// Event arguments for cache eviction events, containing the cache and the evicted items.
    /// </summary>
    /// <seealso cref="System.EventArgs" />
    public class CacheEvictionEventArgs: EventArgs
	{
        /// <summary>
        /// Initializes a new instance of the <see cref="CacheEvictionEventArgs"/> class.
        /// </summary>
        public CacheEvictionEventArgs() { }

        /// <summary>
        /// Gets or sets the cache.
        /// </summary>
        /// <value>
        /// The cache.
        /// </value>
        public Cache Cache { get; set; } = null!;

        /// <summary>
        /// Gets or sets the evicted items.
        /// </summary>
        /// <value>
        /// The evicted items.
        /// </value>
        public CacheItem[] EvictedItems { get; set; } = null!;
	}
}
