using System;
using BenchmarkDotNet.Attributes;

namespace Bloomn.Benchmarks
{
    [SimpleJob(launchCount: 1, warmupCount: 1, targetCount: 5, invocationCount: 100)]
    [MaxIterationCount(100)]
    [MemoryDiagnoser]
    public class DefaultHasherBenchmarks
    {

        public const int OperationsPerInvoke = 100;
        
        public Func<int, uint> IntHasher { get; set; }

        public DefaultHasherBenchmarks()
        {
            IntHasher = CreateHasher<int>();
        }

        public Func<T, uint> CreateHasher<T>()
        {
            var factory = (IKeyHasherFactory<T>) new DefaultHasherFactoryV1();
            return factory.CreateHasher(1, 1234567);
        }
        
        [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
        public void IntHasherBenchmark()
        {
            var hasher = IntHasher;
            uint total = 0;
            for (int i = 0; i < OperationsPerInvoke; i++)
            {
                total += hasher(i);
            }
        }
    }
}