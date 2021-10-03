using System.Collections.Generic;
using Bloomn.Behaviors;

namespace Bloomn
{
    public record BloomFilterParameters (string Id)
    {
        public string? Profile { get; init; }
        
        public BloomFilterDimensions Dimensions { get; init; } = new();

        public BloomFilterScaling Scaling { get; init; } = new()
        {
            MaxCapacityBehavior = MaxCapacityBehavior.Throw
        };

        public string HashAlgorithm { get; init; } = "murmur3";

        public BloomFilterParameters WithCapacityAndErrorRate(int capacity, double errorRate)
        {
            return this with
            {
                Dimensions = BloomFilterDimensions.ForCapacityAndErrorRate(capacity, errorRate)
            };
        }

        public BloomFilterParameters WithScaling(double capacityScaling = 2, double errorRateScaling = 0.8)
        {
            return this with
            {
                Scaling = new BloomFilterScaling
                {
                    MaxCapacityBehavior = MaxCapacityBehavior.Scale,
                    CapacityScaling = capacityScaling,
                    FalsePositiveProbabilityScaling = errorRateScaling
                }
            };
        }

        public void Validate()
        {
            if (string.IsNullOrEmpty(HashAlgorithm))
            {
                throw new BloomFilterException(BloomFilterExceptionCode.InvalidParameters, $"{nameof(HashAlgorithm)} must be set.");
            }

            if (Scaling == null)
            {
                throw new BloomFilterException(BloomFilterExceptionCode.InvalidParameters, $"{nameof(Scaling)} must be set;");
            }

            if (Dimensions == null)
            {
                throw new BloomFilterException(BloomFilterExceptionCode.InvalidParameters, $"{nameof(Dimensions)} must be set;");
            }

            Dimensions.Validate();
            Scaling.Validate();
        }

        public List<string> Diff(BloomFilterParameters other)
        {
            var diff = Dimensions.Diff(other.Dimensions);
            diff.AddRange(Scaling.Diff(other.Scaling));

            if (HashAlgorithm != other.HashAlgorithm)
            {
                diff.Add($"{nameof(HashAlgorithm)}: {HashAlgorithm} != {other.HashAlgorithm}");
            }

            return diff;
        }
    }
}