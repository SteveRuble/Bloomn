using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using FluentAssertions;
using MathNet.Numerics.Statistics;
using Xunit;
using Xunit.Abstractions;

namespace Bloomn.Tests
{
    public abstract class BloomFilterTestsBase
    {
        private const bool AddLoggingCallbacks = false;
        public readonly ITestOutputHelper TestOutputHelper;

        protected BloomFilterTestsBase(ITestOutputHelper testOutputHelper)
        {
            TestOutputHelper = testOutputHelper;


            Options = new BloomFilterOptions<string>
            {
                Callbacks = AddLoggingCallbacks
                    ? new Callbacks
                    {
                        OnCapacityChanged = (x, i) => testOutputHelper.WriteLine($"OnCapacityChanged({x}, {i})"),
                        OnCountChanged = (x, i) => testOutputHelper.WriteLine($"OnCountChanged({x}, {i})"),
                        OnBitCountChanged = (x, i) => testOutputHelper.WriteLine($"OnBitCountChanged({x}, {i})"),
                        OnScaled = (x, p) => testOutputHelper.WriteLine($"OnScaled({x}, {p})"),
                        OnHit = x => testOutputHelper.WriteLine($"OnHit({x})"),
                        OnMiss = x => testOutputHelper.WriteLine($"OnMiss({x})"),
                        OnFalsePositive = x => testOutputHelper.WriteLine($"OnFalsePositive({x})")
                    }
                    : new Callbacks
                    {
                        // OnHit = (x) => testOutputHelper.WriteLine($"OnHit({x})"),
                        OnScaled = (x, p) => testOutputHelper.WriteLine($"OnScaled({x}, {p})")
                    }
            };
        }

        public BloomFilterOptions<string> Options { get; set; }

        protected static IEnumerable<string> PredictableStrings(int count)
        {
            var rand = new Random(1234);
            var buffer = new byte[16];
            return Enumerable.Range(0, count).Select((_, i) =>
            {
                rand.NextBytes(buffer);
                return i + "-" + new Guid(buffer);
            });
        }

        protected static IEnumerable<string> RandomStrings(int count)
        {
            return Enumerable.Range(0, count).Select((_, i) => i + "-" + Guid.NewGuid());
        }

        public void TearDown()
        {
        }

        public abstract IBloomFilter<string> Create(BloomFilterOptions<string> options, BloomFilterParameters parameters);

        [Fact]
        public void CanAddAndCheckSingleItem()
        {
            var sut = Create(Options, new BloomFilterParameters("test").WithCapacityAndErrorRate(100, 0.1));
            var key = "test string";
            sut.Add(key).Should().BeTrue("the string hasn't been added before");
            sut.IsNotPresent(key).Should().BeFalse("the string has been added before");
            sut.Add(key).Should().BeFalse("the string hasn't been added before");
            sut.IsNotPresent(key).Should().BeFalse("the string has been added");
        }

        [Fact]
        public void CanPrepareAndCommitSingleItem()
        {
            var sut = Create(Options, new BloomFilterParameters("test").WithCapacityAndErrorRate(100, 0.1));

            var key = "test string";
            using (var entry = sut.CheckAndPrepareAdd(key))
            {
                entry.IsNotPresent.Should().BeTrue("the key has not been added");
                sut.IsNotPresent(key).Should().BeTrue("the key has not been added");

                entry.Add().Should().BeTrue("the key had not been added previously");

                sut.IsNotPresent(key).Should().BeFalse("the key has been added");
            }
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
        [InlineData(1000, 0.1, 1)]
        [InlineData(10000, 0.01, 1)]
        [InlineData(10000, 0.01, 2)]
        [InlineData(10000, 0.01, 4)]
        [InlineData(10000, 0.01, 8)]
        public void PredictableStringsTests(int count, double errorRate, int threads)
        {
            var parameters = new BloomFilterParameters("test").WithCapacityAndErrorRate(count, errorRate);

            VerifyContracts(
                parameters,
                () => Create(Options, parameters),
                PredictableStrings,
                1,
                count,
                threads
            );
        }

        [Theory]
        [InlineData(11000, 0.01, 1000, 1000)]
        public void FalsePositiveDistributionIsCorrect(int count, double errorRate, int sampleSize, int sampleInterval)
        {
            var parameters = new BloomFilterParameters("test").WithCapacityAndErrorRate(count, errorRate);

            ChartFalsePositiveRates(
                parameters,
                () => Create(Options, parameters),
                RandomStrings,
                count,
                sampleSize,
                sampleInterval
            );
        }

        public double GetFalsePositiveRate(IBloomFilter<string> bloomFilter, int sampleSize)
        {
            var keys = RandomStrings(sampleSize).ToList();
            var falsePositiveCount = 0;
            foreach (var sampleKey in keys)
                if (!bloomFilter.IsNotPresent(sampleKey))
                {
                    falsePositiveCount++;
                }

            var fpr = falsePositiveCount / (double) sampleSize;
            return fpr;
        }


        public void ChartFalsePositiveRates(BloomFilterParameters parameters, Func<IBloomFilter<string>> factory, Func<int, IEnumerable<string>> keyFactory, int numberToInsert, int sampleSize, int sampleInterval)
        {
            var incrementalFalsePositiveCounts = new Dictionary<int, int>();
            var maxCapacityFalsePositiveCounts = new List<int>();
            var sut = factory();

            var magnitude = (int) Math.Log10(numberToInsert);

            var sampleKeys = keyFactory(sampleSize).ToList();

            var keys = keyFactory(numberToInsert).ToList();
            for (var i = 0; i < keys.Count; i++)
            {
                var key = keys[i];
                sut.Add(key);

                if (i % sampleInterval == 0)
                {
                    var falsePositiveCount = 0;
                    foreach (var sampleKey in sampleKeys)
                        if (!sut.IsNotPresent(sampleKey))
                        {
                            falsePositiveCount++;
                        }

                    var fpr = falsePositiveCount / (double) sampleSize;
                    incrementalFalsePositiveCounts[i] = falsePositiveCount;
                    TestOutputHelper.WriteLine($"{i.ToString().PadLeft(magnitude, ' ')}: saturation:{sut.Saturation:F4} {fpr:F4} {new string('X', falsePositiveCount)}");
                }
            }

            for (var i = 0; i < 100; i++)
            {
                var keySample = keyFactory(sampleSize).ToList();
                var falsePositiveCount = 0;
                foreach (var sampleKey in keySample)
                    if (!sut.IsNotPresent(sampleKey))
                    {
                        falsePositiveCount++;
                    }

                maxCapacityFalsePositiveCounts.Add(falsePositiveCount);
            }


            var averageIncrementalFpr = incrementalFalsePositiveCounts.Values.Select(x => x / (double) sampleSize).Average();
            TestOutputHelper.WriteLine($"Average false positive rate while adding: {averageIncrementalFpr} (expected < {parameters.Dimensions.FalsePositiveProbability})");

            var averageMaxedFpr = maxCapacityFalsePositiveCounts.Select(x => x / (double) sampleSize).Average();
            TestOutputHelper.WriteLine($"Average false positive rate while at max capacity: {averageMaxedFpr} (expected < {parameters.Dimensions.FalsePositiveProbability})");

            averageIncrementalFpr.Should().BeLessOrEqualTo(parameters.Dimensions.FalsePositiveProbability);
            var maxAcceptableErrorRate = parameters.Dimensions.FalsePositiveProbability + parameters.Dimensions.FalsePositiveProbability / 10;
            averageMaxedFpr.Should().BeLessThan(maxAcceptableErrorRate, $"the false positive rate should be close to or less than the max acceptable rate {parameters.Dimensions.FalsePositiveProbability}");
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
        /// <param name="maxErrorRate"></param>
        [Theory]
        [InlineData(10, 10000, 0.01, 1)]
        [InlineData(10, 10000, 0.05, 4)]
        public void RandomStringsTests(int reps, int count, double errorRate, int threads)
        {
            var parameters = new BloomFilterParameters("test").WithCapacityAndErrorRate(count, errorRate);

            VerifyContracts(
                parameters,
                () => Create(Options, parameters),
                RandomStrings,
                reps,
                count,
                threads
            );
        }

        public void VerifyContracts(BloomFilterParameters parameters, Func<IBloomFilter<string>> factory, Func<int, IEnumerable<string>> keyFactory, int reps, int sampleSize, int threads)
        {
            var minimumCapacityForErrorRate = 1d / parameters.Dimensions.FalsePositiveProbability * 10;
            var logOfMinimimuCapacity = Math.Ceiling(Math.Log10(minimumCapacityForErrorRate));
            var logOfCapacity = Math.Ceiling(Math.Log10(sampleSize));
            logOfMinimimuCapacity.Should().BeLessThan(logOfCapacity, "you can't get meaningful stats if the inverse of the error rate is within an order of magnitude of the sample size");

            var falsePositiveRates = new List<double>();

            var times = new List<double>();

            // warmup run:
            var timer = Stopwatch.StartNew();
            var warmupResult = GetFalsePositiveCount();
            timer.Stop();
            TestOutputHelper.WriteLine($"Warmup run completed in {timer.Elapsed.TotalMilliseconds}ms, with {warmupResult} false positives for an error rate of: {warmupResult / (double) sampleSize}");


            for (var i = 0; i < reps; i++)
            {
                timer.Restart();
                var falsePositiveCount = GetFalsePositiveCount();
                timer.Stop();
                times.Add(timer.Elapsed.TotalMilliseconds);

                var falsePositiveRate = falsePositiveCount / (double) sampleSize;
                falsePositiveRates.Add(falsePositiveRate);
            }

            var falsePositiveStats = new DescriptiveStatistics(falsePositiveRates);
            var timeStats = new DescriptiveStatistics(times);

            TestOutputHelper.WriteLine($"Expected error rate: {parameters.Dimensions.FalsePositiveProbability}");
            TestOutputHelper.WriteLine($"Reps: {reps}");
            TestOutputHelper.WriteLine($"Sample size: {sampleSize}");
            TestOutputHelper.WriteLine("Observed error rate stats:");
            TestOutputHelper.WriteLine($"  Mean: {falsePositiveStats.Mean}");
            TestOutputHelper.WriteLine($"  Min: {falsePositiveStats.Minimum}");
            TestOutputHelper.WriteLine($"  Max: {falsePositiveStats.Maximum}");
            TestOutputHelper.WriteLine($"  σ: {falsePositiveStats.StandardDeviation}");

            TestOutputHelper.WriteLine("Duration stats:");
            TestOutputHelper.WriteLine($"  Mean: {timeStats.Mean}ms");
            TestOutputHelper.WriteLine($"  Min: {timeStats.Minimum}ms");
            TestOutputHelper.WriteLine($"  Max: {timeStats.Maximum}ms");
            TestOutputHelper.WriteLine($"  σ: {timeStats.StandardDeviation}ms");

            if (reps == 1)
            {
                falsePositiveStats.Mean.Should().BeLessThan(3 * parameters.Dimensions.FalsePositiveProbability, "the actual false positive rate should be less than triple the expected rate");
            }
            else
            {
                var minusOneStandardDeviation = falsePositiveStats.Mean - falsePositiveStats.StandardDeviation;
                minusOneStandardDeviation.Should().BeLessThan(parameters.Dimensions.FalsePositiveProbability, "the actual false positive rate should be within 1 standard deviation of the expected false positive rate");
            }

            int GetFalsePositiveCount()
            {
                var sut = factory();
                var falsePositiveCount = 0;
                var count = 0;
                keyFactory(sampleSize).AsParallel()
                    .WithDegreeOfParallelism(threads)
                    .ForAll(s =>
                    {
                        var c = Interlocked.Increment(ref count);
                        var f = falsePositiveCount;
                        if (!sut.IsNotPresent(s))
                        {
                            f = Interlocked.Increment(ref falsePositiveCount);
                            var runningFpr = f / (double) c;
                            // _testOutputHelper.WriteLine($"False positive rate @ {c}: {runningFpr}");
                        }


                        sut.Add(s);
                    });
                return falsePositiveCount;
            }
        }


        // [Fact]
        // public void CanAddAndCheckBloomFilterConcurrently()
        // {
        //     // We split the capacity between the training set and the verification set.
        //     var strings = Strings.Value;
        //
        //     var count = strings.Count;
        //     var initialCapacity = count / 10;
        //     var errorRate = 0.01;
        //     var acceptableErrorRate = errorRate * 10;
        //     var acceptableErrorCount = (int) Math.Ceiling(count * acceptableErrorRate);
        //
        //     var parameters = new BloomFilterParameters("test", initialCapacity, errorRate);
        //     var sut = new ScalingBloomFilter(parameters);
        //
        //     var hitOnAddCount = 0;
        //
        //     strings.AsParallel().WithDegreeOfParallelism(8)
        //         .ForAll(s =>
        //         {
        //             if (!sut.Add(s))
        //             {
        //                 Interlocked.Increment(ref hitOnAddCount);
        //             }
        //         });
        //
        //     hitOnAddCount.Should().BeLessThan(acceptableErrorCount, "The hit rate on adds should be close to the false positive rate");
        // }

        //
        // [Theory]
        // [InlineData(100, 0.01)]
        // [InlineData(1000, 0.01)]
        // [InlineData(100, 0.5)]
        // [InlineData(1000, 0.001)]
        // public void CanAddAndCheckBloomFilterWithScaling(int count, double errorRate)
        // {
        //     // We split the capacity between the training set and the verification set.
        //     var strings = Strings.Value.Take(count).ToList();
        //
        //     // Start out at a small capacity to exercise scaling
        //     var initialCapacity = count / 10;
        //
        //     var acceptableErrorRate = errorRate * 10;
        //     var acceptableErrorCount = (int) Math.Ceiling(count * acceptableErrorRate);
        //
        //     var parameters = new BloomFilterParameters("test", initialCapacity, errorRate);
        //     var sut = new ScalingBloomFilter(LoggingOptions, parameters);
        //
        //     var hitOnAddCount = 0;
        //
        //     foreach (var s in strings)
        //     {
        //         var isNotPresent = sut.IsNotPresent(s);
        //         var preparedAdd = sut.PrepareAdd(s);
        //
        //         preparedAdd.IsNotPresent.Should().Be(isNotPresent, "both methods of checking should have the same result");
        //
        //         if (!preparedAdd.Add())
        //         {
        //             hitOnAddCount++;
        //         }
        //     }
        //
        //     hitOnAddCount.Should().BeLessOrEqualTo(acceptableErrorCount, "The hit rate on adds should be close to the false positive rate");
        //
        //     foreach (var s in strings)
        //     {
        //         var result = sut.PrepareAdd(s);
        //         result.IsNotPresent.Should().BeFalse($"every string added should be known including '{s}'");
        //     }
        //
        //     var stringsAlt = StringsAlt.Value.Take(count).ToList();
        //
        //     var hitsOnCheck = 0;
        //     foreach (var s in stringsAlt)
        //     {
        //         var isNotPresent = sut.IsNotPresent(s);
        //         var preparedAdd = sut.PrepareAdd(s);
        //
        //         preparedAdd.IsNotPresent.Should().Be(isNotPresent, "both methods of checking should have the same result");
        //
        //         if (!isNotPresent)
        //         {
        //             hitsOnCheck++;
        //         }
        //     }
        //
        //     hitsOnCheck.Should().BeLessOrEqualTo(acceptableErrorCount, "The hit rate on strings not added to the filter should be close to the false positive rate");
        // }
        //
        // [Fact]
        // public void CanAddAndCheckBloomFilterConcurrently()
        // {
        //     // We split the capacity between the training set and the verification set.
        //     var strings = Strings.Value;
        //
        //     var count = strings.Count;
        //     var initialCapacity = count / 10;
        //     var errorRate = 0.01;
        //     var acceptableErrorRate = errorRate * 10;
        //     var acceptableErrorCount = (int) Math.Ceiling(count * acceptableErrorRate);
        //
        //     var parameters = new BloomFilterParameters("test", initialCapacity, errorRate);
        //     var sut = new ScalingBloomFilter(parameters);
        //
        //     var hitOnAddCount = 0;
        //
        //     strings.AsParallel().WithDegreeOfParallelism(8)
        //         .ForAll(s =>
        //         {
        //             if (!sut.Add(s))
        //             {
        //                 Interlocked.Increment(ref hitOnAddCount);
        //             }
        //         });
        //
        //     hitOnAddCount.Should().BeLessThan(acceptableErrorCount, "The hit rate on adds should be close to the false positive rate");
        // }
        //
        // [Theory]
        // [InlineData(10, true)]
        // [InlineData(100, false)]
        // public void StateCanBeExportedAndImported(int initialCapacity, bool scalable)
        // {
        //     const int count = 100;
        //     const double falsePositiveRate = 0.01;
        //     var strings = Strings.Value.Take(count).ToList();
        //
        //     var parameters = new BloomFilterParameters("test", initialCapacity, falsePositiveRate)
        //     {
        //         AllowScaling = scalable
        //     };
        //     var sut = new ScalingBloomFilter(parameters);
        //
        //     var hitOnAddCount = 0;
        //
        //     foreach (var s in strings)
        //     {
        //         if (!sut.Add(s))
        //         {
        //             hitOnAddCount++;
        //         }
        //     }
        //
        //     var expectedHitCount = (int) Math.Ceiling(strings.Count * falsePositiveRate) + 1;
        //     hitOnAddCount.Should().BeLessOrEqualTo(expectedHitCount);
        //
        //     foreach (var s in strings)
        //     {
        //         var key = new BloomFilterCheckRequest(s);
        //         var added = sut.Add(key);
        //         added.Should().BeFalse($"'{s} has been added before");
        //     }
        //
        //     var state = sut.ExportState();
        //     state.Parameters.Should().BeEquivalentTo(parameters);
        //     state.Id.Should().Be(parameters.Id);
        //     state.Count.Should().Be(sut.Count);
        //
        //     if (scalable)
        //     {
        //         state.Children.Should().HaveCountGreaterThan(1);
        //     }
        //     else
        //     {
        //         state.Base64BitArray.Should().NotBeNullOrEmpty();
        //     }
        //
        //     var json = JsonSerializer.Serialize(state, new JsonSerializerOptions() {WriteIndented = true});
        //     _testOutputHelper.WriteLine(json);
        //     ;
        //
        //
        //     state.Count.Should().BeCloseTo(strings.Count, 10);
        //
        //     var sut2 = new ScalingBloomFilter(LoggingOptions, state);
        //
        //     var state2 = sut.ExportState();
        //
        //     state2.Should().BeEquivalentTo(state, "state should be applied correctly");
        //
        //     foreach (var s in strings)
        //     {
        //         var key = new BloomFilterCheckRequest(s);
        //         var added = sut2.PrepareAdd(key);
        //         added.IsNotPresent.Should().BeFalse($"'{s} has been added in previous instance");
        //     }
        //
        //     var stringsAlt = StringsAlt.Value.Take(count).ToList();
        //
        //     var hitsOnCheck = 0;
        //     foreach (var s in stringsAlt)
        //     {
        //         if (sut2.IsNotPresent(s) == false)
        //         {
        //             hitsOnCheck++;
        //         }
        //     }
        //
        //     hitsOnCheck.Should().BeCloseTo(expectedHitCount, 10, "The hit rate on strings not added to the filter should be close to the false positive rate");
        // }
    }
}