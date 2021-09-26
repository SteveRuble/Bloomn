using System;

namespace Bloomn
{
    public interface IBloomFilterBuilder<TKey>
    {
        IBloomFilterBuilder<TKey> WithOptions(BloomFilterOptions<TKey> options);
        IBloomFilterBuilder<TKey> WithOptions(Action<IBloomFilterOptionsBuilder<TKey>> configure);
        IBloomFilterBuilder<TKey> WithProfile(string profile);
        IBloomFilterBuilder<TKey> WithState(BloomFilterState state);
        IBloomFilter<TKey> Build();
    }
}