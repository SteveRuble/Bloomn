using System;
using System.Collections.Generic;

namespace Bloomn
{
    public interface IBloomFilterDimensions
    {
        double FalsePositiveProbability { get; }
        int Capacity { get; }
        int BitCount { get; }
        int HashCount { get; }
    }

    public record BloomFilterDimensions : IBloomFilterDimensions
    {
        public BloomFilterDimensions(double falsePositiveProbability = 0.01, int capacity = 10000, int bitCount = 95851, int hashCount = 7)
        {
            if (hashCount < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(falsePositiveProbability), falsePositiveProbability, "Parameters resulted in a hash count of 1, which is pointless.");
            }

            FalsePositiveProbability = falsePositiveProbability;
            Capacity = capacity;
            BitCount = bitCount;
            HashCount = hashCount;
        }

        public double FalsePositiveProbability { get; init; }
        public int Capacity { get; init; }
        public int BitCount { get; init; }
        public int HashCount { get; init; }

        public static BloomFilterDimensions ForCapacityAndErrorRate(int capacity, double errorRate)
        {
            return new BloomFilterDimensionsBuilder
            {
                Capacity = capacity,
                FalsePositiveProbability = errorRate
            }.Build();
        }

        public void Validate()
        {
            if (Capacity <= 0)
            {
                throw new BloomFilterException(BloomFilterExceptionCode.InvalidParameters, "Capacity must be greater than 0.");
            }

            if (FalsePositiveProbability is <= 0 or >= 1)
            {
                throw new BloomFilterException(BloomFilterExceptionCode.InvalidParameters, "ErrorRate must be between 0 and 1 exclusive.");
            }

            if (BitCount <= 0)
            {
                throw new BloomFilterException(BloomFilterExceptionCode.InvalidParameters, "BitCount must be greater than 0.");
            }

            if (HashCount is <= 2 or > 100)
            {
                throw new BloomFilterException(BloomFilterExceptionCode.InvalidParameters, "HashCount must be greater than 1 and less than 100.");
            }
        }

        public List<string> Diff(BloomFilterDimensions other)
        {
            var diff = new List<string>();
            if (BitCount != other.BitCount)
            {
                diff.Add($"{nameof(BitCount)}: {BitCount} != {other.BitCount}");
            }

            if (Capacity != other.Capacity)
            {
                diff.Add($"{nameof(Capacity)}: {Capacity} != {other.Capacity}");
            }

            if (HashCount != other.HashCount)
            {
                diff.Add($"{nameof(HashCount)}: {HashCount} != {other.HashCount}");
            }

            if (Math.Abs(FalsePositiveProbability - other.FalsePositiveProbability) > double.Epsilon)
            {
                diff.Add($"{nameof(FalsePositiveProbability)}: {FalsePositiveProbability} != {other.FalsePositiveProbability}");
            }

            return diff;
        }

        /// <summary>
        ///     These are the equations which relate the bloom filter parameters.
        ///     n => max items before invariants are broken
        ///     m => number of bits
        ///     k => number of hashes
        ///     p => false positive rate
        /// </summary>
        internal static class Equations
        {
            // ReSharper disable InconsistentNaming

            // n = ceil(m / (-k / log(1 - exp(log(p) / k))))
            // p = pow(1 - exp(-k / (m / n)), k)
            // m = ceil((n * log(p)) / log(1 / pow(2, log(2))));
            // k = round((m / n) * log(2));


            public static int n(int m, int k, double p)
            {
                return (int) Math.Ceiling(m / (-k / Math.Log(1 - Math.Exp(Math.Log(p) / k))));
            }

            public static double p(int m, int n, int k)
            {
                return Math.Pow(1 - Math.Exp(-k / (m / (double) n)), k);
            }

            public static int m(int n, double p)
            {
                return (int) Math.Ceiling(n * Math.Log(p) / Math.Log(1 / Math.Pow(2, Math.Log(2))));
            }

            public static int k(int m, int n)
            {
                return (int) Math.Round(m / (double) n * Math.Log(2));
            }

            public static int k(double p)
            {
                return (int) Math.Round(-Math.Log2(p));
            }

            // ReSharper restore InconsistentNaming
        }
    }
}