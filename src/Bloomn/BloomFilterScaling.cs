using System;
using System.Collections.Generic;

namespace Bloomn
{
    public record BloomFilterScaling
    {
        public MaxCapacityBehavior MaxCapacityBehavior { get; init; } = MaxCapacityBehavior.Throw;

        public double CapacityScaling { get; init; } = 2;

        public double FalsePositiveProbabilityScaling { get; init; } = 0.8;

        public void Validate()
        {
            if (MaxCapacityBehavior == MaxCapacityBehavior.Scale)
            {
                if (CapacityScaling <= 1)
                {
                    throw new BloomFilterException(BloomFilterExceptionCode.InvalidParameters, "CapacityScaling must be greater than 1.");
                }

                if (FalsePositiveProbabilityScaling is <= 0 or >= 1)
                {
                    throw new BloomFilterException(BloomFilterExceptionCode.InvalidParameters, "ErrorRateScaling must be between 0 and 1 exclusive.");
                }
            }
        }

        public IEnumerable<string> Diff(BloomFilterScaling other)
        {
            var diff = new List<string>();
            if (MaxCapacityBehavior != other.MaxCapacityBehavior)
            {
                diff.Add($"{nameof(MaxCapacityBehavior)}: {MaxCapacityBehavior} != {other.MaxCapacityBehavior}");
            }

            if (Math.Abs(CapacityScaling - other.CapacityScaling) > double.Epsilon)
            {
                diff.Add($"{nameof(CapacityScaling)}: {CapacityScaling} != {other.CapacityScaling}");
            }

            if (Math.Abs(FalsePositiveProbabilityScaling - other.FalsePositiveProbabilityScaling) > double.Epsilon)
            {
                diff.Add($"{nameof(FalsePositiveProbabilityScaling)}: {FalsePositiveProbabilityScaling} != {other.FalsePositiveProbabilityScaling}");
            }

            return diff;
        }
    }
}