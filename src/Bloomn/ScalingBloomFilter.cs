using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;

namespace Bloomn
{
    public sealed class ScalingBloomFilter : IBloomFilter
    {
        private readonly List<ClassicBloomFilter> _filters = new();
        private readonly ReaderWriterLockSlim _lock = new();
        private readonly BloomFilterOptions _options;
        private readonly BloomFilterParameters _parameters;
        private readonly ScalingParameters _scalingParameters;
        private readonly StateMetrics _metrics;
        private ClassicBloomFilter _activeFilter;

        public ScalingBloomFilter(BloomFilterParameters parameters) : this(BloomFilterOptions.DefaultOptions, new BloomFilterState {Parameters = parameters})
        {
        }

        public ScalingBloomFilter(BloomFilterState state) : this(BloomFilterOptions.DefaultOptions, state)
        {
        }

        public ScalingBloomFilter(BloomFilterOptions options, BloomFilterParameters parameters) : this(options, new BloomFilterState {Parameters = parameters})
        {
        }

        public ScalingBloomFilter(BloomFilterOptions options, BloomFilterState state)
        {
            if (state.Parameters == null)
            {
                throw new ArgumentException("BloomFilterState.Parameters must not be null.");
            }

            state.Parameters.Validate();

            if (state.Parameters.ScalingParameters.MaxCapacityBehavior != MaxCapacityBehavior.Scale)
            {
                throw new ArgumentException(nameof(state), $"Parameters.ScalingParameters.MaxCapacityBehavior was not set to {MaxCapacityBehavior.Scale}");
            }

            _options = options;
            _parameters = state.Parameters;
            _scalingParameters = state.Parameters.ScalingParameters;

            _metrics = new StateMetrics(_parameters, options.Callbacks);

            if (state.Parameters.Id == null)
            {
                throw new ArgumentException("state.Parameters.Id must not be null", nameof(state));
            }

            if (_scalingParameters.MaxCapacityBehavior == MaxCapacityBehavior.Scale)
            {
                if (state.Children?.Count > 0)
                {
                    _filters = state.Children.Select((childState, i) =>
                    {
                        if (childState.Parameters == null)
                        {
                            throw new ArgumentException($"Invalid state: child filter {i} was missing parameters");
                        }

                        return new ClassicBloomFilter(options, childState);
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
                _filters = new List<ClassicBloomFilter>();
                _activeFilter = new ClassicBloomFilter(options, state);
            }
            else
            {
                Scale();
            }

            _metrics.OnCountChanged(state.Count);
        }

        public string Id => _parameters.Id;

        public long Count => _metrics.Count;

        public IBloomFilterDimensions Dimensions => _metrics;

        public double Saturation => _filters.Sum(x => x.Saturation);

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
                        return new BloomFilterEntry(true, preparedAdd);
                    }

                    return BloomFilterEntry.MaybePresent;

                case BloomFilterCheckBehavior.AddImmediately:

                    using (var entry = PrepareAdd(new BloomFilterCheckRequest(checkRequest.Key, BloomFilterCheckBehavior.PrepareForAdd)))
                    {
                        if (entry.CanAdd)
                        {
                            ApplyPreparedAdd(entry);
                            return BloomFilterEntry.NotPresent;
                        }
                    }

                    return BloomFilterEntry.MaybePresent;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private bool ApplyPreparedAdd(PreparedAdd preparedAdd)
        {
            try
            {
                _lock.EnterWriteLock();

                var added = false;

                if (_activeFilter.Id == preparedAdd.FilterId)
                {
                    added = _activeFilter.ApplyPreparedAdd(preparedAdd);
                }
                else
                {
                    foreach (var filter in _filters)
                    {
                        if (filter.Id == preparedAdd.FilterId)
                        {
                            added = filter.ApplyPreparedAdd(preparedAdd);
                        }
                    }
                }

                if (added)
                {
                    _metrics.IncrementCount(1);
                }

                if (_metrics.Count >= _metrics.Capacity)
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

        public bool IsNotPresent(BloomFilterCheckRequest checkRequest)
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
        private PreparedAdd PrepareAdd(BloomFilterCheckRequest checkRequest)
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

                        var isNotPresent = filter.Check(BloomFilterCheckRequest.CheckOnly(checkRequest.Key)).IsNotPresent;
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
                    return new PreparedAdd(entry.PreparedAdd.FilterId, entry.PreparedAdd.Indexes, ApplyPreparedAdd, entry.PreparedAdd.Release);
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

        public BloomFilterState GetState()
        {
            _lock.EnterReadLock();
            try
            {
                var state = new BloomFilterState
                {
                    Id = _parameters.Id,
                    Parameters = _parameters,
                    Count = Count
                };

                state.Children = _filters.Select(x => x.GetState()).ToList();

                return state;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        [MemberNotNull(nameof(_activeFilter))]
        private void Scale()
        {
            if (_activeFilter == null)
            {
                var bloomFilterDimensions = _parameters.Dimensions;
                if (_parameters.ScalingParameters.MaxCapacityBehavior == MaxCapacityBehavior.Scale)
                {
                    // We need to create the filter with a lower error rate so that the compounded 
                    // error rate for all filters will stay below the requested rate.
                    var rescaledErrorRate = _parameters.Dimensions.ErrorRate / (1 / (1 - _parameters.ScalingParameters.ErrorRateScaling));
                    bloomFilterDimensions = new BloomFilterDimensions.Computer()
                    {
                        Capacity = bloomFilterDimensions.Capacity,
                        FalsePositiveRate = rescaledErrorRate
                    }.Compute();
                }

                var nextParameters = _parameters with
                {
                    Id = $"{_parameters.Id}[{_filters.Count}]",
                    Dimensions = bloomFilterDimensions
                };
                _activeFilter = new ClassicBloomFilter(_options, new BloomFilterState()
                {
                    Parameters = nextParameters
                });
                _filters.Add(_activeFilter);
            }
            else
            {
                var nextBitCount = (int) Math.Round(_activeFilter.Parameters.Dimensions.BitCount * _scalingParameters.CapacityScaling);
                var nextErrorRate = _activeFilter.Parameters.Dimensions.ErrorRate * _scalingParameters.ErrorRateScaling;
                var nextHashCount = (int) Math.Ceiling(_activeFilter.Parameters.Dimensions.HashCount + _filters.Count * Math.Log2(Math.Pow(_activeFilter.Parameters.ScalingParameters.ErrorRateScaling, -1)));

                var nextDimensions = new BloomFilterDimensions.Computer()
                {
                    BitCount = nextBitCount,
                    FalsePositiveRate = nextErrorRate,
                    HashCount = nextHashCount
                }.Compute();

                var nextParameters = (_parameters with
                {
                    Id = $"{_parameters.Id}[{_filters.Count}]",
                    Dimensions = nextDimensions
                });
                _activeFilter = new ClassicBloomFilter(_options, new BloomFilterState()
                {
                    Parameters = nextParameters,
                });
                _filters.Add(_activeFilter);
            }

            _metrics.OnCapacityChanged(_filters.Sum(x => x.Parameters.Dimensions.Capacity));
            _metrics.OnBitCountChanged(_filters.Sum(x => x.Parameters.Dimensions.BitCount));
            _metrics.OnScaled(_activeFilter.Parameters);
        }
    }
}