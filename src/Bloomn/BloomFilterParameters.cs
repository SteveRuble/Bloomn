using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Bloomn
{
    public record BloomFilterParameters (string Id)
    {
        public BloomFilterDimensions Dimensions { get; init; } = new BloomFilterDimensions();

        public ScalingParameters ScalingParameters { get; init; } = new ScalingParameters()
        {
            MaxCapacityBehavior = MaxCapacityBehavior.Throw,
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
                ScalingParameters = new ScalingParameters()
                {
                    MaxCapacityBehavior = MaxCapacityBehavior.Scale,
                    CapacityScaling = capacityScaling,
                    ErrorRateScaling = errorRateScaling
                }
            };
        }

        public void Validate()
        {
            if (string.IsNullOrEmpty(HashAlgorithm))
            {
                throw new ValidationException($"{nameof(HashAlgorithm)} must be set.");
            }

            if (ScalingParameters == null)
            {
                throw new ValidationException($"{nameof(ScalingParameters)} must be set;");
            }

            if (Dimensions == null)
            {
                throw new ValidationException($"{nameof(Dimensions)} must be set;");
            }

            Dimensions.Validate();
            ScalingParameters.Validate();
        }

        public List<string> Diff(BloomFilterParameters other)
        {
            var diff = Dimensions.Diff(other.Dimensions);
            diff.AddRange(ScalingParameters.ValidateMigration(other.ScalingParameters));

            if (HashAlgorithm != other.HashAlgorithm)
            {
                diff.Add($"{nameof(HashAlgorithm)}: {HashAlgorithm} != {other.HashAlgorithm}");
            }

            return diff;
        }
    }

    public enum MaxCapacityBehavior
    {
        /// <summary>
        /// The bloom filter will throw an exception when it hits capacity.
        /// </summary>
        Throw,

        /// <summary>
        /// The bloom filter will scale up when it reaches capacity, using the algorithm from "Scalable Bloom Filters".
        /// Enabling scaling allows you to avoid over-allocating storage when you don't know how many items you'll
        /// need to add to the filter. However, if you do know how many items you need to add you will get better performance
        /// and storage efficiency by specifying the capacity initially.
        /// <para>
        /// https://doi.org/10.1016/j.ipl.2006.10.007
        /// </para>
        /// <para>
        /// https://haslab.uminho.pt/cbm/files/dbloom.pdf
        /// </para>
        /// <para>
        /// Almeida, P. S. et al. “Scalable Bloom Filters.” Inf. Process. Lett. 101 (2007): 255-261.
        /// </para>
        /// </summary>
        Scale,

        /// <summary>
        /// The bloom filter will continue to add items even if it can no longer fulfil the requested error rate. 
        /// </summary>
        Ignore
    }
}