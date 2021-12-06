using BenchmarkDotNet.Attributes;
using Core.ECS;

namespace Benchmarks;

[StopOnFirstError]
public class RefListVsList
{
    public const int IterationCount = 10000;

    [Benchmark]
    public void RefStructListAdd()
    {
        var list = new RefStructList<Guid>();

        for (var i = 0; i < IterationCount; i++)
        {
            var guid = Guid.NewGuid();
            list.Add(in guid);
        }
    }

    [Benchmark]
    public void ListAdd()
    {
        var list = new List<Guid>();

        for (var i = 0; i < IterationCount; i++) list.Add(Guid.NewGuid());
    }
}