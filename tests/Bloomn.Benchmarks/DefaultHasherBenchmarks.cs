using System;
using System.Collections.Generic;
using System.Linq;
using BenchmarkDotNet.Attributes;

namespace Bloomn.Benchmarks
{
    [SimpleJob(launchCount: 1, warmupCount: 1, targetCount: 5, invocationCount: 1)]
    [MemoryDiagnoser]
    public class DefaultHasherBenchmarks
    {

        public const int OperationsPerInvoke = 10000;

        public readonly List<int> Ints = Enumerable.Range(0, OperationsPerInvoke).ToList();
        
        [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
        public void IntHasherBenchmark()
        {
            var b = new HashBenchmark<int>();
            b.Execute(Ints);
        }
        
        public readonly List<double> Doubles = Enumerable.Range(0, OperationsPerInvoke).Select(x => x * (double)123.456).ToList();
        
        [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
        public void DoublesHasherBenchmark()
        {
            var b = new HashBenchmark<double>();
            b.Execute(Doubles);
        }

        public readonly List<Guid> Guids = Enumerable.Range(0, OperationsPerInvoke).Select(x => Guid.NewGuid()).ToList();
        
        [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
        public void GuidHasherBenchmark()
        {
            var b = new HashBenchmark<Guid>();
            b.Execute(Guids);
        }

        public readonly List<string> Strings = Enumerable.Range(0, OperationsPerInvoke).Select(x => Guid.NewGuid().ToString()).ToList();
        
        [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
        public void StringHasherBenchmark()
        {
            var b = new HashBenchmark<string>();
            b.Execute(Strings);
        }

        private static readonly Random Random = new Random();
        public readonly List<byte[]> Bytes = Enumerable.Range(0, OperationsPerInvoke).Select(x =>
        {
            var b = new byte[64];
            Random.NextBytes(b);
            return b;
        }).ToList();
        
        [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
        public void BytesHasherBenchmark()
        {
            var b = new HashBenchmark<byte[]>();
            b.Execute(Bytes);
        }

        public class HashBenchmark<T>
        {
            public void Execute(List<T> keys)
            {
                var factory = (IKeyHasherFactory<T>) new DefaultHasherFactoryV1();
                var hasher = factory.CreateHasher(0, 12345);
                uint total = 0;
                foreach (var key in keys)
                {
                    total += hasher(key);
                }
            }
        }
    }
}