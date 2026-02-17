using System.Text;

namespace Bam.Caching
{
    /// <summary>
    /// Provides GZip compression extension methods for strings.
    /// </summary>
    public static class StringExtensions
    {
        /// <summary>
        /// Asynchronously compresses the string using GZip encoding.
        /// </summary>
        /// <param name="value">The string to compress.</param>
        /// <param name="enc">The encoding to use. Defaults to UTF-8 if null.</param>
        /// <returns>A task that resolves to the GZip-compressed byte array.</returns>
        public static Task<byte[]> GZipAsync(this string value, Encoding enc = null!)
        {
            return Task.Run(() => value.GZip(enc));
        }

        /// <summary>
        /// Compresses the string using GZip encoding.
        /// </summary>
        /// <param name="value">The string to compress.</param>
        /// <param name="enc">The encoding to use. Defaults to UTF-8 if null.</param>
        /// <returns>The GZip-compressed byte array.</returns>
        public static byte[] GZip(this string value, Encoding enc = null!)
        {
            enc = enc ?? Encoding.UTF8;
            return enc.GetBytes(value).GZip();
        }
    }
}
