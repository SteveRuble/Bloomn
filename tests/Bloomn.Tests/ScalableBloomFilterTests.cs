using System.Collections.Concurrent;
using System.Linq;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Bloomn.Tests
{
    public class ScalableBloomFilterTests : BloomFilterTestsBase
    {
        public ScalableBloomFilterTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
        }

        public override IBloomFilter Create(BloomFilterOptions options, BloomFilterParameters parameters)
        {
            parameters = parameters.WithScaling(4, 0.8);
            
            return new ScalingBloomFilter(options, new BloomFilterState()
            {
                Parameters = parameters
            });
        }
        
        /// <summary>
        /// These tests are intended to be more debuggable and to provide a baseline for correct behavior.
        /// They use the same set of keys on every run and log events.
        /// </summary>
        /// <param name="count"></param>
        /// <param name="capacity"></param>
        /// <param name="errorRate"></param>
        /// <param name="threads"></param>
        /// <param name="maxErrorRate"></param>
        [Theory]
        [InlineData(10000, 1000,  0.01, 1)]
        [InlineData(10000, 1000,  0.01, 8)]
        public void PredictableStringsTestsWithScaling(int count, int capacity, double errorRate, int threads)
        {
            var parameters = new BloomFilterParameters("test")
                .WithCapacityAndErrorRate(capacity, errorRate);

            VerifyContracts(
                parameters,
                () => Create(Options, parameters),
                PredictableStrings,
                1,
                count,
                threads
            );
        }    
        
        /// <summary>
        /// These tests focus on performance and statistical correctness. They use random values on each run
        /// and run many reps of the same parameters to build a statistical picture of the behavior of the implementation.
        /// </summary>
        /// <param name="reps"></param>
        /// <param name="count"></param>
        /// <param name="capacity"></param>
        /// <param name="errorRate"></param>
        /// <param name="threads"></param>
        [Theory]
        [InlineData(4, 10000, 1000,  0.01, 1)]
        [InlineData(4, 10000, 1000,  0.01, 4)]
        public void RandomStringsTestsWithScaling(int reps, int count, int capacity, double errorRate, int threads)
        {
            var parameters = new BloomFilterParameters("test")
                .WithCapacityAndErrorRate(capacity, errorRate);

            ChartFalsePositiveRates(
                parameters,
                () => Create(Options, parameters),
                RandomStrings,
                count,
                1000,
                100
                );

            VerifyContracts(
                parameters,
                () => Create(Options, parameters),
                RandomStrings,
                reps,
                count,
                threads
            );
        }    

    }
}