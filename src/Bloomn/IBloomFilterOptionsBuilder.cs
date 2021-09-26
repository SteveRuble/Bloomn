namespace Bloomn
{
    public interface IBloomFilterOptionsBuilder<TKey>
    {
        IBloomFilterOptionsBuilder<TKey> WithCapacityAndFalsePositiveProbability(int capacity, double falsePositiveProbability);
        IBloomFilterOptionsBuilder<TKey> WithDimensions(BloomFilterDimensions dimensions);
        IBloomFilterOptionsBuilder<TKey> WithScaling(double capacityScaling = 2, double errorRateScaling = 0.8);
        IBloomFilterOptionsBuilder<TKey> WithHasher(IKeyHasherFactory<TKey> hasherFactory);
        IBloomFilterOptionsBuilder<TKey> WithCallbacks(BloomFilterEvents events);
        IBloomFilterOptionsBuilder<TKey> IgnoreCapacityLimits();
    }
}