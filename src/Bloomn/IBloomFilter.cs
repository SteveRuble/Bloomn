namespace Bloomn
{
    public interface IBloomFilter<T>
    {
        string Id { get; }
        long Count { get; }

        BloomFilterParameters Parameters { get; }

        IBloomFilterDimensions Dimensions { get; }

        double Saturation { get; }

        /// <summary>
        ///     Checks whether a key is not present in the filter.
        ///     Returned value can be used to add the key
        /// </summary>
        /// <param name="checkRequest"></param>
        /// <returns></returns>
        BloomFilterEntry Check(BloomFilterCheckRequest<T> checkRequest);

        BloomFilterState GetState();
    }

    public interface IPreparedAddTarget
    {
        bool ApplyPreparedAdd(string id, int[] indexes);
        void Release(string id, int[] indexes);
        
    }

    public static class BloomFilterExtensions
    {
        public static BloomFilterEntry CheckAndPrepareAdd<T>(this IBloomFilter<T> bloomFilter, T key)
        {
            return bloomFilter.Check(BloomFilterCheckRequest<T>.PrepareForAdd(key));
        }

        public static bool Add<T>(this IBloomFilter<T> bloomFilter, T key)
        {
            var check = bloomFilter.Check(BloomFilterCheckRequest<T>.AddImmediately(key));
            return check.IsNotPresent;
        }

        public static bool IsNotPresent<T>(this IBloomFilter<T> bloomFilter, T key)
        {
            var check = bloomFilter.Check(BloomFilterCheckRequest<T>.CheckOnly(key));
            return check.IsNotPresent;
        }
    }
}