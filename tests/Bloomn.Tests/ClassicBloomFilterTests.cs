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

        public override IBloomFilter<string> Create(BloomFilterOptions<string> options, BloomFilterParameters parameters)
        {
            return new ClassicBloomFilter<string>(options, new BloomFilterState()
            {
                Parameters = parameters
            });
        }
        
         
        [Fact]
        public void CanExportAndImportState()
        {
            var parameters = new BloomFilterParameters("test")
                .WithCapacityAndErrorRate(10000, 0.1);

            var first = new ClassicBloomFilter<string>(new BloomFilterOptions<string>(), new BloomFilterState()
            {
                Parameters = parameters
            });
            
            // Populate with data
            ChartFalsePositiveRates(parameters, () => first, RandomStrings, 5000, 100, 100);

            var firstState = first.GetState();

            var serializedFirstState = firstState.Serialize();

            var secondState = BloomFilterState.Deserialize(serializedFirstState);

            var second = new ClassicBloomFilter<string>(new BloomFilterOptions<string>(), secondState);
            
            second.Parameters.Should().BeEquivalentTo(first.Parameters);

            second.Count.Should().Be(first.Count);

            var fpr = GetFalsePositiveRate(second, 10000);

            fpr.Should().BeGreaterThan(0, "there should be some false positives");
            fpr.Should().BeLessOrEqualTo(parameters.Dimensions.FalsePositiveProbability, "the filter should behave correctly");
        }    
    }
}