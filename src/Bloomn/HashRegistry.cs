using System;
using System.Collections.Generic;

namespace Bloomn
{
    public class BloomFilterOptions
    {
        private IKeyHasherFactory? _keyHasher;

        public static BloomFilterOptions DefaultOptions { get; set; } = new BloomFilterOptions();

        public string HasherType { get; set; } = typeof(Murmur3HasherFactory).AssemblyQualifiedName!;

        public BloomFilterDimensions Dimensions { get; set; } = new BloomFilterDimensions();

        public BloomFilterScaling BloomFilterScaling { get; set; } = new BloomFilterScaling();

        public Callbacks Callbacks { get; set; } = new Callbacks();

        public bool DiscardInconsistentState { get; set; }

        public void SetHasher(IKeyHasherFactory hasherFactory)
        {
            HasherType = hasherFactory.GetType().AssemblyQualifiedName!;
            _keyHasher = hasherFactory;
        }

        public IKeyHasherFactory GetHasher()
        {
            if (_keyHasher == null)
            {
                var type = Type.GetType(HasherType, true)!;

                _keyHasher = Activator.CreateInstance(type) as IKeyHasherFactory;

                if (_keyHasher == null)
                {
                    throw new Exception($"HasherType {HasherType} does not implement {typeof(IKeyHasherFactory)}");
                }
            }

            return _keyHasher;
        }
    }
}