// See https://aka.ms/new-console-template for more information

using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.CoreRt;
using Benchmarks;

Console.WriteLine("Hello, World!");

var aotToolchain = CoreRtToolchain.CreateBuilder()
                                  .UseCoreRtNuGet()
                                  .DisplayName("NativeAOT")
                                  .TargetFrameworkMoniker("net6.0")
                                  .ToToolchain();

var aotConfig = DefaultConfig.Instance
                             .AddJob(Job.Default.AsBaseline().WithRuntime(CoreRuntime.Core60))
                             .AddJob(Job.Default.WithToolchain(aotToolchain));

BenchmarkRunner.Run<RefListVsList>(aotConfig);