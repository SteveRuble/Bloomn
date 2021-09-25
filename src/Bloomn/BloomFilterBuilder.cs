using System;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Bloomn
{
    public interface IBloomFilterOptionsBuilder<TKey>
    {
        IBloomFilterBuilder<TKey> WithCapacityAndErrorRate(int capacity, double errorRate);
        IBloomFilterBuilder<TKey> WithDimensions(BloomFilterDimensions dimensions);
        IBloomFilterBuilder<TKey> WithScaling(double capacityScaling = 2, double errorRateScaling = 0.8);
        IBloomFilterBuilder<TKey> WithHasher(IKeyHasherFactory<TKey> hasherFactory);
    }

    public interface IBloomFilterBuilder<TKey> : IBloomFilterOptionsBuilder<TKey>
    {
        IBloomFilterBuilder<TKey> WithOptions(BloomFilterOptions<TKey> options);
        IBloomFilterBuilder<TKey> WithProfile(string profile);
        IBloomFilterBuilder<TKey> WithState(BloomFilterState state);
        IBloomFilter<TKey> Build();
    }

    internal class BloomFilterBuilder<TKey> : IBloomFilterBuilder<TKey>
    {
        private readonly IOptionsSnapshot<BloomFilterOptions<TKey>>? _optionsSnapshot;

        public BloomFilterBuilder(IOptionsSnapshot<BloomFilterOptions<TKey>> options)
        {
            _optionsSnapshot = options;
            Options = options.Value;
        }

        public BloomFilterBuilder(BloomFilterOptions<TKey> options)
        {
            Options = options;
        }

        internal BloomFilterOptions<TKey> Options { get; set; }

        internal BloomFilterState? State { get; set; }

        public IBloomFilterBuilder<TKey> WithCapacityAndErrorRate(int capacity, double errorRate)
        {
            return WithDimensions(BloomFilterDimensions.ForCapacityAndErrorRate(capacity, errorRate));
        }

        public IBloomFilterBuilder<TKey> WithDimensions(BloomFilterDimensions dimensions)
        {
            Options.Dimensions = new BloomFilterDimensionsBuilder
            {
                FalsePositiveProbability = dimensions.FalsePositiveProbability,
                Capacity = dimensions.Capacity,
                BitCount = dimensions.BitCount,
                HashCount = dimensions.HashCount
            };

            return this;
        }

        public IBloomFilterBuilder<TKey> WithScaling(double capacityScaling = 2, double errorRateScaling = 0.8)
        {
            Options.Scaling = new BloomFilterScaling
            {
                MaxCapacityBehavior = MaxCapacityBehavior.Scale,
                CapacityScaling = capacityScaling,
                FalsePositiveProbabilityScaling = errorRateScaling
            };
            return this;
        }

        public IBloomFilterBuilder<TKey> WithHasher(IKeyHasherFactory<TKey> hasherFactory)
        {
            Options.SetHasher(hasherFactory);
            return this;
        }

        public IBloomFilterBuilder<TKey> WithOptions(BloomFilterOptions<TKey> options)
        {
            Options = options;
            return this;
        }

        public IBloomFilterBuilder<TKey> WithProfile(string profile)
        {
            if (_optionsSnapshot == null)
            {
                throw new InvalidOperationException("This builder was not ");
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
                HashAlgorithm = Options.GetHasher().Algorithm
            };

            var state = State;
            if (state != null)
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