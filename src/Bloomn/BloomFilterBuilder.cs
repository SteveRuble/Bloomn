using System;
using System.Linq;
using System.Text.Json;
using Bloomn.Behaviors;
using Microsoft.Extensions.Options;

namespace Bloomn
{
    internal class BloomFilterBuilder<TKey> : IBloomFilterBuilder<TKey>, IBloomFilterOptionsBuilder<TKey>
    {
        private readonly IOptionsSnapshot<BloomFilterOptions<TKey>>? _optionsSnapshot;

        /// <summary>
        /// Create a new builder with an options snapshot allowing use of profiles.
        /// </summary>
        /// <param name="options"></param>
        public BloomFilterBuilder(IOptionsSnapshot<BloomFilterOptions<TKey>> options)
        {
            _optionsSnapshot = options;
            Options = options.Value.Clone();
        }

        /// <summary>
        /// Create a new builder with the provided options.
        /// </summary>
        /// <param name="options"></param>
        /// <param name="cloneOptions">
        /// Optional; if true, the provided options will be cloned and any changes made by
        /// the builder will only be applied to the cloned version. 
        /// </param>
        public BloomFilterBuilder(BloomFilterOptions<TKey> options, bool cloneOptions = true)
        {
            if (cloneOptions)
            {
                Options = options.Clone();
            }
            else
            {
                Options = options;
            }
        }

        /// <summary>
        /// Create a new builder with the default options.
        /// </summary>
        public BloomFilterBuilder()
        {
            Options = BloomFilterOptions<TKey>.DefaultOptions.Clone();
        }

        /// <summary>
        /// Create a new builder with the provided state..
        /// </summary>
        public BloomFilterBuilder(BloomFilterState state)
        {
            State = state;
            Options = BloomFilterOptions<TKey>.DefaultOptions.Clone();
            Options.StateValidationBehavior = StateValidationBehavior.PreferStateConfiguration;
        }

        internal BloomFilterOptions<TKey> Options { get; set; }

        internal BloomFilterState? State { get; set; }

        IBloomFilterOptionsBuilder<TKey> IBloomFilterOptionsBuilder<TKey>.WithCapacityAndFalsePositiveProbability(int capacity, double falsePositiveProbability)
        {
            return WithDimensions(BloomFilterDimensions.ForCapacityAndErrorRate(capacity, falsePositiveProbability));
        }

        public IBloomFilterOptionsBuilder<TKey> WithDimensions(BloomFilterDimensions dimensions)
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

        public IBloomFilterOptionsBuilder<TKey> WithScaling(double capacityScaling = 2, double falsePositiveProbabilityScaling = 0.8)
        {
            Options.Scaling = new BloomFilterScaling
            {
                MaxCapacityBehavior = MaxCapacityBehavior.Scale,
                CapacityScaling = capacityScaling,
                FalsePositiveProbabilityScaling = falsePositiveProbabilityScaling
            };
            return this;
        }

        public IBloomFilterOptionsBuilder<TKey> WithHasherFactory(IKeyHasherFactory<TKey> hasherFactory)
        {
            Options.SetHasherFactory(hasherFactory);
            return this;
        }

        public IBloomFilterBuilder<TKey> WithOptions(BloomFilterOptions<TKey> options)
        {
            Options = options;
            return this;
        }

        public IBloomFilterBuilder<TKey> WithOptions(Action<IBloomFilterOptionsBuilder<TKey>> configure)
        {
            configure(this);
            return this;
        }

        public IBloomFilterOptionsBuilder<TKey> WithEventHandlers(BloomFilterEvents events)
        {
            Options.Events = events;
            return this;
        }

        public IBloomFilterOptionsBuilder<TKey> IgnoreCapacityLimits()
        {
            Options.Scaling = Options.Scaling with {MaxCapacityBehavior = MaxCapacityBehavior.Ignore};
            return this;
        }

        public IBloomFilterOptionsBuilder<TKey> PreferStateConfiguration()
        {
            Options.StateValidationBehavior = StateValidationBehavior.PreferStateConfiguration;
            return this;
        }

        public IBloomFilterOptionsBuilder<TKey> DiscardInconsistentState()
        {
            Options.StateValidationBehavior = StateValidationBehavior.DiscardInconsistentState;
            return this;
        }

        public IBloomFilterBuilder<TKey> WithProfile(string profile)
        {
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
            if (state != null)
            {
                var parametersFromState = state?.Parameters;

                if (parametersFromState != null)
                {
                    var inconsistencies = parametersFromState.Diff(configuredParameters);
                    if (inconsistencies.Any())
                    {
                        switch (Options.StateValidationBehavior)
                        {
                            case StateValidationBehavior.ThrowIfInconsistent:
                                throw new BloomFilterException(BloomFilterExceptionCode.ParameterMismatch,
                                    "When state containing parameters are provided it must be consistent with the configured parameters. " +
                                    $"To change this behavior set {nameof(BloomFilterOptions<TKey>.StateValidationBehavior)} on the options, or " +
                                    $"use the {nameof(IBloomFilterOptionsBuilder<TKey>.DiscardInconsistentState)} or {nameof(IBloomFilterOptionsBuilder<TKey>.PreferStateConfiguration)} " +
                                    $"methods on the option builder.\n" +
                                    $"Configured parameters: {configuredParameters}\n" +
                                    $"Parameters from state: {parametersFromState}\n" +
                                    $"Inconsistencies:\n {string.Join("\n ", inconsistencies)}");
                            case StateValidationBehavior.PreferStateConfiguration:
                                break;
                            case StateValidationBehavior.DiscardInconsistentState:
                                state = null;
                                break;
                            default:
                                throw new BloomFilterException(BloomFilterExceptionCode.InvalidOptions, $"Unsupported state validation behavior {Options.StateValidationBehavior}");
                        }
                    }
                }
            }

            state ??= new BloomFilterState
            {
                Parameters = configuredParameters
            };

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