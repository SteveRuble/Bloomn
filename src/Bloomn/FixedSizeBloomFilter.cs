using System;
using System.Buffers;
using System.Collections;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using Bloomn.Behaviors;

namespace Bloomn
{
    public sealed class FixedSizeBloomFilter<T> : IBloomFilter<T>, IPreparedAddTarget
    {
        /// <summary>
        ///     Seed used for second hash.
        /// </summary>
        private const int Hash2Seed = 1234567;

        private readonly int _actualBitCount;
        private readonly BitArray[] _bitArrays;
        private readonly int _bitsPerSlice;
        private readonly int _hashCount;
        private readonly Func<T, uint> _hasher1;
        private readonly Func<T, uint> _hasher2;
        private readonly ArrayPool<int> _indexPool;

        private readonly ReaderWriterLockSlim _lock = new();
        private readonly StateMetrics _metrics;


        internal MaxCapacityBehavior MaxCapacityBehavior;
        private readonly BloomFilterOptions<T> _options;

        public FixedSizeBloomFilter(BloomFilterOptions<T> options, BloomFilterState state)
        {
            if (state.Parameters == null)
            {
                throw new ArgumentException("BloomFilterState.Parameters must not be null.");
            }

            state.Parameters.Validate();

            Parameters = state.Parameters;

            _options = options;

            var hasherFactory = options.GetHasherFactory();

            _metrics = new StateMetrics(Parameters, options.Events);

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
            _hashCount = Parameters.Dimensions.HashCount;
            _actualBitCount = _bitsPerSlice * _hashCount;
            _indexPool = ArrayPool<int>.Create(_hashCount, 10);

            _hasher1 = hasherFactory.CreateHasher(0, _bitsPerSlice);
            _hasher2 = hasherFactory.CreateHasher(Hash2Seed, _bitsPerSlice);
        }


        public double Saturation => _metrics.SetBitCount / (double) _actualBitCount;

        public long Count => _metrics.Count;
        public BloomFilterParameters Parameters { get; }

        public BloomFilterEntry Check(BloomFilterCheckRequest<T> checkRequest)
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

        public string Id => Parameters.Id;
        
        public IBloomFilterDimensions Dimensions => Parameters.Dimensions;

        public BloomFilterState GetState()
        {
            _lock.EnterReadLock();
            try
            {
                var state = new BloomFilterState
                {
                    Profile = _options.Profile,
                    Parameters = Parameters,
                    Count = Count,
                    BitArrays = _bitArrays.Select(x =>
                    {
                        var bytes = new byte[_bitsPerSlice];
                        x.CopyTo(bytes, 0);
                        return bytes;
                    }).ToList()
                };
                return state;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        internal static int ComputeBitsPerSlice(int bitCount, int hashCount)
        {
            var n = bitCount / hashCount;

            // Hash distribution is best when modded by a prime number
            return MathHelpers.GetNextPrimeNumber(n);
        }

        public bool IsNotPresent(BloomFilterCheckRequest<T> checkRequest)
        {
            _lock.EnterReadLock();
            try
            {
                var maybePresent = true;
                var key = checkRequest.Key;

                var hash1 = _hasher1(key);
                var index = AdaptHash(hash1);
                maybePresent = GetBit(0, index);
                if (!maybePresent)
                {
                    return true;
                }

                var hash2 = _hasher2(key);
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

        private PreparedAdd PrepareAdd(BloomFilterCheckRequest<T> checkRequest)
        {
            _lock.EnterReadLock();
            try
            {
                var indexes = _indexPool.Rent(_hashCount);

                var maybePresent = true;
                var key = checkRequest.Key;

                var hash1 = (int) _hasher1(key);
                var index = hash1;
                maybePresent &= GetBit(0, index);
                indexes[0] = index;

                var hash2 = (int) _hasher2(key);
                index = hash2;
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

                return new PreparedAdd(Id, indexes, this);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        private bool TryAdd(T key)
        {
            _lock.EnterWriteLock();
            try
            {
                ValidateCapacity();
                var wasPresent = true;

                var hash1 = _hasher1(key);
                var index = AdaptHash(hash1);
                wasPresent &= SetBitAndReturnPreviousState(0, index);

                var hash2 = _hasher2(key);
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

        public bool ApplyPreparedAdd(string id, int[] indexes)
        {
            _lock.EnterWriteLock();
            try
            {
                ValidateCapacity();
                var madeChange = false;
                for (var i = 0; i < _hashCount; i++)
                {
                    var index = indexes[i];
                    madeChange |= !SetBitAndReturnPreviousState(i, index);
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

        public void Release(string id, int[] indexes)
        {
            _indexPool.Return(indexes);
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
                    "Adding more entries would increase the false positive rate above the configured value. " +
                    "Perhaps you should enable scaling.");
            }
        }
    }
}