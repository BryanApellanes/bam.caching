using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bam.Caching
{
    public class QueryCacheEventArgs: EventArgs
    {
        public QueryContext QueryContext { get; set; }
    }
}
