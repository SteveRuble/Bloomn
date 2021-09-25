using System;

namespace Bloomn
{
    public interface IKeyHasherFactory<T>
    {
        string Algorithm { get; }
        Func<T, uint> CreateHasher(int seed, int modulus);
    }
}