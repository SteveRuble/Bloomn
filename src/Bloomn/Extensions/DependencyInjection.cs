using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Bloomn.Extensions
{
    public class BloomFilterConfigurationBuilder<TKey>
    {
        public BloomFilterConfigurationBuilder(IServiceCollection serviceCollection)
        {
            ServiceCollection = serviceCollection;
        }

        private IServiceCollection ServiceCollection { get; }

        public BloomFilterConfigurationBuilder<TKey> WithDefaultProfile(Action<IBloomFilterOptionsBuilder<TKey>> configureOptions)
        {
            return WithDefaultProfile(null, configureOptions);
        }

        public BloomFilterConfigurationBuilder<TKey> WithDefaultProfile(IConfiguration configurationSection)
        {
            return WithDefaultProfile(configurationSection, null);
        }

        public BloomFilterConfigurationBuilder<TKey> WithDefaultProfile(IConfiguration? configurationSection, Action<IBloomFilterOptionsBuilder<TKey>>? configureOptions)
        {
            return WithProfile(Options.DefaultName, configurationSection, configureOptions);
        }

        public BloomFilterConfigurationBuilder<TKey> WithProfile(string name, Action<IBloomFilterOptionsBuilder<TKey>> configureOptions)
        {
            return WithProfile(name, null, configureOptions);
        }

        public BloomFilterConfigurationBuilder<TKey> WithProfile(string name, IConfiguration configurationSection)
        {
            return WithProfile(name, configurationSection, null);
        }

        public BloomFilterConfigurationBuilder<TKey> WithProfile(string name, IConfiguration? configurationSection, Action<IBloomFilterOptionsBuilder<TKey>>? configureOptions)
        {
            if (configurationSection != null)
            {
                ServiceCollection.Configure<BloomFilterOptions<TKey>>(name, configurationSection);
            }

            if (configureOptions != null)
            {
                ServiceCollection.Configure<BloomFilterOptions<TKey>>(name, options =>
                {
                    var builder = new BloomFilterBuilder<TKey>(options, false);
                    configureOptions(builder);
                    if (name != Options.DefaultName)
                    {
                        options.Profile = name;
                    }
                });
            }

            return this;
        }
    }

    public static class DependencyInjectionExtensions
    {
        public static IServiceCollection AddBloomFilters<TKey>(this IServiceCollection serviceCollection, Action<BloomFilterConfigurationBuilder<TKey>>? configure = null)
        {
            serviceCollection.AddOptions();

            if (configure != null)
            {
                var builder = new BloomFilterConfigurationBuilder<TKey>(serviceCollection);
                configure(builder);
            }

            serviceCollection.TryAddTransient<IBloomFilterBuilder<TKey>, BloomFilterBuilder<TKey>>();
            serviceCollection.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<BloomFilterOptions<TKey>>, OptionsValidator<TKey>>());
            return serviceCollection;
        }
    }
}