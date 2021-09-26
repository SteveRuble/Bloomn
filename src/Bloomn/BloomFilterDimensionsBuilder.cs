using System;
using System.Diagnostics.CodeAnalysis;

namespace Bloomn
{
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
            Capacity.HasValue && FalsePositiveProbability.HasValue
            || FalsePositiveProbability.HasValue && BitCount.HasValue
            || Capacity.HasValue && FalsePositiveProbability.HasValue
            || Capacity.HasValue && BitCount.HasValue
            || FalsePositiveProbability.HasValue && BitCount.HasValue;

        public BloomFilterDimensions Build()
        {
            // Create a copy to mutate during building
            return new BloomFilterDimensionsBuilder()
            {
                FalsePositiveProbability = this.FalsePositiveProbability,
                Capacity = this.Capacity,
                BitCount = this.BitCount,
                HashCount = this.HashCount
            }.ReallyBuild();
        }
        
        private BloomFilterDimensions ReallyBuild()
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