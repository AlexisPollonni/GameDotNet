using BenchmarkDotNet.Attributes;
using GameDotNet.Core.ECS;

namespace GameDotNet.Benchmarks;

public class TypeIdBenchmarks
{
    [Benchmark]
    public void TypeIdGet()
    {
        var id = TypeId.Get<int>();
    }
}