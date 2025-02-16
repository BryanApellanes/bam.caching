namespace Bam.Caching
{
    public class CacheManagerEventArgs: EventArgs
    {
        public string TypeName { get { return Type.Name; } }
        public Type Type { get; set; }
        public Cache Cache { get; set; }
    }
}
