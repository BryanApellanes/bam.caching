namespace Bam.Caching
{
    /// <summary>
    /// Exception thrown when a delete operation is attempted on a <see cref="CachingRepository"/>, which does not support deletes.
    /// </summary>
    public class DeleteNotSupportedException: Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DeleteNotSupportedException"/> class.
        /// </summary>
        /// <param name="identifier">An identifier for the item that was attempted to be deleted.</param>
        public DeleteNotSupportedException(string identifier)
            : base($"CachingRepository does not support the Delete operation, use CachingRepository.SourceRepository.Delete if deleting is required\r\n\t{identifier}")
        {
        }
    }
}
