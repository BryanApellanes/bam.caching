using Bam.Console;
using Bam.Data.Repositories;
using Bam.DependencyInjection;
using NSubstitute;

namespace Bam.Application
{
    [Serializable]
    class Program
    {
        static void Main(string[] args)
        {
            string rootDirectory = Path.Combine(Path.GetTempPath(), "bam-caching-tests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(rootDirectory);

            IObjectPersister mock = Substitute.For<IObjectPersister>();
            mock.RootDirectory.Returns(rootDirectory);
            mock.WriteAsync(Arg.Any<object>()).Returns(Task.CompletedTask);
            mock.WriteAsync(Arg.Any<Type>(), Arg.Any<object>()).Returns(Task.CompletedTask);

            ServiceRegistry.Default!.Set<IObjectPersister>(mock);
            BamConsoleContext.StaticMain(args);
        }
    }
}
