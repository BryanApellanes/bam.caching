/*
	Copyright © Bryan Apellanes 2015  
*/

using Bam.Data.Repositories;

namespace Bam.Caching
{
    /// <summary>
    /// Represents an item in a cache.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <seealso cref="Bam.Caching.CacheItem" />
    [Serializable]
    public class CacheItem<T>: CacheItem where T: IMemorySize, new()
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CacheItem{T}"/> class.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="metaProvider">The meta provider.</param>
        public CacheItem(T value, IMetaProvider metaProvider) : base(value, metaProvider)
        {
        }

        /// <summary>
        /// Gets the value.
        /// </summary>
        /// <value>
        /// The value.
        /// </value>
        public new T Value => (T)base.Value;

        /// <summary>
        /// Gets the size of the value in memory.
        /// </summary>
        /// <value>
        /// The size of the memory.
        /// </value>
        public override int MemorySize => Value.MemorySize();
    }

    /// <summary>
    /// Represents a non-generic item stored in a cache, tracking hits, misses, and serialized value data.
    /// </summary>
    /// <seealso cref="Bam.Caching.CacheItem" />
    [Serializable]
	public class CacheItem
	{
        /// <summary>
        /// Initializes a new instance of the <see cref="CacheItem"/> class.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="metaProvider">The meta provider.</param>
        public CacheItem(object value, IMetaProvider metaProvider)
		{
			Value = value;
			Meta = metaProvider.GetMeta(this);
			Created = DateTime.UtcNow;
            ulong valueId = value.Property<ulong>("Id", false);
            if (valueId > 0)
            {
                Id = valueId;
            }
            Uuid = value.Property<string>("Uuid", false).Or(Uuid)!;
            Cuid = value.Property<string>("Cuid", false).Or(Cuid)!;
            Name = value.Property<string>("Name", false).Or(Name)!;
		}

        /// <summary>
        /// Gets or sets the meta.
        /// </summary>
        /// <value>
        /// The meta.
        /// </value>
        protected Meta Meta { get; set; }

        /// <summary>
        /// Gets or sets the numeric ID of the cached value.
        /// </summary>
		public ulong Id
		{
			get;
			set;
		}

        /// <summary>
        /// Gets or sets the UUID of the cached value.
        /// </summary>
		public string Uuid
		{
			get;
			set;
		}

        /// <summary>
        /// Gets or sets the CUID of the cached value.
        /// </summary>
        public string Cuid
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the name of the cached value.
        /// </summary>
        public string Name
        {
            get;
            set;
        }

		Type _type = null!;
        /// <summary>
        /// Gets the runtime type of the cached value.
        /// </summary>
		public Type Type
		{
			get
			{
				if (_type == null)
				{
					if (Value != null)
					{
						_type = Value.GetType();
					}
				}
				return _type!;
			}
		}

        /// <summary>
        /// Casts the cached value to the specified type.
        /// </summary>
        /// <typeparam name="T">The type to cast the value to.</typeparam>
        /// <returns>The cached value cast to <typeparamref name="T"/>.</returns>
		public T ValueAs<T>()
		{
			return (T)Value;
		}

        /// <summary>
        /// Gets the UTC time when this cache item was created.
        /// </summary>
		public DateTime Created { get; private set; }

        /// <summary>
        /// Gets the UTC time when this cache item was last read (hit).
        /// </summary>
		public DateTime LastRead { get; private set; }
		Serialized _serialized = null!;
        /// <summary>
        /// Gets the cached value, deserialized from its internal serialized form.
        /// </summary>
		public object Value
		{
			get
			{
				return _serialized.Deserialize()!;
			}
			private set
			{
				_serialized = new Serialized(value);
			}
		}

        /// <summary>
        /// Gets or sets the number of times this item matched a query (cache hits).
        /// </summary>
		public int Hits { get; set; }

        /// <summary>
        /// Gets or sets the number of times this item did not match a query (cache misses).
        /// </summary>
		public int Misses { get; set; }

		int _memorySize;
        /// <summary>
        /// Gets the serialized size of this cache item in bytes.
        /// </summary>
		public virtual int MemorySize
		{
			get
			{
				if (_memorySize == 0)
				{
					_memorySize = _serialized.Size;
				}

				return _memorySize;
			}
		}

		internal void IncrementHits()
		{
			++Hits;
			LastRead = DateTime.UtcNow;
		}

		internal void IncrementMisses()
		{
			++Misses;
		}

		public override int GetHashCode()
		{
			return Value.GetHashCode();
		}

		public override bool Equals(object? obj)
		{
			return Value.Equals(obj);
		}
	}
}
