namespace Bam.Caching
{
    /// <summary>
    /// Event arguments for <see cref="CachingRepository"/> events such as typeless queries and differing type detection.
    /// </summary>
    public class CachingRepositoryEventArgs: EventArgs
    {
        /// <summary>
        /// Gets or sets the property name used in the query.
        /// </summary>
        public string PropertyName { get; set; }

        /// <summary>
        /// Gets or sets the parameter value used in the query.
        /// </summary>
        public object ParameterValue { get; set; }

        /// <summary>
        /// Gets or sets a comma-delimited string of differing type names found during a query.
        /// </summary>
        public string DifferingTypes { get; set; }
    }
}
