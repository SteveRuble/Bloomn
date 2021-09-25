using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Bloomn.Tests
{
    public class ScalingFilterTests : BloomFilterTestsBase
    {
        public ScalingFilterTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
        }

        public override IBloomFilter<string> Create(BloomFilterOptions<string> options, BloomFilterParameters parameters)
        {
            parameters = parameters.WithScaling();

            return new ScalingBloomFilter<string>(options, new BloomFilterState
            {
                Parameters = parameters
            });
        }

        /// <summary>
        ///     These tests are intended to be more debuggable and to provide a baseline for correct behavior.
        ///     They use the same set of keys on every run and log events.
        /// </summary>
        /// <param name="count"></param>
        /// <param name="capacity"></param>
        /// <param name="errorRate"></param>
        /// <param name="threads"></param>
        /// <param name="maxErrorRate"></param>
        [Theory]
        [InlineData(10000, 1000, 0.01, 1)]
        [InlineData(10000, 1000, 0.01, 8)]
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
        ///     These tests focus on performance and statistical correctness. They use random values on each run
        ///     and run many reps of the same parameters to build a statistical picture of the behavior of the implementation.
        /// </summary>
        /// <param name="reps"></param>
        /// <param name="count"></param>
        /// <param name="capacity"></param>
        /// <param name="errorRate"></param>
        /// <param name="threads"></param>
        [Theory]
        [InlineData(4, 10000, 1000, 0.01, 1)]
        [InlineData(4, 10000, 1000, 0.01, 4)]
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


        [Fact]
        public void CanExportAndImportState()
        {
            var parameters = new BloomFilterParameters("test")
                .WithCapacityAndErrorRate(100, 0.1)
                .WithScaling();

            var first = new ScalingBloomFilter<string>(new BloomFilterOptions<string>
            {
                Callbacks = new Callbacks
                {
                    OnScaled = (id, p) => TestOutputHelper.WriteLine($"{id} {p}")
                }
            }, parameters);

            // Populate with data
            ChartFalsePositiveRates(parameters, () => first, RandomStrings, 10000, 100, 1000);

            var firstState = first.GetState();

            var serializedFirstState = firstState.Serialize();

            var secondState = BloomFilterState.Deserialize(serializedFirstState);

            var second = new ScalingBloomFilter<string>(secondState);

            second.Parameters.Should().BeEquivalentTo(first.Parameters);

            second.Count.Should().Be(first.Count);

            var fpr = GetFalsePositiveRate(second, 10000);

            fpr.Should().BeGreaterThan(0, "there should be some false positives");
            fpr.Should().BeLessOrEqualTo(parameters.Dimensions.FalsePositiveProbability, "the filter should behave correctly");
        }
    }
}