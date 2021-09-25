using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Bloomn.Extensions
{
    public class BloomFilterConfigurationBuilder<TKey>
    {
        private IServiceCollection ServiceCollection { get; }

        public BloomFilterConfigurationBuilder(IServiceCollection serviceCollection)
        {
            ServiceCollection = serviceCollection;
        }

        public BloomFilterConfigurationBuilder<TKey> WithDefaultProfile(Action<IBloomFilterOptionsBuilder<TKey>> configureOptions) 
            => WithDefaultProfile(null, configureOptions);

        public BloomFilterConfigurationBuilder<TKey> WithDefaultProfile(IConfiguration configurationSection) 
            => WithDefaultProfile(configurationSection, null);

        public BloomFilterConfigurationBuilder<TKey> WithDefaultProfile(IConfiguration? configurationSection, Action<IBloomFilterOptionsBuilder<TKey>>? configureOptions) 
            => WithProfile(Options.DefaultName, configurationSection, configureOptions);

        public BloomFilterConfigurationBuilder<TKey> WithProfile(string name, Action<IBloomFilterOptionsBuilder<TKey>> configureOptions)
            => WithProfile(name, null, configureOptions);

        public BloomFilterConfigurationBuilder<TKey> WithProfile(string name, IConfiguration configurationSection)
            => WithProfile(name, configurationSection, null);

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
                    var builder = new BloomFilterBuilder<TKey>(options);
                    configureOptions(builder);
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