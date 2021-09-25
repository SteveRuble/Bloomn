namespace Bloomn
{
   
    public interface IBloomFilter
    {
        string Id { get; }
        long Count { get; }
        
        BloomFilterParameters Parameters { get; }
        
        IBloomFilterDimensions Dimensions { get; }
        
        double Saturation { get; }

        /// <summary>
        /// Checks whether a key is not present in the filter.
        /// Returned value can be used to add the key
        /// </summary>
        /// <param name="checkRequest"></param>
        /// <returns></returns>
        BloomFilterEntry Check(BloomFilterCheckRequest checkRequest);

        BloomFilterState GetState();
    }

    public interface IPreparedAddTarget
    {
        bool Add(int[] indexes);
    }

    public static class BloomFilterExtensions
    {
        public static BloomFilterEntry CheckAndPrepareAdd(this IBloomFilter bloomFilter, BloomFilterKey key)
            => bloomFilter.Check(BloomFilterCheckRequest.PrepareForAdd(key));

        public static bool Add(this IBloomFilter bloomFilter, BloomFilterKey key)
        {
            var check = bloomFilter.Check(BloomFilterCheckRequest.AddImmediately(key));
            return check.IsNotPresent;
        }
        
        public static bool IsNotPresent(this IBloomFilter bloomFilter, BloomFilterKey key)
        {
            var check = bloomFilter.Check(BloomFilterCheckRequest.CheckOnly(key));
            return check.IsNotPresent;
        }
    }
}