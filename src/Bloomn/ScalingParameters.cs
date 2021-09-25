using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Bloomn
{
    public record ScalingParameters
    {
        public MaxCapacityBehavior MaxCapacityBehavior { get; init; } = MaxCapacityBehavior.Throw;

        public double CapacityScaling { get; init; } = 2;

        public double ErrorRateScaling { get; init; } = 0.8;

        public void Validate()
        {
            if (MaxCapacityBehavior == MaxCapacityBehavior.Scale)
            {
                if (CapacityScaling <= 1)
                {
                    throw new ValidationException("CapacityScaling must be greater than 1.");
                }

                if (ErrorRateScaling is <= 0 or >= 1)
                {
                    throw new ValidationException("ErrorRateScaling must be between 0 and 1 exclusive.");
                }
            }
        }

        public IEnumerable<string> ValidateMigration(ScalingParameters other)
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

            if (Math.Abs(ErrorRateScaling - other.ErrorRateScaling) > double.Epsilon)
            {
                diff.Add($"{nameof(ErrorRateScaling)}: {ErrorRateScaling} != {other.ErrorRateScaling}");
            }

            return diff;
        }
    }
}