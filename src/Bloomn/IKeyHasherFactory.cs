using System;

namespace Bloomn
{
    public interface IKeyHasherFactory
    {
        string Algorithm { get; }
        uint Hash(ReadOnlySpan<Byte> key, int seed);
    }
}