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
    }
}