#pragma warning disable 8618
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Bloomn.Tests
{
    
    public class BloomFilterBuilderTests
    {
        public BloomFilterOptions DefaultOptions { get; set; }
        public Mock<IOptionsMonitor<BloomFilterOptions>> OptionsMonitor { get; set; }
        
        public BloomFilterBuilderTests()
        {
            DefaultOptions = new BloomFilterOptions();

            OptionsMonitor = new Mock<IOptionsMonitor<BloomFilterOptions>>();
            OptionsMonitor.SetupGet(x => x.CurrentValue).Returns(DefaultOptions);
        }

        [Fact]
        public void BuilderCanCreateDefaultInstance()
        {
            var sut = new ServiceCollection()
                .AddBloomFilters()
                .BuildServiceProvider()
                .GetRequiredService<IBloomFilterBuilder>();
            
            var actual = sut.Build();
            actual.Dimensions.Should().BeEquivalentTo(new BloomFilterDimensions());

            var state = actual.GetState();
            state.Parameters.ShouldNotBeNull();
            state.Parameters.HashAlgorithm.Should().Be(new Murmur3HasherFactory().Algorithm);

        }
    }
    
}