using System;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Bloomn
{
    public interface IBloomFilterOptionsBuilder
    {
        IBloomFilterBuilder WithCapacityAndErrorRate(int capacity, double errorRate);

        IBloomFilterBuilder WithDimensions(BloomFilterDimensions dimensions);
        
        IBloomFilterBuilder WithScaling(double capacityScaling = 2, double errorRateScaling = 0.8);

        IBloomFilterBuilder WithHasher(IKeyHasherFactory hasherFactory);
    }
    
    public interface IBloomFilterBuilder: IBloomFilterOptionsBuilder
    {
        IBloomFilterBuilder WithOptions(BloomFilterOptions options);

        IBloomFilterBuilder WithProfile(string profile);
        
        IBloomFilterBuilder WithState(BloomFilterState state);

        IBloomFilter Build();
    }

    internal class BloomFilterBuilder : IBloomFilterBuilder
    {
        private readonly IOptionsSnapshot<BloomFilterOptions>? _optionsSnapshot;
        internal BloomFilterOptions Options { get; set; }

        internal BloomFilterState? State { get; set; }

        public BloomFilterBuilder(IOptionsSnapshot<BloomFilterOptions> options)
        {
            _optionsSnapshot = options;
            Options = options.Value;
        }
        
        public BloomFilterBuilder(BloomFilterOptions options)
        {
            Options = options;
        }

        public IBloomFilterBuilder WithCapacityAndErrorRate(int capacity, double errorRate)
        {
            return WithDimensions(BloomFilterDimensions.ForCapacityAndErrorRate(capacity, errorRate));
        }

        public IBloomFilterBuilder WithDimensions(BloomFilterDimensions dimensions)
        {
            Options.Dimensions = dimensions;
            return this;
        }

        public IBloomFilterBuilder WithScaling(double capacityScaling = 2, double errorRateScaling = 0.8)
        {
            Options.BloomFilterScaling = new BloomFilterScaling()
            {
                MaxCapacityBehavior = MaxCapacityBehavior.Scale,
                CapacityScaling = capacityScaling,
                FalsePositiveProbabilityScaling = errorRateScaling
            };
            return this;
        }

        public IBloomFilterBuilder WithHasher(IKeyHasherFactory hasherFactory)
        {
            Options.SetHasher(hasherFactory);
            return this;
        }

        public IBloomFilterBuilder WithOptions(BloomFilterOptions options)
        {
            Options = options;
            return this;
        }

        public IBloomFilterBuilder WithProfile(string profile)
        {
            if (_optionsSnapshot == null)
            {
                throw new InvalidOperationException("This builder was not ");
            }
            Options = _optionsSnapshot.Get(profile);
            return this;
        }

        public IBloomFilterBuilder WithState(string? serializedState)
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

        public IBloomFilterBuilder WithState(BloomFilterState state)
        {
            State = state;
            return this;
        }
        
        public IBloomFilter Build()
        {
            var id = State?.Parameters?.Id ?? Guid.NewGuid().ToString();

            var configuredParameters = new BloomFilterParameters(id)
            {
                Dimensions = Options.Dimensions,
                Scaling = Options.BloomFilterScaling,
                HashAlgorithm = Options.GetHasher().Algorithm,
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

                        throw new InvalidOperationException($"When state containing parameters are provided it must be consistent with the configured parameters. " +
                                                            $"Configured parameters: {configuredParameters}; " +
                                                            $"Parameters from state: {parametersFromState}; " +
                                                            $"Inconsistencies: {string.Join(", ", inconsistencies)}");
                    }
                }
            }

            if (state == null)
            {
                state = new BloomFilterState()
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
                return new ScalingBloomFilter(Options, state);
            }

            return new ClassicBloomFilter(Options, state);
        }
    }
}