using System;

namespace Bloomn
{
    public interface IBloomFilterBuilder<TKey>
    {
        /// <summary>
        /// Provide the options directly, rather than using options configured at the application level.
        /// </summary>
        /// <param name="options"></param>
        /// <returns></returns>
        IBloomFilterBuilder<TKey> WithOptions(BloomFilterOptions<TKey> options);
        
        /// <summary>
        /// Customize the options provided from the application.
        /// </summary>
        /// <param name="configure"></param>
        /// <returns></returns>
        IBloomFilterBuilder<TKey> WithOptions(Action<IBloomFilterOptionsBuilder<TKey>> configure);
        
        /// <summary>
        /// Use the options from a profile configured at the application level.
        /// </summary>
        /// <param name="profile"></param>
        /// <returns></returns>
        IBloomFilterBuilder<TKey> WithProfile(string profile);
        
        /// <summary>
        /// Load the provided state into the builder.
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        IBloomFilterBuilder<TKey> WithState(BloomFilterState state);
        
        /// <summary>
        /// Build the filter.
        /// </summary>
        /// <returns></returns>
        IBloomFilter<TKey> Build();
    }
}