using System.Threading.Tasks;

namespace Bloomn
{
    public interface IBloomFilterManager
    {
        Task<ScalingBloomFilter> GetOrCreateBloomFilter(BloomFilterParameters parameters);
        Task SaveBloomFilter(ScalingBloomFilter scalingBloomFilter);
        Task DeleteBloomFilter(string key);
    }
}