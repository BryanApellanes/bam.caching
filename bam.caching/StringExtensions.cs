using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bam.Caching;

namespace Bam.Net.Caching
{
    public static class StringExtensions
    {
        public static Task<byte[]> GZipAsync(this string value, Encoding enc = null)
        {
            return Task.Run(() => value.GZip(enc));
        }

        public static byte[] GZip(this string value, Encoding enc = null)
        {
            enc = enc ?? Encoding.UTF8;
            return enc.GetBytes(value).GZip();
        }
    }
}
