using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

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
        /// <summary>
        /// These are the equations which relate the bloom filter parameters.
        /// n => max items before invariants are broken
        /// m => number of bits
        /// k => number of hashes
        /// p => false positive rate
        /// </summary>
        internal static class Equations
        {
            // ReSharper disable InconsistentNaming

            // n = ceil(m / (-k / log(1 - exp(log(p) / k))))
            // p = pow(1 - exp(-k / (m / n)), k)
            // m = ceil((n * log(p)) / log(1 / pow(2, log(2))));
            // k = round((m / n) * log(2));


            public static int n(int m, int k, double p) => (int) Math.Ceiling(m / (-k / Math.Log(1 - Math.Exp(Math.Log(p) / k))));

            public static double p(int m, int n, int k) => Math.Pow(1 - Math.Exp(-k / (m / (double) n)), k);

            public static int m(int n, double p) => (int) Math.Ceiling((n * Math.Log(p)) / Math.Log(1 / Math.Pow(2, Math.Log(2))));

            public static int k(int m, int n) => (int) Math.Round((m / (double) n) * Math.Log(2));
            
            public static int k(double p) => (int)Math.Round(-Math.Log2(p));

            // ReSharper restore InconsistentNaming
        }

        public double FalsePositiveProbability { get; init; }
        public int Capacity { get; init; }
        public int BitCount { get; init; }
        public int HashCount { get; init; }
        
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

        public static BloomFilterDimensions ForCapacityAndErrorRate(int capacity, double errorRate)
        {
            return new BloomFilterDimensionsBuilder()
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
    }

    public class BloomFilterDimensionsBuilder
    {
        public double? FalsePositiveProbability { get; set; }
        public int? Capacity { get; set; }
        public int? BitCount { get; set; }
        public int? HashCount { get; set; }

        [MemberNotNullWhen(true, nameof(FalsePositiveProbability))]
        [MemberNotNullWhen(true, nameof(Capacity))]
        [MemberNotNullWhen(true, nameof(BitCount))]
        [MemberNotNullWhen(true, nameof(HashCount))]
        public bool FullySpecified => FalsePositiveProbability != null && Capacity != null && BitCount != null && HashCount != null;

        public bool Buildable =>
            (Capacity.HasValue && FalsePositiveProbability.HasValue)
            || (FalsePositiveProbability.HasValue && BitCount.HasValue)
            || (Capacity.HasValue && FalsePositiveProbability.HasValue)
            || (Capacity.HasValue && BitCount.HasValue)
            || (FalsePositiveProbability.HasValue && BitCount.HasValue);

        public BloomFilterDimensions Build()
        {
            if (!Buildable)
            {
                throw new InvalidOperationException("Not enough parameters are set.");
            }

            var makingProgress = true;
            while (!FullySpecified && makingProgress)
            {
                makingProgress = false;
                if (!HashCount.HasValue && Capacity.HasValue && BitCount.HasValue)
                {
                    HashCount = BloomFilterDimensions.Equations.k(BitCount.Value, Capacity.Value);
                    makingProgress = true;
                    continue;
                }  
                    
                if (!HashCount.HasValue && FalsePositiveProbability.HasValue)
                {
                    HashCount = BloomFilterDimensions.Equations.k(FalsePositiveProbability.Value);
                    makingProgress = true;
                    continue;
                }

                if (!BitCount.HasValue && Capacity.HasValue && FalsePositiveProbability.HasValue)
                {
                    BitCount = BloomFilterDimensions.Equations.m(Capacity.Value, FalsePositiveProbability.Value);
                    makingProgress = true;
                    continue;
                }

                if (!Capacity.HasValue && BitCount.HasValue && HashCount.HasValue && FalsePositiveProbability.HasValue)
                {
                    Capacity = BloomFilterDimensions.Equations.n(BitCount.Value, HashCount.Value, FalsePositiveProbability.Value);
                    makingProgress = true;
                    continue;
                }

                if (!FalsePositiveProbability.HasValue && BitCount.HasValue && Capacity.HasValue && HashCount.HasValue)
                {
                    FalsePositiveProbability = BloomFilterDimensions.Equations.p(BitCount.Value, Capacity.Value, HashCount.Value);
                    makingProgress = true;
                    continue;
                }
            }

            if (!FullySpecified)
            {
                throw new InvalidOperationException($"Could not compute dimensions using provided values: {this}");
            }

            return new BloomFilterDimensions(FalsePositiveProbability.Value, Capacity.Value, BitCount.Value, HashCount.Value);
        }

        public override string ToString()
        {
            return $"{nameof(FalsePositiveProbability)}: {FalsePositiveProbability}, {nameof(Capacity)}: {Capacity}, {nameof(BitCount)}: {BitCount}, {nameof(HashCount)}: {HashCount}";
        }
    }
}