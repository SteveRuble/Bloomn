using FluentAssertions;
using Xunit;

namespace Bloomn.Tests
{
    public class BloomFilterScalingTests
    {
        [Fact]
        public void ScalingValidatesCapacityRateWhenEnabled()
        {
            var sut = new BloomFilterScaling
            {
                MaxCapacityBehavior = MaxCapacityBehavior.Scale,
                CapacityScaling = 1
            };
            this.Invoking(_ => sut.Validate()).Should().Throw<BloomFilterException>().Which.Code.Should().Be(BloomFilterExceptionCode.InvalidParameters);
        }

        [Fact]
        public void ScalingValidatesFppWhenEnabled()
        {
            var sut = new BloomFilterScaling
            {
                MaxCapacityBehavior = MaxCapacityBehavior.Scale,
                FalsePositiveProbabilityScaling = 1.7
            };
            this.Invoking(_ => sut.Validate()).Should().Throw<BloomFilterException>().Which.Code.Should().Be(BloomFilterExceptionCode.InvalidParameters);
        }
    }
}