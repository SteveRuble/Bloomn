using FluentAssertions;
using Xunit;

namespace Bloomn.Tests
{
    public class BloomFilterParametersTests
    {
        [Fact]
        public void CanValidateParameters()
        {
            this.Invoking(_ => new BloomFilterParameters("x").Validate()).Should().NotThrow();

            this.Invoking(_ => new BloomFilterParameters("x")
            {
                HashAlgorithm = null!
            }.Validate()).Should().Throw<BloomFilterException>().Which.Code.Should().Be(BloomFilterExceptionCode.InvalidParameters);

            this.Invoking(_ => new BloomFilterParameters("x")
            {
                Dimensions = null!
            }.Validate()).Should().Throw<BloomFilterException>().Which.Code.Should().Be(BloomFilterExceptionCode.InvalidParameters);

            this.Invoking(_ => new BloomFilterParameters("x")
            {
                Scaling = null!
            }.Validate()).Should().Throw<BloomFilterException>().Which.Code.Should().Be(BloomFilterExceptionCode.InvalidParameters);
        }

        [Fact]
        public void CanDetectChangesInParameters()
        {
            var a = new BloomFilterParameters("a")
            {
                Dimensions = new BloomFilterDimensions
                {
                    Capacity = 1,
                    BitCount = 2,
                    FalsePositiveProbability = 3,
                    HashCount = 4
                },
                Scaling = new BloomFilterScaling
                {
                    CapacityScaling = 5,
                    FalsePositiveProbabilityScaling = 6,
                    MaxCapacityBehavior = MaxCapacityBehavior.Scale
                },
                HashAlgorithm = "test"
            };

            var b = new BloomFilterParameters("b")
            {
                Dimensions = new BloomFilterDimensions
                {
                    Capacity = 7,
                    BitCount = 6,
                    FalsePositiveProbability = 5,
                    HashCount = 4
                },
                Scaling = new BloomFilterScaling
                {
                    CapacityScaling = 3,
                    FalsePositiveProbabilityScaling = 2,
                    MaxCapacityBehavior = MaxCapacityBehavior.Throw
                },
                HashAlgorithm = "other"
            };

            var diff = a.Diff(b);

            diff.Should().HaveCount(7, "there are 7 differences between the instances");

            a.Diff(a).Should().HaveCount(0, "there are no differences");
        }
    }
}