using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Bloomn
{
    public class BloomFilterConfigurationBuilder
    {
        public IServiceCollection ServiceCollection { get; }

        public BloomFilterConfigurationBuilder(IServiceCollection serviceCollection)
        {
            ServiceCollection = serviceCollection;
        }

        public BloomFilterConfigurationBuilder WithDefaultProfile(Action<IBloomFilterOptionsBuilder> configureOptions)
        {
            ServiceCollection.Configure<BloomFilterOptions>(options =>
            {
                var builder = new BloomFilterBuilder(options);
                configureOptions(builder);
            });
            return this;
        }

        public BloomFilterConfigurationBuilder WithProfile(string name, Action<IBloomFilterOptionsBuilder> configureOptions)
        {
            ServiceCollection.Configure<BloomFilterOptions>(name, options =>
            {
                var builder = new BloomFilterBuilder(options);
                configureOptions(builder);
            });

            return this;
        }
    }

    public static class DependencyInjectionExtensions
    {
        public static IServiceCollection AddBloomFilters(this IServiceCollection serviceCollection, Action<BloomFilterConfigurationBuilder>? configure = null)
        {
            serviceCollection.AddOptions();
            if (configure != null)
            {
                var builder = new BloomFilterConfigurationBuilder(serviceCollection);
                configure(builder);
            }
            else
            {
                serviceCollection.AddOptions<BloomFilterOptions>();
            }

            serviceCollection.TryAddTransient<IBloomFilterBuilder, BloomFilterBuilder>();

            return serviceCollection;
        }
    }
}