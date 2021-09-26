using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using BenchmarkDotNet.Attributes;

namespace Bloomn.Benchmarks
{
    [SimpleJob(launchCount: 1, warmupCount: 1, targetCount: 5, invocationCount: 100)]
    [MaxIterationCount(100)]
    [MemoryDiagnoser]
    public class SingleItemBenchmarks
    {
        public const int OperationsPerInvoke = 100;

        public int KeyIndex { get; set; }
        public readonly IBloomFilter<int> Fixed;
        public readonly IBloomFilter<int> Scaling;

        public SingleItemBenchmarks()
        {
            Scaling = BloomFilter.Builder<int>()
                                 .WithOptions(x => x.WithCapacityAndFalsePositiveProbability(1000, 0.02)
                                                    .WithScaling(4, 0.9))
                                 .Build();
            Fixed = BloomFilter.Builder<int>()
                               .WithOptions(x => x.WithCapacityAndFalsePositiveProbability(1000, 0.02)
                                                  .WithScaling(4, 0.9))
                               .Build();

            Console.WriteLine("instantiated");
        }

        public IBloomFilter<int> Sut
        {
            get

            {
                switch (Behavior)
                {
                    case MaxCapacityBehavior.Scale: return Scaling;
                    case MaxCapacityBehavior.Throw: return Fixed;
                    default: throw new ArgumentOutOfRangeException(nameof(Behavior), Behavior.ToString());
                }
            }
        }

        [Params(MaxCapacityBehavior.Scale, MaxCapacityBehavior.Throw)]
        public MaxCapacityBehavior Behavior { get; set; }


        [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
        public void CheckOnly()
        {
            var sut = Sut;
            for (int i = 0; i < OperationsPerInvoke; i++)
            {
                KeyIndex++;
                var _ = sut.IsNotPresent(KeyIndex);
            }
        }     
        
        [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
        public void AddAndCheck()
        {
            var sut = Sut;
            for (int i = 0; i < OperationsPerInvoke; i++)
            {
                KeyIndex++;

                sut.Add(KeyIndex);

                var _ = sut.IsNotPresent(KeyIndex);
            }
        }

        [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
        public void CheckAndAdd()
        {
            var sut = Sut;
            for (int i = 0; i < OperationsPerInvoke; i++)
            {
                KeyIndex++;

                if (sut.IsNotPresent(KeyIndex))
                {
                    sut.Add(KeyIndex);
                }
            }
        }

        [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
        public void PrepareAndCommit()
        {
            var sut = Sut;
            for (int i = 0; i < OperationsPerInvoke; i++)
            {
                KeyIndex++;
                
                var prepared = sut.CheckAndPrepareAdd(KeyIndex);
                {
                    if (prepared.IsNotPresent)
                    {
                        prepared.Add();
                    }
                }     
                prepared.Dispose();
            }
        }
    }
}