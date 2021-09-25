using System;
using System.Buffers;
using System.Collections;
using System.Linq;
using System.Threading;

namespace Bloomn
{
    public class ClassicBloomFilter : IBloomFilter
    {
        /// <summary>
        ///     Seed used for second hash.
        /// </summary>
        private const int Hash2Seed = 1234567;

        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();

        private readonly int _bitCount;
        private readonly int _hashCount;
        private readonly StateMetrics _metrics;
        private readonly ArrayPool<int> _indexPool;
        private readonly IKeyHasherFactory _keyHasherFactory;
        private readonly BitArray[] _bitArrays;

        private readonly int _bitsPerSlice;
        private readonly int _actualBitCount;
        
        internal MaxCapacityBehavior MaxCapacityBehavior;

        public ClassicBloomFilter(BloomFilterOptions options, BloomFilterState state)
        {
            if (state.Parameters == null)
            {
                throw new ArgumentException("BloomFilterState.Parameters must not be null.");
            }

            state.Parameters.Validate();

            Parameters = state.Parameters;

            _keyHasherFactory = options.GetHasher();

            _metrics = new StateMetrics(Parameters, options.Callbacks);

            _bitsPerSlice = ComputeBitsPerSlice(state.Parameters.Dimensions.BitCount, state.Parameters.Dimensions.HashCount);

            if (state.BitArrays != null && state.BitArrays.Count > 0)
            {
                _bitArrays = state.BitArrays.Select(x => new BitArray(x)).ToArray();
            }
            else
            {
                _bitArrays = Enumerable.Range(0, Parameters.Dimensions.HashCount).Select(_ => new BitArray(_bitsPerSlice)).ToArray();
            }

            _metrics.IncrementSetBitCount(_bitArrays.Sum(x => x.OfType<bool>().Count(x => x)));
            _metrics.IncrementCount(state.Count);
            
            MaxCapacityBehavior = Parameters.Scaling.MaxCapacityBehavior;
            _bitCount = Parameters.Dimensions.BitCount;
            _hashCount = Parameters.Dimensions.HashCount;
            _actualBitCount = _bitsPerSlice * _hashCount;
            _indexPool = ArrayPool<int>.Create(_hashCount, 10);
        }

        internal static int ComputeBitsPerSlice(int bitCount, int hashCount)
        {
            var n = bitCount / hashCount;
            // Hash distribution is best when modded by a prime number
            if (n % 2 == 0)
            {
                n++;
            }

            // The maximum prime gap at 1,346,294,310,749 is 582 so we should never hit it
            var safety = n + 582;
            int i, j;
            for (i = n; i < safety; i += 2)
            {
                var limit = Math.Sqrt(i);
                for (j = 3; j <= limit; j += 2)
                {
                    if (i % j == 0)
                        break;
                }

                if (j > limit)
                    return i;
            }

            throw new Exception($"Prime above {n} not found in a reasonable time (your filter must be unreasonably large).");
        }


        public double Saturation => _metrics.SetBitCount / (double) _actualBitCount;

        public long Count => _metrics.Count;
        public BloomFilterParameters Parameters { get; }

        public BloomFilterEntry Check(BloomFilterCheckRequest checkRequest)
        {
            switch (checkRequest.Behavior)
            {
                case BloomFilterCheckBehavior.CheckOnly:
                    return IsNotPresent(checkRequest) ? BloomFilterEntry.NotPresent : BloomFilterEntry.MaybePresent;

                case BloomFilterCheckBehavior.PrepareForAdd:
                    var preparedAdd = PrepareAdd(checkRequest);
                    if (preparedAdd.CanAdd)
                    {
                        return BloomFilterEntry.Addable(preparedAdd);
                    }

                    return BloomFilterEntry.MaybePresent;

                case BloomFilterCheckBehavior.AddImmediately:
                    var wasNotPresent = TryAdd(checkRequest.Key);
                    if (wasNotPresent)
                    {
                        return BloomFilterEntry.Addable(PreparedAdd.AlreadyAdded);
                    }

                    return BloomFilterEntry.MaybePresent;

                default:
                    throw new ArgumentOutOfRangeException(nameof(checkRequest));
            }
        }

        public bool IsNotPresent(BloomFilterCheckRequest checkRequest)
        {
            _lock.EnterReadLock();
            try
            {
                var maybePresent = true;
                var bytes = checkRequest.Key.Bytes;

                var hash1 = _keyHasherFactory.Hash(bytes, 0);
                var index = AdaptHash(hash1);
                maybePresent = GetBit(0, index);
                if (!maybePresent)
                {
                    return true;
                }

                var hash2 = _keyHasherFactory.Hash(bytes, Hash2Seed);
                index = AdaptHash(hash2);
                maybePresent = GetBit(1, index);

                if (!maybePresent)
                {
                    return true;
                }

                for (var i = 2; i < _hashCount; i++)
                {
                    var hash = (hash1 + i) * hash2;
                    index = AdaptHash(hash);
                    maybePresent = GetBit(i, index);
                    if (!maybePresent)
                    {
                        return true;
                    }
                }

                return false;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        private PreparedAdd PrepareAdd(BloomFilterCheckRequest checkRequest)
        {
            _lock.EnterReadLock();
            try
            {
                var indexes = _indexPool.Rent(_hashCount);

                var maybePresent = true;
                var bytes = checkRequest.Key.Bytes;

                var hash1 = _keyHasherFactory.Hash(bytes, 0);
                var index = AdaptHash(hash1);
                maybePresent &= GetBit(0, index);
                indexes[0] = index;

                var hash2 = _keyHasherFactory.Hash(bytes, Hash2Seed);
                index = AdaptHash(hash2);
                if (maybePresent)
                {
                    maybePresent &= GetBit(1, index);
                }

                indexes[1] = index;

                for (var i = 2; i < _hashCount; i++)
                {
                    var hash = (hash1 + i) * hash2;
                    index = AdaptHash(hash);
                    if (maybePresent)
                    {
                        maybePresent &= GetBit(i, index);
                    }

                    indexes[i] = index;
                }

                if (maybePresent)
                {
                    _indexPool.Return(indexes);
                    return PreparedAdd.AlreadyAdded;
                }

                return new PreparedAdd(Id, indexes, ApplyPreparedAdd, Release);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        private bool TryAdd(BloomFilterKey key)
        {
            _lock.EnterWriteLock();
            try
            {
                ValidateCapacity();
                var wasPresent = true;
                var bytes = key.Bytes;

                var hash1 = _keyHasherFactory.Hash(bytes, 0);
                var index = AdaptHash(hash1);
                wasPresent &= SetBitAndReturnPreviousState(0, index);

                var hash2 = _keyHasherFactory.Hash(bytes, Hash2Seed);
                index = AdaptHash(hash2);
                wasPresent &= SetBitAndReturnPreviousState(1, index);

                for (var i = 2; i < _hashCount; i++)
                {
                    var hash = (hash1 + i) * hash2;
                    index = AdaptHash(hash);
                    wasPresent &= SetBitAndReturnPreviousState(i, index);
                }

                if (!wasPresent)
                {
                    _metrics.IncrementCount(1);
                }

                return !wasPresent;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public string Id => Parameters.Id;
        public IBloomFilterDimensions Dimensions => Parameters.Dimensions;

        public bool ApplyPreparedAdd(PreparedAdd preparedAdd)
        {
            _lock.EnterWriteLock();
            try
            {
                ValidateCapacity();
                var madeChange = false;
                if (preparedAdd.Indexes != null)
                {
                    for (int i = 0; i < _hashCount; i++)
                    {
                        var index = preparedAdd.Indexes[i];
                        madeChange |= !SetBitAndReturnPreviousState(i, index);
                    }
                }

                if (madeChange)
                {
                    _metrics.IncrementCount(1);
                }

                return madeChange;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public void Release(PreparedAdd preparedAdd)
        {
            if (preparedAdd.Indexes != null)
            {
                _indexPool.Return(preparedAdd.Indexes);
            }
        }

        private bool SetBitAndReturnPreviousState(int slice, int index)
        {
            if (GetBit(slice, index))
            {
                return true;
            }

            _metrics.IncrementSetBitCount(1);
            _bitArrays[slice][index] = true;
            return false;
        }

        private bool GetBit(int slice, int index)
        {
            return _bitArrays[slice][index];
        }

        private int AdaptHash(long hash)
        {
            return (int) (Math.Abs(hash) % _bitsPerSlice);
        }

        private void ValidateCapacity()
        {
            switch (MaxCapacityBehavior)
            {
                case MaxCapacityBehavior.Ignore:
                    return;
            }

            if (_metrics.Count > _metrics.Capacity)
            {
                throw new BloomFilterException(BloomFilterExceptionCode.MaxCapacityExceeded,
                    $"Cannot add to filter because filter is at maximum capacity {Parameters.Dimensions.Capacity}. " +
                    $"Adding more entries would increase the false positive rate above the configured value. " +
                    $"Perhaps you should enable scaling.");
            }
        }

        public BloomFilterState GetState()
        {
            var state = new BloomFilterState
            {
                Parameters = Parameters,
                Count = Count,
                BitArrays = _bitArrays.Select(x =>
                {
                    var bytes = new byte[_bitsPerSlice];
                    x.CopyTo(bytes, 0);
                    return bytes;
                }).ToList(),
            };
            return state;
        }
    }
}