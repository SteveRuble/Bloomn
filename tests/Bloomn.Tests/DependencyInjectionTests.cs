using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bloomn.Tests
{
    public class DependencyInjectionTests
    {
        [Fact]
        public void CanResolveDefaultInstance()
        {
            var svc = new ServiceCollection()
                .AddBloomFilters()
                .BuildServiceProvider();

            var builder = svc.GetRequiredService<IBloomFilterBuilder>();

            var instance = builder.Build();
            
            instance.Dimensions.Should().BeEquivalentTo(new BloomFilterDimensions());
            instance.Parameters.Scaling.Should().BeEquivalentTo(new BloomFilterScaling());
        }  
        
        [Fact]
        public void CanConfigureAndResolveDefaultBuilder()
        {
            var svc = new ServiceCollection()
                .AddBloomFilters(x => x.WithDefaultProfile(b => b.WithCapacityAndErrorRate(1234, 0.0123)))
                .BuildServiceProvider();

            var builder = svc.GetRequiredService<IBloomFilterBuilder>();

            var instance = builder.Build();

            instance.Dimensions.Capacity.Should().Be(1234);
            instance.Dimensions.ErrorRate.Should().Be(0.0123);
        }        
        
        [Fact]
        public void CanConfigureAndResolveDefaultBuilderUsingCustomProfile()
        {
            var svc = new ServiceCollection()
                .AddBloomFilters(x => x.WithDefaultProfile(b => b.WithCapacityAndErrorRate(1234, 0.0123))
                    .WithProfile("custom", b => b.WithCapacityAndErrorRate(4321, 0.0321)))
                .BuildServiceProvider();

            var builder = svc.GetRequiredService<IBloomFilterBuilder>();

            var instance = builder.WithProfile("custom").Build();

            instance.Dimensions.Capacity.Should().Be(4321);
            instance.Dimensions.ErrorRate.Should().Be(0.0321);
        }
        
    }
}