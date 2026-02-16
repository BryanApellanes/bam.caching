using System.IO.Compression;

namespace Bam.Caching
{
    /// <summary>
    /// Provides GZip compression extension methods for byte arrays.
    /// </summary>
    public static class ByteArrayExtensions
    {
        /// <summary>
        /// Asynchronously compresses the byte array using GZip.
        /// </summary>
        /// <param name="data">The byte array to compress.</param>
        /// <returns>A task that resolves to the GZip-compressed byte array.</returns>
        public static Task<byte[]> GZipAsync(this byte[] data)
        {
            return Task.Run(() => data.GZip());
        }

        /// <summary>
        /// Compresses the byte array using GZip.
        /// </summary>
        /// <param name="data">The byte array to compress.</param>
        /// <returns>The GZip-compressed byte array.</returns>
        public static byte[] GZip(this byte[] data)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                using (GZipStream zipStream = new GZipStream(ms, CompressionMode.Compress, true))
                {
                    zipStream.Write(data, 0, data.Length);
                }

                data = ms.ToArray();
            }

            return data;
        }

    }
}
