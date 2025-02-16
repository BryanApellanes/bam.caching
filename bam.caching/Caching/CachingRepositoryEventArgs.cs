namespace Bam.Caching
{
    public class CachingRepositoryEventArgs: EventArgs
    {
        public string PropertyName { get; set; }
        public object ParameterValue { get; set; }
        public string DifferingTypes { get; set; }
    }
}
