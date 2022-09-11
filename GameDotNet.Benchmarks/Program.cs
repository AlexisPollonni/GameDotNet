// See https://aka.ms/new-console-template for more information

using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.NativeAot;
using GameDotNet.Benchmarks;

Console.WriteLine("Hello, World!");

var aotToolchain = NativeAotToolchain.Net60;

var aotConfig = DefaultConfig.Instance
                             .AddJob(Job.Default.AsBaseline().WithRuntime(CoreRuntime.Core60))
                             .AddJob(Job.Default.WithToolchain(aotToolchain));

BenchmarkRunner.Run<RefListVsList>(aotConfig);
BenchmarkRunner.Run<TypeIdBenchmarks>(aotConfig);