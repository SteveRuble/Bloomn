using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using Bloomn.Behaviors;

namespace Bloomn
{
    public sealed class ScalingBloomFilter<TKey> : IBloomFilter<TKey>, IPreparedAddTarget
    {
        private readonly BloomFilterScaling _bloomFilterScaling;
        private readonly List<FixedSizeBloomFilter<TKey>> _filters = new();
        private readonly ReaderWriterLockSlim _lock = new();
        private readonly StateMetrics _metrics;
        private readonly BloomFilterOptions<TKey> _options;
        private FixedSizeBloomFilter<TKey> _activeFilter;

        public ScalingBloomFilter(BloomFilterParameters parameters) : this(BloomFilterOptions<TKey>.DefaultOptions, new BloomFilterState {Parameters = parameters})
        {
        }

        public ScalingBloomFilter(BloomFilterState state) : this(BloomFilterOptions<TKey>.DefaultOptions, state)
        {
        }

        public ScalingBloomFilter(BloomFilterOptions<TKey> options, BloomFilterParameters parameters) : this(options, new BloomFilterState {Parameters = parameters})
        {
        }

        public ScalingBloomFilter(BloomFilterOptions<TKey> options, BloomFilterState state)
        {
            if (state.Parameters == null)
            {
                throw new ArgumentException("BloomFilterState.Parameters must not be null.");
            }

            state.Parameters.Validate();

            if (state.Parameters.Scaling.MaxCapacityBehavior != MaxCapacityBehavior.Scale)
            {
                throw new ArgumentException(nameof(state), $"Parameters.ScalingParameters.MaxCapacityBehavior was not set to {MaxCapacityBehavior.Scale}");
            }

            _options = options;
            Parameters = state.Parameters;
            _bloomFilterScaling = state.Parameters.Scaling;

            _metrics = new StateMetrics(Parameters, options.Events);

            if (state.Parameters.Id == null)
            {
                throw new ArgumentException("state.Parameters.Id must not be null", nameof(state));
            }

            if (_bloomFilterScaling.MaxCapacityBehavior == MaxCapacityBehavior.Scale)
            {
                if (state.Children?.Count > 0)
                {
                    _filters = state.Children.Select((childState, i) =>
                    {
                        if (childState.Parameters == null)
                        {
                            throw new ArgumentException($"Invalid state: child filter {i} was missing parameters");
                        }

                        return new FixedSizeBloomFilter<TKey>(options, childState);
                    }).ToList();
                    _activeFilter = _filters.Last();
                    _metrics.OnCapacityChanged(_filters.Sum(x => x.Parameters.Dimensions.Capacity));
                    _metrics.OnBitCountChanged(_filters.Sum(x => x.Parameters.Dimensions.BitCount));
                }
                else
                {
                    Scale();
                }
            }
            else if (state.BitArrays != null)
            {
                _filters = new List<FixedSizeBloomFilter<TKey>>();
                _activeFilter = new FixedSizeBloomFilter<TKey>(options, state);
            }
            else
            {
                Scale();
            }

            _metrics.OnCountChanged(state.Count);
        }

        public BloomFilterParameters Parameters { get; }

        public string Id => Parameters.Id;

        public long Count => _metrics.Count;

        public IBloomFilterDimensions Dimensions => _metrics;

        public double Saturation => _filters.Sum(x => x.Saturation) / _filters.Count;

        public BloomFilterEntry Check(BloomFilterCheckRequest<TKey> checkRequest)
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

                    using (var entry = PrepareAdd(new BloomFilterCheckRequest<TKey>(checkRequest.Key, BloomFilterCheckBehavior.PrepareForAdd)))
                    {
                        if (entry.CanAdd)
                        {
                            ApplyPreparedAddCore(entry.FilterId, entry.Indexes);
                            return BloomFilterEntry.NotPresent;
                        }
                    }

                    return BloomFilterEntry.MaybePresent;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

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
                    Children = _filters.Select(x => x.GetState()).ToList(),
                };


                return state;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        bool IPreparedAddTarget.ApplyPreparedAdd(string id, int[] indexes) => ApplyPreparedAddCore(id, indexes);

        private bool ApplyPreparedAddCore(string id, int[] indexes)
        {
            try
            {
                _lock.EnterWriteLock();

                var added = false;

                if (_activeFilter.Id == id)
                {
                    added = _activeFilter.ApplyPreparedAdd(id, indexes);
                }
                else
                {
                    // This handles the (rare) case where we rolled over to a new filter
                    // while there was an outstanding prepared add on another thread. 
                    // We will force this into the filter it belongs to because we have 
                    // no way to recompute the key for the new filter, and it won't
                    // disturb the contracts too much to exceed the capacity by a 
                    // small number of entries.
                    foreach (var filter in _filters)
                        if (filter.Id == id)
                        {
                            var previousBehavior = filter.MaxCapacityBehavior;
                            try
                            {
                                filter.MaxCapacityBehavior = MaxCapacityBehavior.Ignore;
                                added = filter.ApplyPreparedAdd(id, indexes);
                            }
                            finally
                            {
                                filter.MaxCapacityBehavior = previousBehavior;
                            }
                        }
                }

                if (added)
                {
                    _metrics.IncrementCount(1);
                }

                if (_activeFilter.Count >= _activeFilter.Dimensions.Capacity)
                {
                    Scale();
                }

                return added;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public bool IsNotPresent(BloomFilterCheckRequest<TKey> checkRequest)
        {
            try
            {
                _lock.EnterReadLock();
                for (var i = _filters.Count - 1; i >= 0; i--)
                {
                    var filter = _filters[i];

                    var isNotPresent = filter.IsNotPresent(checkRequest);
                    if (!isNotPresent)
                    {
                        _metrics.OnHit();
                        return false;
                    }
                }

                _metrics.OnMiss();
                return true;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        /// <summary>
        ///     Prepares to add the <paramref name="checkRequest" /> to the filter.
        ///     Returns a disposable struct on which you can call <see cref="PreparedAdd.Add()" />
        ///     to add the key to the filter without incurring the cost of computing the hashes
        ///     again. It's important to dispose the returned struct to limit allocations.
        /// </summary>
        /// <returns>True if the set probably contains the item</returns>
        private PreparedAdd PrepareAdd(BloomFilterCheckRequest<TKey> checkRequest)
        {
            try
            {
                _lock.EnterReadLock();
                // Check active filter because it's the largest and if we get
                // a hit anywhere it's most likely to be in the active filter
                var entry = _activeFilter.Check(checkRequest);
                if (entry.IsNotPresent)
                {
                    // Check the rest of the filters.
                    for (var i = _filters.Count - 2; i >= 0; i--)
                    {
                        var filter = _filters[i];

                        var isNotPresent = filter.Check(BloomFilterCheckRequest<TKey>.CheckOnly(checkRequest.Key)).IsNotPresent;
                        if (!isNotPresent)
                        {
                            _metrics.OnHit();
                            return PreparedAdd.AlreadyAdded;
                        }
                    }
                }

                if (entry.IsNotPresent)
                {
                    _metrics.OnMiss();
                }
                else
                {
                    _metrics.OnHit();
                }

                if (entry.PreparedAdd.CanAdd)
                {
                    return new PreparedAdd(entry.PreparedAdd.FilterId, entry.PreparedAdd.Indexes, this);
                }

                return PreparedAdd.AlreadyAdded;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }


        /// <summary>
        ///     Reports that a hit was a false positive, to allow unified metrics reporting.
        /// </summary>
        public void ReportFalsePositive()
        {
            _metrics.OnFalsePositive();
        }

        [MemberNotNull(nameof(_activeFilter))]
        private void Scale()
        {
            if (_activeFilter == null)
            {
                var bloomFilterDimensions = Parameters.Dimensions;
                if (Parameters.Scaling.MaxCapacityBehavior == MaxCapacityBehavior.Scale)
                {
                    // We need to create the filter with a lower error rate so that the compounded 
                    // error rate for all filters will stay below the requested rate.
                    var rescaledErrorRate = Parameters.Dimensions.FalsePositiveProbability / (1 / (1 - Parameters.Scaling.FalsePositiveProbabilityScaling));
                    bloomFilterDimensions = new BloomFilterDimensionsBuilder
                    {
                        Capacity = bloomFilterDimensions.Capacity,
                        FalsePositiveProbability = rescaledErrorRate
                    }.Build();
                }

                var nextParameters = Parameters with
                {
                    Id = $"{Parameters.Id}[{_filters.Count}]",
                    Dimensions = bloomFilterDimensions
                };
                _activeFilter = new FixedSizeBloomFilter<TKey>(_options, new BloomFilterState
                {
                    Parameters = nextParameters
                });
                _filters.Add(_activeFilter);
            }
            else
            {
                var nextBitCount = (int) Math.Round(_activeFilter.Parameters.Dimensions.BitCount * _bloomFilterScaling.CapacityScaling);
                var nextErrorRate = _activeFilter.Parameters.Dimensions.FalsePositiveProbability * _bloomFilterScaling.FalsePositiveProbabilityScaling;
                var nextHashCount = (int) Math.Ceiling(_activeFilter.Parameters.Dimensions.HashCount + _filters.Count * Math.Log2(Math.Pow(_activeFilter.Parameters.Scaling.FalsePositiveProbabilityScaling, -1)));

                var nextDimensions = new BloomFilterDimensionsBuilder
                {
                    BitCount = nextBitCount,
                    FalsePositiveProbability = nextErrorRate,
                    HashCount = nextHashCount
                }.Build();

                var nextParameters = Parameters with
                {
                    Id = $"{Parameters.Id}[{_filters.Count}]",
                    Dimensions = nextDimensions
                };
                _activeFilter = new FixedSizeBloomFilter<TKey>(_options, new BloomFilterState
                {
                    Parameters = nextParameters
                });
                _filters.Add(_activeFilter);
            }

            _metrics.OnCapacityChanged(_filters.Sum(x => x.Parameters.Dimensions.Capacity));
            _metrics.OnBitCountChanged(_filters.Sum(x => x.Parameters.Dimensions.BitCount));
            _metrics.OnScaled(_activeFilter.Parameters);
        }


        public void Release(string id, int[] indexes)
        {
            // In the rare case that we've rolled the active filter before an outstanding 
            // prepared add was released, we'll leak an array. Not a huge deal.
            if (_activeFilter.Id == id)
            {
                _activeFilter.Release(id, indexes);
            }
        }
    }
}