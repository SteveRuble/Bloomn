using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Bloomn.Tests
{
    public class ClassicBloomFilterTests : BloomFilterTestsBase
    {
        public ClassicBloomFilterTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
        }

        public override IBloomFilter Create(BloomFilterOptions options, BloomFilterParameters parameters)
        {
            return new ClassicBloomFilter(options, new BloomFilterState()
            {
                Parameters = parameters
            });
        }
        
         
        [Fact]
        public void CanExportAndImportState()
        {
            var parameters = new BloomFilterParameters("test")
                .WithCapacityAndErrorRate(10000, 0.1);

            var first = new ClassicBloomFilter(new BloomFilterOptions(), new BloomFilterState()
            {
                Parameters = parameters
            });
            
            // Populate with data
            ChartFalsePositiveRates(parameters, () => first, RandomStrings, 1000, 100, 100);

            var firstState = first.GetState();

            var serializedFirstState = firstState.Serialize();

            var secondState = BloomFilterState.Deserialize(serializedFirstState);

            var second = new ClassicBloomFilter(new BloomFilterOptions(), secondState);
            
            second.Parameters.Should().BeEquivalentTo(first.Parameters);

            second.Count.Should().Be(first.Count);

            var fpr = GetFalsePositiveRate(second, 10000);

            fpr.Should().BeGreaterThan(0, "there should be some false positives");
            fpr.Should().BeLessOrEqualTo(parameters.Dimensions.ErrorRate, "the filter should behave correctly");
        }    
    }
}