namespace Bam.Caching
{
    /// <summary>
    /// When implemented, provides a mechanism to report
    /// the size of an object in memory.
    /// </summary>
    public interface IMemorySize
    {
        /// <summary>
        /// Get the size of the object in memory.
        /// </summary>
        /// <returns></returns>
        int MemorySize();
    }
}
