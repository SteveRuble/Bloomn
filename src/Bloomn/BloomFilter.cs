namespace Bloomn
{
    public static class BloomFilter
    {
        /// <summary>
        /// Create a new builder with default options.
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <returns></returns>
        public static IBloomFilterBuilder<TKey> Builder<TKey>() => new BloomFilterBuilder<TKey>();
        
        /// <summary>
        /// Create a new builder with the provided options.
        /// </summary>
        /// <param name="options"></param>
        /// <param name="cloneOptions">
        /// Optional; if true, the provided options will be cloned and any changes made by
        /// the builder will only be applied to the cloned version. Defaults to true.
        /// </param>
        public static IBloomFilterBuilder<TKey> Builder<TKey>(BloomFilterOptions<TKey> options, bool cloneOptions = true) => new BloomFilterBuilder<TKey>(new BloomFilterOptions<TKey>());
        
        /// <summary>
        /// Create a new builder directly from state.
        /// </summary>
        /// <param name="state"></param>
        /// <typeparam name="TKey"></typeparam>
        /// <returns></returns>
        public static IBloomFilterBuilder<TKey> Builder<TKey>(BloomFilterState state) => new BloomFilterBuilder<TKey>(state);
    }
}