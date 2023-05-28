using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bam.Caching
{
    public static class ByteArrayExtensions
    {
        public static Task<byte[]> GZipAsync(this byte[] data)
        {
            return Task.Run(() => data.GZip());
        }

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
