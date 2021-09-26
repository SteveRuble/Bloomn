namespace Bloomn
{
    public static class BloomFilter
    {
        public static IBloomFilterBuilder<TKey> Builder<TKey>() => new BloomFilterBuilder<TKey>(new BloomFilterOptions<TKey>());
    }
}