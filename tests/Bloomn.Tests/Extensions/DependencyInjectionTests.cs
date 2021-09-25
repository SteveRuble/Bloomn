using System.Collections.Generic;
using Bloomn.Extensions;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Bloomn.Tests.Extensions
{
    public class DependencyInjectionTests
    {
        [Fact]
        public void CanResolveDefaultInstance()
        {
            var svc = new ServiceCollection()
                .AddBloomFilters<string>()
                .BuildServiceProvider();

            var builder = svc.GetRequiredService<IBloomFilterBuilder<string>>();

            var instance = builder.Build();

            instance.Dimensions.Should().BeEquivalentTo(new BloomFilterDimensions());
            instance.Parameters.Scaling.Should().BeEquivalentTo(new BloomFilterScaling());
        }

        [Fact]
        public void CanConfigureAndResolveDefaultBuilder()
        {
            var svc = new ServiceCollection()
                .AddBloomFilters<string>(x => x.WithDefaultProfile(b => b.WithCapacityAndErrorRate(1234, 0.0123)))
                .BuildServiceProvider();

            var builder = svc.GetRequiredService<IBloomFilterBuilder<string>>();

            var instance = builder.Build();

            instance.Dimensions.Capacity.Should().Be(1234);
            instance.Dimensions.FalsePositiveProbability.Should().Be(0.0123);
        }


        [Fact]
        public void CanConfigureAndResolveDefaultBuilderUsingConfiguration()
        {
            var configBuilder = new ConfigurationBuilder();
            configBuilder.Add(new MemoryConfigurationSource
            {
                InitialData = new Dictionary<string, string>
                {
                    ["Dimensions:Capacity"] = "1234",
                    ["Dimensions:FalsePositiveProbability"] = "0.0123",
                    ["Scaling:MaxCapacityBehavior"] = "Scale",
                    ["Scaling:CapacityScaling"] = "3"
                }
            });

            var config = configBuilder.Build();

            var svc = new ServiceCollection()
                .AddBloomFilters<string>(x => x.WithDefaultProfile(config))
                .BuildServiceProvider();

            var builder = svc.GetRequiredService<IBloomFilterBuilder<string>>();

            var instance = builder.Build();

            instance.Dimensions.Capacity.Should().Be(1234);
            instance.Dimensions.FalsePositiveProbability.Should().Be(0.0123);
            instance.Dimensions.BitCount.Should().Be(15430, "it should have been computed from the provided values");
            instance.Dimensions.HashCount.Should().Be(6, "it should have been computed from the provided values");
            instance.Parameters.Scaling.MaxCapacityBehavior.Should().Be(MaxCapacityBehavior.Scale);
            instance.Parameters.Scaling.CapacityScaling.Should().Be(3);
        }

        [Fact]
        public void WhenProvidedConfigurationIsInvalidThenAnErrorIsThrown()
        {
            var configBuilder = new ConfigurationBuilder();
            configBuilder.Add(new MemoryConfigurationSource
            {
                InitialData = new Dictionary<string, string>
                {
                    ["Dimensions:Capacity"] = "1234",
                    ["Scaling:MaxCapacityBehavior"] = "Scale",
                    ["Scaling:CapacityScaling"] = "3"
                }
            });

            var config = configBuilder.Build();

            var svc = new ServiceCollection()
                .AddBloomFilters<string>(x => x.WithDefaultProfile(config))
                .BuildServiceProvider();

            svc.Invoking(s => s.GetRequiredService<IBloomFilterBuilder<string>>())
                .Should().Throw<OptionsValidationException>();
        }

        [Fact]
        public void CanConfigureAndResolveDefaultBuilderUsingCustomProfile()
        {
            var svc = new ServiceCollection()
                .AddBloomFilters<string>(x => x.WithDefaultProfile(b => b.WithCapacityAndErrorRate(1234, 0.0123))
                    .WithProfile("custom", b => b.WithCapacityAndErrorRate(4321, 0.0321)))
                .BuildServiceProvider();

            var builder = svc.GetRequiredService<IBloomFilterBuilder<string>>();

            var instance = builder.WithProfile("custom").Build();

            instance.Dimensions.Capacity.Should().Be(4321);
            instance.Dimensions.FalsePositiveProbability.Should().Be(0.0321);
        }
    }
}