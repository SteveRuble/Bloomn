using System;
using System.Buffers;
using Bloomn.Behaviors;

namespace Bloomn
{
    public class BloomFilterOptions<T>
    {
        public static string DefaultHasherType = typeof(DefaultHasherFactoryV1).AssemblyQualifiedName!;

        private IKeyHasherFactory<T>? _keyHasher;

        public static BloomFilterOptions<T> DefaultOptions { get; set; } = new();

        public string? Profile { get; set; }

        public string HasherType { get; set; } = DefaultHasherType;

        public BloomFilterDimensionsBuilder? Dimensions { get; set; }

        public BloomFilterScaling Scaling { get; set; } = new();

        public BloomFilterEvents Events { get; set; } = new();

        public StateValidationBehavior StateValidationBehavior { get; set; }

        public void SetHasherFactory(IKeyHasherFactory<T> hasherFactory)
        {
            HasherType = hasherFactory.GetType().AssemblyQualifiedName!;
            _keyHasher = hasherFactory;
        }

        public BloomFilterDimensions GetDimensions()
        {
            return Dimensions?.Build() ?? new BloomFilterDimensions();
        }

        public IKeyHasherFactory<T> GetHasherFactory()
        {
            if (_keyHasher == null)
            {
                var type = Type.GetType(HasherType, true)!;

                _keyHasher = Activator.CreateInstance(type) as IKeyHasherFactory<T>;

                if (_keyHasher == null)
                {
                    if (HasherType == DefaultHasherType)
                    {
                        throw new BloomFilterException(BloomFilterExceptionCode.InvalidOptions, "The default hasher can handle keys of type string, byte[], int, long, float, double, and decimal. " +
                                                                                                $"If you need to support keys of type {typeof(T)} you will need to implement {typeof(IKeyHasherFactory<T>)} " +
                                                                                                "and set HasherType to the assembly-qualified name, or pass an instance to the " +
                                                                                                "SetHasher method of the options builder.");
                    }

                    throw new BloomFilterException(BloomFilterExceptionCode.InvalidOptions, $"Custom hasher identified by string '{HasherType}' does not implement {typeof(IKeyHasherFactory<T>)}");
                }
            }

            return _keyHasher;
        }

        public BloomFilterOptions<T> Clone()
        {
            return new BloomFilterOptions<T>
            {
                _keyHasher = _keyHasher,
                Profile = Profile,
                HasherType = HasherType,
                Dimensions = Dimensions,
                Scaling = Scaling,
                Events = Events,
                StateValidationBehavior = StateValidationBehavior,
            };
        }
    }
}