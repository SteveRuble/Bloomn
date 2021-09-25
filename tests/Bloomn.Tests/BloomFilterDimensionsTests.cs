using FluentAssertions;
using Xunit;

namespace Bloomn.Tests
{
    
    public class BloomFilterDimensionsTests
    {
        [Theory]
        [InlineData(6550, 62783, 7, 0.01)]
        [InlineData(4000, 38341, 7, 0.01)]
        [InlineData(20000000, 124704485, 4, 0.05)]
        public void CanComputeParametersFromCapacityAndErrorRate(int capacity, int bitCount, int hashCount, double errorRate)
        {
            BloomFilterDimensions.ForCapacityAndErrorRate(capacity, errorRate)
                .Should().BeEquivalentTo(new BloomFilterDimensions()
                {
                    Capacity = capacity,
                    FalsePositiveProbability = errorRate,
                    BitCount = bitCount,
                    HashCount = hashCount,
                }, o => o.ComparingByMembers<BloomFilterParameters>());   
        }   
        
        [Theory]
        [InlineData(null, 100001, 8, 0.03, 12950, 100001, 8, 0.03)]
        [InlineData(12345, 123456, 5, null, 12345, 123456, 5, 0.009429163)]
        [InlineData(12345, 123456, null, null, 12345, 123456, 7, 0.008191797)]
        public void CanComputeParameters(
            int? capacity, 
            int? bitCount,
            int? hashCount,
            double? falsePositiveRate,
            int expectedCapacity, 
            int expectedBitCount, 
            int expectedHashCount, 
            double expectedFalsePositiveRate
        
        )
        {
            new BloomFilterDimensionsBuilder()
            {
                Capacity = capacity,
                BitCount = bitCount,
                HashCount = hashCount,
                FalsePositiveProbability = falsePositiveRate
            }.Build().Should().BeEquivalentTo(new BloomFilterDimensions(
                expectedFalsePositiveRate, 
                expectedCapacity,
                expectedBitCount,
                expectedHashCount), o => o.ComparingByMembers<BloomFilterDimensions>()
                .Using<double>(t => t.Subject.Should().BeApproximately(t.Expectation, 0.0001)
                ).WhenTypeIs<double>());
            
        }

        [Fact]
        public void CanValidate()
        {
            this.Invoking(_ => new BloomFilterDimensions()
            {
                Capacity = 1000,
                BitCount = 1000,
                FalsePositiveProbability = 0.03,
                HashCount = 0
            }.Validate()).Should().Throw<BloomFilterException>().Which.Code.Should().Be(BloomFilterExceptionCode.InvalidParameters);
            
            this.Invoking(_ => new BloomFilterDimensions()
            {
                Capacity = 0,
                BitCount = 1000,
                FalsePositiveProbability = 0.03,
                HashCount = 5
            }.Validate()).Should().Throw<BloomFilterException>().Which.Code.Should().Be(BloomFilterExceptionCode.InvalidParameters);
       
            this.Invoking(_ => new BloomFilterDimensions()
            {
                Capacity = 1000,
                BitCount = 0,
                FalsePositiveProbability = 0.03,
                HashCount = 5
            }.Validate()).Should().Throw<BloomFilterException>().Which.Code.Should().Be(BloomFilterExceptionCode.InvalidParameters);
         
            this.Invoking(_ => new BloomFilterDimensions()
            {
                Capacity = 1000,
                BitCount = 123,
                FalsePositiveProbability = 0,
                HashCount = 5
            }.Validate()).Should().Throw<BloomFilterException>().Which.Code.Should().Be(BloomFilterExceptionCode.InvalidParameters);

            
        }
    }
}