using System;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Bloomn
{
    internal class BloomFilterBuilder<TKey> : IBloomFilterBuilder<TKey>, IBloomFilterOptionsBuilder<TKey>
    {
        private bool _validateStateAgainstOptions;
        private readonly IOptionsSnapshot<BloomFilterOptions<TKey>>? _optionsSnapshot;

        public BloomFilterBuilder(IOptionsSnapshot<BloomFilterOptions<TKey>> options)
        {
            _validateStateAgainstOptions = true;
            _optionsSnapshot = options;
            Options = options.Value;
        }

        public BloomFilterBuilder(BloomFilterOptions<TKey> options)
        {
            Options = options;
        }

        internal BloomFilterOptions<TKey> Options { get; set; }

        internal BloomFilterState? State { get; set; }

        IBloomFilterOptionsBuilder<TKey> IBloomFilterOptionsBuilder<TKey>.WithCapacityAndFalsePositiveProbability(int capacity, double falsePositiveProbability)
        {
            _validateStateAgainstOptions = true;
            return WithDimensions(BloomFilterDimensions.ForCapacityAndErrorRate(capacity, falsePositiveProbability));
        }

        public IBloomFilterOptionsBuilder<TKey> WithDimensions(BloomFilterDimensions dimensions)
        {
            _validateStateAgainstOptions = true;
            Options.Dimensions = new BloomFilterDimensionsBuilder
            {
                FalsePositiveProbability = dimensions.FalsePositiveProbability,
                Capacity = dimensions.Capacity,
                BitCount = dimensions.BitCount,
                HashCount = dimensions.HashCount
            };

            return this;
        }

        public IBloomFilterOptionsBuilder<TKey> WithScaling(double capacityScaling = 2, double errorRateScaling = 0.8)
        {
            _validateStateAgainstOptions = true;
            Options.Scaling = new BloomFilterScaling
            {
                MaxCapacityBehavior = MaxCapacityBehavior.Scale,
                CapacityScaling = capacityScaling,
                FalsePositiveProbabilityScaling = errorRateScaling
            };
            return this;
        }

        public IBloomFilterOptionsBuilder<TKey> WithHasher(IKeyHasherFactory<TKey> hasherFactory)
        {
            _validateStateAgainstOptions = true;
            Options.SetHasher(hasherFactory);
            return this;
        }

        public IBloomFilterBuilder<TKey> WithOptions(BloomFilterOptions<TKey> options)
        {
            _validateStateAgainstOptions = true;
            Options = options;
            return this;
        }

        public IBloomFilterBuilder<TKey> WithOptions(Action<IBloomFilterOptionsBuilder<TKey>> configure)
        {
            _validateStateAgainstOptions = true;
            configure(this);
            return this;
        }

        public IBloomFilterOptionsBuilder<TKey> WithCallbacks(BloomFilterEvents events)
        {
            _validateStateAgainstOptions = true;
            Options.Events = events;
            return this;
        }

        public IBloomFilterOptionsBuilder<TKey> IgnoreCapacityLimits()
        {
            _validateStateAgainstOptions = true;
            Options.Scaling = Options.Scaling with {MaxCapacityBehavior = MaxCapacityBehavior.Ignore};
            return this;
        }

        public IBloomFilterBuilder<TKey> WithProfile(string profile)
        {
            _validateStateAgainstOptions = true;
            if (_optionsSnapshot == null)
            {
                throw new BloomFilterException(BloomFilterExceptionCode.InvalidOptions, "This builder was not acquired from a service provider that could inject options.");
            }

            Options = _optionsSnapshot.Get(profile);
            return this;
        }

        public IBloomFilterBuilder<TKey> WithState(BloomFilterState state)
        {
            State = state;
            return this;
        }

        public IBloomFilter<TKey> Build()
        {
            var id = State?.Parameters?.Id ?? Guid.NewGuid().ToString();

            var configuredParameters = new BloomFilterParameters(id)
            {
                Dimensions = Options.GetDimensions(),
                Scaling = Options.Scaling,
                HashAlgorithm = Options.GetHasherFactory().Algorithm
            };

            var state = State;
            if (state != null && _validateStateAgainstOptions)
            {
                var parametersFromState = state?.Parameters;

                if (parametersFromState != null)
                {
                    var inconsistencies = parametersFromState.Diff(configuredParameters);
                    if (inconsistencies.Any())
                    {
                        if (Options.DiscardInconsistentState)
                        {
                            state = null;
                        }

                        throw new InvalidOperationException("When state containing parameters are provided it must be consistent with the configured parameters. " +
                                                            $"Configured parameters: {configuredParameters}; " +
                                                            $"Parameters from state: {parametersFromState}; " +
                                                            $"Inconsistencies: {string.Join(", ", inconsistencies)}");
                    }
                }
            }

            if (state == null)
            {
                state = new BloomFilterState
                {
                    Parameters = configuredParameters
                };
            }

            if (state.Parameters == null)
            {
                throw new Exception("State parameters not found.");
            }

            if (state.Parameters.Scaling.MaxCapacityBehavior == MaxCapacityBehavior.Scale)
            {
                return new ScalingBloomFilter<TKey>(Options, state);
            }

            return new FixedSizeBloomFilter<TKey>(Options, state);
        }

        public IBloomFilterBuilder<TKey> WithState(string? serializedState)
        {
            if (serializedState == null)
            {
                return this;
            }

            var state = JsonSerializer.Deserialize<BloomFilterState>(serializedState);
            if (state == null)
            {
                throw new ArgumentException("Serialized state deserialized to null.", nameof(serializedState));
            }

            return WithState(state);
        }
    }
}