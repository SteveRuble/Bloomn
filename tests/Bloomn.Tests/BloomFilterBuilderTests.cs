#pragma warning disable 8618
using Bloomn.Extensions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Bloomn.Tests
{
    public class BloomFilterBuilderTests
    {
        public BloomFilterBuilderTests()
        {
            DefaultOptions = new BloomFilterOptions<string>();

            OptionsMonitor = new Mock<IOptionsMonitor<BloomFilterOptions<string>>>();
            OptionsMonitor.SetupGet(x => x.CurrentValue).Returns(DefaultOptions);
        }

        public BloomFilterOptions<string> DefaultOptions { get; set; }
        public Mock<IOptionsMonitor<BloomFilterOptions<string>>> OptionsMonitor { get; set; }

        [Fact]
        public void BuilderCanCreateDefaultInstance()
        {
            var sut = new ServiceCollection()
                .AddBloomFilters<string>()
                .BuildServiceProvider()
                .GetRequiredService<IBloomFilterBuilder<string>>();

            var actual = sut.Build();
            actual.Dimensions.Should().BeEquivalentTo(new BloomFilterDimensions());

            var state = actual.GetState();
            state.Parameters.ShouldNotBeNull();
            state.Parameters.HashAlgorithm.Should().Be(new DefaultHasherFactoryV1().Algorithm);
        }
    }
}