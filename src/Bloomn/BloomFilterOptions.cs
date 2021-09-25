using System;

namespace Bloomn
{
    public class BloomFilterOptions<T>
    {
        public static string DefaultHasherType = typeof(Murmur3HasherFactory).AssemblyQualifiedName!;
        private IKeyHasherFactory<T>? _keyHasher;

        public static BloomFilterOptions<T> DefaultOptions { get; set; } = new();

        public string HasherType { get; set; } = DefaultHasherType;

        public BloomFilterDimensionsBuilder? Dimensions { get; set; }

        public BloomFilterScaling Scaling { get; set; } = new();

        public Callbacks Callbacks { get; set; } = new();

        public bool DiscardInconsistentState { get; set; }

        public void SetHasher(IKeyHasherFactory<T> hasherFactory)
        {
            HasherType = hasherFactory.GetType().AssemblyQualifiedName!;
            _keyHasher = hasherFactory;
        }

        public BloomFilterDimensions GetDimensions()
        {
            return Dimensions?.Build() ?? new BloomFilterDimensions();
        }

        public IKeyHasherFactory<T> GetHasher()
        {
            if (_keyHasher == null)
            {
                var type = Type.GetType(HasherType, true)!;

                _keyHasher = Activator.CreateInstance(type) as IKeyHasherFactory<T>;

                if (_keyHasher == null)
                {
                    if (HasherType == DefaultHasherType)
                    {
                        throw new BloomFilterException(BloomFilterExceptionCode.InvalidOptions, "The default hasher can handle keys of type string and byte[]. If you " +
                                                                                                $"need to support keys of type {typeof(T)} you will need to implement {typeof(IKeyHasherFactory<T>)} " +
                                                                                                "and set HasherType to the assembly-qualified name, or pass an instance to the " +
                                                                                                "SetHasher method of the options builder.");
                    }

                    throw new BloomFilterException(BloomFilterExceptionCode.InvalidOptions, $"Custom hasher identified by string '{HasherType}' does not implement {typeof(IKeyHasherFactory<T>)}");
                }
            }

            return _keyHasher;
        }
    }
}